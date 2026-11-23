[CmdletBinding()]
param(
    [string] $BicepPath = 'bicep',
    [string] $FoundationParameters,
    [string] $ApplicationParameters
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$monitorDnsZoneNames = @(
    'privatelink.monitor.azure.com',
    'privatelink.oms.opinsights.azure.com',
    'privatelink.ods.opinsights.azure.com',
    'privatelink.agentsvc.azure-automation.net',
    'privatelink.blob.core.windows.net'
)

function Assert-Condition([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}

function Get-Ipv4Range([string] $Cidr) {
    Assert-Condition ($Cidr -match '^\d{1,3}(\.\d{1,3}){3}/\d{1,2}$') 'Expected a canonical IPv4 CIDR.'
    $parts = $Cidr.Split('/')
    $ip = [System.Net.IPAddress]::Parse($parts[0])
    $bits = [int] $parts[1]
    Assert-Condition ($bits -ge 8 -and $bits -le 29) 'IPv4 CIDR prefix must be between /8 and /29.'
    $octets = $ip.GetAddressBytes()
    [long] $number = ([long] $octets[0] * 16777216) + ([long] $octets[1] * 65536) + ([long] $octets[2] * 256) + $octets[3]
    [long] $size = [math]::Pow(2, 32 - $bits)
    Assert-Condition (($number % $size) -eq 0) 'CIDR must identify the network address, not a host address.'
    @{ First = $number; Last = $number + $size - 1; Bits = $bits }
}

function Test-Network([hashtable] $Values) {
    $vnet = Get-Ipv4Range $Values.vnetCidr
    $aca = Get-Ipv4Range $Values.acaSubnetCidr
    $pe = Get-Ipv4Range $Values.privateEndpointSubnetCidr
    Assert-Condition ($aca.Bits -le 27) 'ACA workload-profile subnet must be /27 or larger.'
    foreach ($subnet in @($aca, $pe)) {
        Assert-Condition ($subnet.First -ge $vnet.First -and $subnet.Last -le $vnet.Last) 'Both subnets must be inside the VNet.'
    }
    Assert-Condition ($aca.Last -lt $pe.First -or $pe.Last -lt $aca.First) 'ACA and private-endpoint subnets must not overlap.'
    foreach ($range in @($vnet, $aca, $pe)) {
        $private = ($range.First -ge 167772160 -and $range.Last -le 184549375) -or
            ($range.First -ge 2886729728 -and $range.Last -le 2887778303) -or
            ($range.First -ge 3232235520 -and $range.Last -le 3232301055)
        Assert-Condition $private 'Use explicitly approved RFC1918 IPv4 address ranges.'
        foreach ($reserved in @((Get-Ipv4Range '172.30.0.0/16'), (Get-Ipv4Range '172.31.0.0/16'))) {
            Assert-Condition ($range.Last -lt $reserved.First -or $reserved.Last -lt $range.First) 'Network overlaps an ACA/AKS reserved range.'
        }
    }
}

function Test-Common([hashtable] $Values) {
    Assert-Condition ($Values.prefix -cmatch '^[a-z0-9]{2,6}$') 'prefix must be 2-6 lowercase alphanumeric characters.'
    Assert-Condition ($Values.environmentName -cmatch '^[a-z0-9]{2,3}$') 'environmentName must be 2-3 lowercase alphanumeric characters.'
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($Values.location)) 'location must be explicitly supplied.'
}

function Assert-ObjectContract($Value, [string[]] $Required, [string[]] $Optional, [string] $Context) {
    Assert-Condition ($Value -is [hashtable]) "Expected an object for $Context."
    foreach ($name in $Required) {
        Assert-Condition ($Value.ContainsKey($name)) "Missing $Context.$name."
    }
    foreach ($name in $Value.Keys) {
        Assert-Condition ($name -cin ($Required + $Optional)) "Unknown property $Context.$name."
    }
}

function Assert-Choice($Value, [string[]] $Allowed, [string] $Context) {
    Assert-Condition ($Value -is [string] -and $Value -cin $Allowed) "Unsupported value for $Context."
}

function Test-ResourceId($Value, [string] $ResourceType) {
    $pattern = '^/subscriptions/([0-9a-fA-F-]{36})/resourceGroups/([^/]+)/providers/' +
        [regex]::Escape($ResourceType) + '/([^/]+)$'
    Assert-Condition ($Value -is [string] -and $Value -imatch $pattern) "Expected a resource-group-scoped $ResourceType resource ID."
    $id = [guid]::Empty
    Assert-Condition ([guid]::TryParse($Value.Split('/')[2], [ref] $id) -and $id -ne [guid]::Empty) 'Resource ID requires a nonempty subscription GUID.'
    Assert-Condition ($Value -notmatch '[<>\s]|(?i)placeholder|replace[-_]?me|your[-_]|todo') 'Replace placeholder resource IDs with approved deployment values.'
}

function Test-Monitoring([hashtable] $Values) {
    foreach ($name in @('monitoringAccessProfile', 'monitoringTransitionApprovalReference', 'monitoringAmpls', 'monitoringPrivateEndpoint', 'monitoringDns')) {
        Assert-Condition ($Values.ContainsKey($name)) "Missing monitoring input: $name."
    }
    Assert-Choice $Values.monitoringAccessProfile @('Private', 'Transition') 'monitoringAccessProfile'
    $approval = $Values.monitoringTransitionApprovalReference
    Assert-Condition ($approval -is [string]) 'monitoringTransitionApprovalReference must be a string.'
    if ($Values.monitoringAccessProfile -ceq 'Transition') {
        Assert-Condition (-not [string]::IsNullOrWhiteSpace($approval) -and $approval -notmatch '[<>]|(?i)pending|todo|placeholder') 'Transition requires an approved change/exception reference, not a placeholder.'
    } else {
        Assert-Condition ($approval -ceq '') 'Private profile requires an explicit empty transition approval reference.'
    }

    $ampls = $Values.monitoringAmpls
    Assert-Condition ($ampls -is [hashtable] -and $ampls.ContainsKey('mode')) 'monitoringAmpls requires a mode.'
    Assert-Choice $ampls.mode @('Create', 'Existing') 'monitoringAmpls.mode'
    if ($ampls.mode -ceq 'Create') {
        Assert-ObjectContract $ampls @('mode', 'associationOwner', 'ingestionAccessMode', 'queryAccessMode') @() 'monitoringAmpls'
        Assert-Choice $ampls.associationOwner @('Project') 'monitoringAmpls.associationOwner'
        Assert-Choice $ampls.ingestionAccessMode @('PrivateOnly') 'monitoringAmpls.ingestionAccessMode'
        Assert-Choice $ampls.queryAccessMode @('PrivateOnly') 'monitoringAmpls.queryAccessMode'
    } else {
        Assert-ObjectContract $ampls @('mode', 'resourceId', 'associationOwner') @('workspaceAssociationName', 'applicationInsightsAssociationName') 'monitoringAmpls'
        Test-ResourceId $ampls.resourceId 'Microsoft.Insights/privateLinkScopes'
        Assert-Choice $ampls.associationOwner @('Project', 'Platform') 'monitoringAmpls.associationOwner'
        foreach ($name in @('workspaceAssociationName', 'applicationInsightsAssociationName')) {
            if ($ampls.associationOwner -ceq 'Project') {
                Assert-Condition ($ampls.ContainsKey($name) -and $ampls[$name] -is [string] -and $ampls[$name] -cmatch '^[A-Za-z0-9._-]+$') "Project-managed existing AMPLS requires an explicitly owned $name."
            } else {
                Assert-Condition (-not $ampls.ContainsKey($name)) 'Platform-managed associations must not supply project association names.'
            }
        }
        if ($ampls.associationOwner -ceq 'Project') {
            Assert-Condition ($ampls.workspaceAssociationName -ine $ampls.applicationInsightsAssociationName) 'AMPLS association names must be distinct.'
        }
    }

    $endpoint = $Values.monitoringPrivateEndpoint
    Assert-Condition ($endpoint -is [hashtable] -and $endpoint.ContainsKey('mode')) 'monitoringPrivateEndpoint requires a mode.'
    Assert-Choice $endpoint.mode @('Create', 'Existing') 'monitoringPrivateEndpoint.mode'
    if ($endpoint.mode -ceq 'Create') {
        Assert-ObjectContract $endpoint @('mode') @() 'monitoringPrivateEndpoint'
        $pe = Get-Ipv4Range $Values.privateEndpointSubnetCidr
        Assert-Condition ($pe.Bits -le 27) 'Azure Monitor Private Link requires a /27 or larger endpoint subnet; verify at least 11 free Monitor IPs separately.'
    } else {
        Assert-ObjectContract $endpoint @('mode', 'resourceId') @() 'monitoringPrivateEndpoint'
        Test-ResourceId $endpoint.resourceId 'Microsoft.Network/privateEndpoints'
        Assert-Condition ($ampls.mode -ceq 'Existing') 'An existing endpoint requires an existing AMPLS.'
    }

    $dns = $Values.monitoringDns
    Assert-Condition ($dns -is [hashtable] -and $dns.ContainsKey('mode')) 'monitoringDns requires a mode.'
    Assert-Choice $dns.mode @('Managed', 'PlatformManaged') 'monitoringDns.mode'
    if ($dns.mode -ceq 'PlatformManaged') {
        Assert-ObjectContract $dns @('mode') @() 'monitoringDns'
        Assert-Condition ($endpoint.mode -ceq 'Existing') 'PlatformManaged DNS requires an existing platform endpoint; do not create an endpoint without its DNS integration.'
    } else {
        Assert-ObjectContract $dns @('mode', 'zones') @() 'monitoringDns'
        Assert-Condition ($dns.zones -is [array] -and $dns.zones.Count -eq 5) 'Managed Monitor DNS requires exactly five zones.'
        $seenNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        $seenIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($zone in $dns.zones) {
            Assert-Condition ($zone -is [hashtable] -and $zone.ContainsKey('name') -and $zone.ContainsKey('mode')) 'Each Monitor DNS zone requires name and mode.'
            Assert-Choice $zone.name $monitorDnsZoneNames 'Monitor DNS zone name'
            Assert-Condition ($seenNames.Add($zone.name)) 'Duplicate Monitor DNS zone/VNet link definition.'
            Assert-Choice $zone.mode @('Create', 'Existing') 'Monitor DNS zone mode'
            if ($zone.mode -ceq 'Create') {
                Assert-ObjectContract $zone @('name', 'mode', 'linkMode') @() 'Monitor DNS zone'
                Assert-Choice $zone.linkMode @('Create') 'New Monitor zone linkMode'
                Assert-Condition ($endpoint.mode -ceq 'Create') 'Do not create empty DNS zones for an existing endpoint; reuse its existing zones.'
            } else {
                Assert-ObjectContract $zone @('name', 'mode', 'resourceId', 'linkMode') @('existingLinkName') 'Monitor DNS zone'
                Test-ResourceId $zone.resourceId 'Microsoft.Network/privateDnsZones'
                Assert-Condition (($zone.resourceId.Split('/')[-1]) -ieq $zone.name) 'Monitor DNS zone name does not match its resource ID.'
                Assert-Condition ($seenIds.Add($zone.resourceId)) 'Duplicate Monitor DNS zone resource ID.'
                Assert-Choice $zone.linkMode @('Create', 'Existing', 'External') 'Existing Monitor zone linkMode'
                if ($zone.linkMode -ceq 'Existing') {
                    Assert-Condition ($zone.ContainsKey('existingLinkName') -and $zone.existingLinkName -is [string] -and $zone.existingLinkName -cmatch '^[A-Za-z0-9._-]+$') 'Existing VNet link requires its actual existingLinkName; do not create a second link.'
                } else {
                    Assert-Condition (-not $zone.ContainsKey('existingLinkName')) 'existingLinkName is only valid for Existing linkMode.'
                }
            }
        }
    }
}

function Test-Sql([hashtable] $Values) {
    $adminId = [guid]::Empty
    Assert-Condition ([guid]::TryParse($Values.sqlAdminObjectId, [ref] $adminId) -and $adminId -ne [guid]::Empty) 'sqlAdminObjectId must be a nonempty GUID.'
    Assert-Choice $Values.sqlAdminPrincipalType @('Group', 'User') 'sqlAdminPrincipalType'
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($Values.sqlAdminName)) 'sqlAdminName must be supplied.'
    Assert-Condition ($Values.databaseName -is [string] -and $Values.databaseName.Length -ge 1 -and $Values.databaseName.Length -le 128 -and $Values.databaseName -cmatch '^[A-Za-z0-9._-]+$') 'databaseName must be 1-128 characters of letters, digits, ., _ or -.'
    Assert-Condition ([double] $Values.sqlMinVcores -le [double] $Values.sqlMaxVcores) 'sqlMinVcores must not exceed sqlMaxVcores.'
    $retention = $Values.sqlLongTermRetention
    Assert-ObjectContract $retention @('weeklyRetention', 'monthlyRetention', 'yearlyRetention', 'weekOfYear') @() 'sqlLongTermRetention'
    foreach ($name in @('weeklyRetention', 'monthlyRetention', 'yearlyRetention')) {
        Assert-Condition ($retention[$name] -is [string] -and $retention[$name] -cmatch '^P(?!$)(\d+Y)?(\d+M)?(\d+W)?(\d+D)?(T(\d+H)?(\d+M)?(\d+S)?)?$') "sqlLongTermRetention.$name must be an explicit ISO 8601 duration (PT0S for none)."
    }
}

function Test-Foundation([hashtable] $Values) {
    Test-Common $Values
    Test-Network $Values
    Test-Sql $Values
    Assert-Condition ($Values.acrSku -ceq 'Premium') 'ACR Private Link requires an explicitly approved Premium SKU; zone redundancy does not.'
    Test-Monitoring $Values
}

function Test-Application([hashtable] $Values) {
    Test-Common $Values
    Assert-Condition ($Values.minReplicas -le $Values.maxReplicas) 'Minimum replicas exceed maximum replicas.'
    Assert-Condition ($Values.imageDigest -cmatch '^sha256:[0-9a-f]{64}$') 'Expected an immutable lowercase SHA-256 image digest.'
    Assert-Condition ($Values.imageRepository -cmatch '^[a-z0-9]+([._/-][a-z0-9]+)*$') 'Invalid registry image repository.'
    foreach ($name in @('entraTenantId', 'entraClientId')) {
        $id = [guid]::Empty
        Assert-Condition ([guid]::TryParse($Values[$name], [ref] $id) -and $id -ne [guid]::Empty) "Expected a nonempty GUID for $name."
    }
    Assert-Condition ($Values.databaseName -cmatch '^[A-Za-z0-9._-]{1,128}$') 'Invalid databaseName.'
    $uri = $null
    Assert-Condition ([uri]::TryCreate($Values.holidaySourceUrl, [System.UriKind]::Absolute, [ref] $uri) -and $uri.Scheme -ceq 'https') 'holidaySourceUrl must be an absolute https URL.'
    Assert-Condition ($Values.holidaySourceTimeoutSeconds -gt 0) 'holidaySourceTimeoutSeconds must be greater than zero.'
}

function Read-ParameterValues([string] $Path, [hashtable] $Template) {
    $document = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
    Assert-Condition ($document.ContainsKey('parameters') -and $document.parameters -is [hashtable]) 'Expected an ARM deployment-parameters JSON file.'
    $values = @{}
    foreach ($name in $Template.parameters.Keys) {
        $schema = $Template.parameters[$name]
        if (-not $document.parameters.ContainsKey($name)) {
            Assert-Condition ($schema.ContainsKey('defaultValue')) "Missing required parameter: $name."
            $values[$name] = $schema.defaultValue
            continue
        }
        $entry = $document.parameters[$name]
        Assert-Condition ($entry -is [hashtable] -and $entry.ContainsKey('value') -and $entry.Count -eq 1) "Expected a value for parameter: $name."
        $value = $entry.value
        $expectedType = if ($schema.ContainsKey('type')) { $schema.type } else { 'object' }
        $correctType = switch ($expectedType) {
            'string' { $value -is [string] }
            'int' { $value -is [long] -or $value -is [int] }
            'bool' { $value -is [bool] }
            'array' { $value -is [array] }
            'object' { $value -is [hashtable] }
            default { throw "Unsupported parameter type for $name." }
        }
        Assert-Condition $correctType "Wrong parameter type: $name."
        if ($schema.ContainsKey('allowedValues')) {
            foreach ($item in @($value)) {
                Assert-Condition ($item -cin $schema.allowedValues) "Unsupported parameter value: $name."
            }
        }
        foreach ($bound in @('minValue', 'maxValue', 'minLength', 'maxLength')) {
            if (-not $schema.ContainsKey($bound)) { continue }
            $measurement = if ($bound.EndsWith('Length')) { $value.Length } else { $value }
            $passes = if ($bound.StartsWith('min')) { $measurement -ge $schema[$bound] } else { $measurement -le $schema[$bound] }
            Assert-Condition $passes "Parameter outside $bound constraint: $name."
        }
        $values[$name] = $value
    }
    foreach ($name in $document.parameters.Keys) {
        Assert-Condition ($Template.parameters.ContainsKey($name)) "Unknown parameter: $name."
    }
    $values
}

function Expect-Failure([scriptblock] $Action, [string] $Message, [string] $ExpectedError) {
    $failed = $false
    try { & $Action } catch {
        $failed = $true
        if ($ExpectedError) {
            Assert-Condition ($_.Exception.Message -like "*$ExpectedError*") "Unexpected failure: $($_.Exception.Message)"
        }
    }
    Assert-Condition $failed $Message
}

function Copy-TestValues([hashtable] $Values) {
    $Values | ConvertTo-Json -Depth 30 | ConvertFrom-Json -AsHashtable
}

function Get-TemplateResources([hashtable] $Template) {
    if ($Template.resources -is [hashtable]) { $Template.resources.Values } else { $Template.resources }
}

Get-Command $BicepPath -ErrorAction Stop | Out-Null
$temporaryFiles = [System.Collections.Generic.List[string]]::new()
try {
    $templates = @{}
    foreach ($file in @('main', 'app', 'modules\private-endpoint', 'modules\internal-environment-dns', 'modules\monitor-private-link-scope', 'modules\monitor-private-endpoint', 'modules\monitor-private-dns-zone')) {
        $output = Join-Path ([System.IO.Path]::GetTempPath()) "bank-holidays-$([System.IO.Path]::GetFileName($file))-$([guid]::NewGuid().ToString('N')).json"
        $temporaryFiles.Add($output)
        & $BicepPath build (Join-Path $PSScriptRoot "$file.bicep") --no-restore --outfile $output
        Assert-Condition ($LASTEXITCODE -eq 0) "Bicep compilation failed: $file."
        $templates[$file] = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json -AsHashtable
    }

    $syntheticNetwork = @{ vnetCidr = '10.60.0.0/16'; acaSubnetCidr = '10.60.0.0/27'; privateEndpointSubnetCidr = '10.60.1.0/28' }
    Test-Network $syntheticNetwork
    $overlap = $syntheticNetwork.Clone()
    $overlap.privateEndpointSubnetCidr = '10.60.0.0/28'
    Expect-Failure { Test-Network $overlap } 'Overlapping subnets were accepted.'
    $small = $syntheticNetwork.Clone()
    $small.acaSubnetCidr = '10.60.0.0/28'
    Expect-Failure { Test-Network $small } 'An undersized ACA subnet was accepted.'
    $outside = $syntheticNetwork.Clone()
    $outside.privateEndpointSubnetCidr = '10.61.0.0/28'
    Expect-Failure { Test-Network $outside } 'A subnet outside the VNet was accepted.'
    Expect-Failure { Get-Ipv4Range '10.60.0.1/27' } 'A host-address CIDR was accepted.'
    Expect-Failure { Get-Ipv4Range 'not-a-cidr' } 'A malformed CIDR was accepted.'
    $reservedNetwork = @{ vnetCidr = '172.30.0.0/16'; acaSubnetCidr = '172.30.0.0/27'; privateEndpointSubnetCidr = '172.30.1.0/28' }
    Expect-Failure { Test-Network $reservedNetwork } 'An ACA-reserved network was accepted.'

    $syntheticFoundation = $syntheticNetwork + @{
        prefix = 'test'
        environmentName = 't01'
        location = 'synthetic-not-a-deployment'
        acrZoneRedundant = $false
        acrSku = 'Premium'
        sqlAdminName = 'test-sql-admins'
        sqlAdminObjectId = '00000000-0000-0000-0000-000000000009'
        sqlAdminPrincipalType = 'Group'
        databaseName = 'DEFRA.BankHolidays'
        sqlMaxVcores = 2
        sqlMinVcores = '0.5'
        sqlMaxSizeGiB = 32
        sqlZoneRedundant = $false
        sqlBackupRedundancy = 'Local'
        sqlPointInTimeRetentionDays = 7
        sqlLongTermRetention = @{ weeklyRetention = 'PT0S'; monthlyRetention = 'PT0S'; yearlyRetention = 'PT0S'; weekOfYear = 1 }
        monitoringAccessProfile = 'Private'
        monitoringTransitionApprovalReference = ''
        monitoringAmpls = @{ mode = 'Create'; associationOwner = 'Project'; ingestionAccessMode = 'PrivateOnly'; queryAccessMode = 'PrivateOnly' }
        monitoringPrivateEndpoint = @{ mode = 'Create' }
        monitoringDns = @{
            mode = 'Managed'
            zones = @($monitorDnsZoneNames | ForEach-Object { @{ name = $_; mode = 'Create'; linkMode = 'Create' } })
        }
    }
    $syntheticFoundation.privateEndpointSubnetCidr = '10.60.1.0/27'
    Test-Foundation $syntheticFoundation
    $zonalRegistry = $syntheticFoundation.Clone()
    $zonalRegistry.acrZoneRedundant = $true
    Test-Foundation $zonalRegistry
    foreach ($sku in @('Basic', 'Standard')) {
        foreach ($zoneRedundant in @($false, $true)) {
            $badRegistry = $syntheticFoundation.Clone()
            $badRegistry.acrSku = $sku
            $badRegistry.acrZoneRedundant = $zoneRedundant
            Expect-Failure { Test-Foundation $badRegistry } 'A non-Premium SKU was accepted for private ACR connectivity.'
        }
    }

    # These IDs are in-memory test fixtures only, never deployment parameter values.
    $fixtureSubscription = '11111111-2222-4333-8444-555555555555'
    $fixtureScopeId = "/subscriptions/$fixtureSubscription/resourceGroups/test-monitor/providers/Microsoft.Insights/privateLinkScopes/test-scope"
    $fixtureEndpointId = "/subscriptions/$fixtureSubscription/resourceGroups/test-network/providers/Microsoft.Network/privateEndpoints/test-monitor"
    $existingProject = Copy-TestValues $syntheticFoundation
    $existingProject.monitoringAmpls = @{
        mode = 'Existing'
        resourceId = $fixtureScopeId
        associationOwner = 'Project'
        workspaceAssociationName = 'owned-workspace'
        applicationInsightsAssociationName = 'owned-insights'
    }
    $existingProject.monitoringDns.zones = @($monitorDnsZoneNames | ForEach-Object {
        @{ name = $_; mode = 'Existing'; resourceId = "/subscriptions/$fixtureSubscription/resourceGroups/test-dns/providers/Microsoft.Network/privateDnsZones/$_"; linkMode = 'Existing'; existingLinkName = 'platform-link' }
    })
    Test-Foundation $existingProject
    $mixedDns = Copy-TestValues $existingProject
    $mixedDns.monitoringDns.zones[0] = @{ name = $monitorDnsZoneNames[0]; mode = 'Create'; linkMode = 'Create' }
    $mixedDns.monitoringDns.zones[1].linkMode = 'Create'
    $mixedDns.monitoringDns.zones[1].Remove('existingLinkName')
    $mixedDns.monitoringDns.zones[2].linkMode = 'External'
    $mixedDns.monitoringDns.zones[2].Remove('existingLinkName')
    Test-Foundation $mixedDns
    $existingPlatform = Copy-TestValues $existingProject
    $existingPlatform.monitoringAmpls = @{ mode = 'Existing'; resourceId = $fixtureScopeId; associationOwner = 'Platform' }
    $existingPlatform.monitoringPrivateEndpoint = @{ mode = 'Existing'; resourceId = $fixtureEndpointId }
    $existingPlatform.monitoringDns = @{ mode = 'PlatformManaged' }
    $existingPlatform.privateEndpointSubnetCidr = '10.60.1.0/28'
    Test-Foundation $existingPlatform
    $existingEndpointWithLinks = Copy-TestValues $existingProject
    $existingEndpointWithLinks.monitoringPrivateEndpoint = @{ mode = 'Existing'; resourceId = $fixtureEndpointId }
    Test-Foundation $existingEndpointWithLinks
    $platformScopeWithProjectEndpoint = Copy-TestValues $existingProject
    $platformScopeWithProjectEndpoint.monitoringAmpls = @{ mode = 'Existing'; resourceId = $fixtureScopeId; associationOwner = 'Platform' }
    Test-Foundation $platformScopeWithProjectEndpoint
    $transition = Copy-TestValues $syntheticFoundation
    $transition.monitoringAccessProfile = 'Transition'
    $transition.monitoringTransitionApprovalReference = 'SYNTHETIC-TEST-ONLY'
    Test-Foundation $transition

    $invalidMonitoringCases = @(
        @{ Name = 'missing monitoring configuration'; Error = 'Missing monitoring input'; Change = { param($v) $v.Remove('monitoringAmpls') } },
        @{ Name = 'invalid profile'; Error = 'monitoringAccessProfile'; Change = { param($v) $v.monitoringAccessProfile = 'Public' } },
        @{ Name = 'unapproved transition'; Error = 'approved change/exception'; Change = { param($v) $v.monitoringAccessProfile = 'Transition' } },
        @{ Name = 'public new scope'; Error = 'ingestionAccessMode'; Change = { param($v) $v.monitoringAmpls.ingestionAccessMode = 'Open' } },
        @{ Name = 'undersized Monitor subnet'; Error = '/27 or larger'; Change = { param($v) $v.privateEndpointSubnetCidr = '10.60.1.0/28' } },
        @{ Name = 'duplicate zone/link'; Error = 'Duplicate Monitor DNS'; Change = { param($v) $v.monitoringDns.zones[1] = $v.monitoringDns.zones[0] } },
        @{ Name = 'missing zone'; Error = 'exactly five'; Change = { param($v) $v.monitoringDns.zones = @($v.monitoringDns.zones[0]) } },
        @{ Name = 'new endpoint without managed DNS'; Error = 'existing platform endpoint'; Change = { param($v) $v.monitoringDns = @{ mode = 'PlatformManaged' } } },
        @{ Name = 'new scope with existing endpoint'; Error = 'existing AMPLS'; Change = { param($v) $v.monitoringPrivateEndpoint = @{ mode = 'Existing'; resourceId = $fixtureEndpointId } } }
    )
    foreach ($case in $invalidMonitoringCases) {
        $invalid = Copy-TestValues $syntheticFoundation
        & $case.Change $invalid
        Expect-Failure { Test-Foundation $invalid } "Accepted $($case.Name)." $case.Error
    }
    foreach ($badId in @('', '/subscriptions/<subscription-id>/resourceGroups/rg/providers/Microsoft.Insights/privateLinkScopes/scope', $fixtureEndpointId, $fixtureScopeId.Replace($fixtureSubscription, [guid]::Empty.ToString()))) {
        $invalid = Copy-TestValues $existingProject
        $invalid.monitoringAmpls.resourceId = $badId
        Expect-Failure { Test-Foundation $invalid } 'Accepted an invalid existing AMPLS ID.'
    }
    $invalid = Copy-TestValues $existingProject
    $invalid.monitoringAmpls.Remove('resourceId')
    Expect-Failure { Test-Foundation $invalid } 'Accepted a missing AMPLS ID.' 'resourceId'
    $invalid = Copy-TestValues $existingProject
    $invalid.monitoringAmpls.Remove('workspaceAssociationName')
    Expect-Failure { Test-Foundation $invalid } 'Accepted unowned existing association names.' 'explicitly owned'
    $invalid = Copy-TestValues $existingProject
    $invalid.monitoringAmpls.associationOwner = 'Platform'
    Expect-Failure { Test-Foundation $invalid } 'Accepted project names with platform ownership.' 'must not supply'
    $invalid = Copy-TestValues $existingProject
    $invalid.monitoringAmpls.ingestionAccessMode = 'PrivateOnly'
    Expect-Failure { Test-Foundation $invalid } 'Accepted shared AMPLS settings in Existing mode.' 'Unknown property'
    $invalid = Copy-TestValues $existingProject
    $invalid.monitoringDns.zones[0].Remove('existingLinkName')
    Expect-Failure { Test-Foundation $invalid } 'Accepted an unnamed existing link.' 'actual existingLinkName'
    $invalid = Copy-TestValues $existingProject
    $invalid.monitoringDns.zones[0].resourceId = $invalid.monitoringDns.zones[1].resourceId
    Expect-Failure { Test-Foundation $invalid } 'Accepted a mismatched/duplicate zone ID.' 'does not match'
    foreach ($badId in @('', $fixtureScopeId)) {
        $invalid = Copy-TestValues $existingProject
        $invalid.monitoringDns.zones[0].resourceId = $badId
        Expect-Failure { Test-Foundation $invalid } 'Accepted an invalid existing DNS zone ID.'
    }
    $invalid = Copy-TestValues $existingProject
    $invalid.monitoringDns.zones[0].Remove('resourceId')
    Expect-Failure { Test-Foundation $invalid } 'Accepted a missing DNS zone ID.' 'resourceId'
    $invalid = Copy-TestValues $existingPlatform
    $invalid.monitoringPrivateEndpoint.Remove('resourceId')
    Expect-Failure { Test-Foundation $invalid } 'Accepted a missing existing endpoint ID.' 'resourceId'
    $invalid = Copy-TestValues $existingEndpointWithLinks
    $invalid.monitoringDns.zones[0] = @{ name = $monitorDnsZoneNames[0]; mode = 'Create'; linkMode = 'Create' }
    Expect-Failure { Test-Foundation $invalid } 'Accepted empty DNS zones with an existing endpoint.' 'empty DNS zones'
    foreach ($badId in @('', $fixtureScopeId)) {
        $invalid = Copy-TestValues $existingPlatform
        $invalid.monitoringPrivateEndpoint.resourceId = $badId
        Expect-Failure { Test-Foundation $invalid } 'Accepted an invalid existing endpoint ID.'
    }

    $syntheticApp = @{
        prefix = 'test'
        environmentName = 't01'
        location = 'synthetic-not-a-deployment'
        imageRepository = 'test/application'
        imageDigest = 'sha256:' + ('a' * 64)
        minReplicas = 0
        maxReplicas = 1
        entraTenantId = '00000000-0000-0000-0000-000000000001'
        entraClientId = '00000000-0000-0000-0000-000000000002'
        databaseName = 'DEFRA.BankHolidays'
        holidaySourceUrl = 'https://www.gov.uk/bank-holidays.json'
        holidaySourceTimeoutSeconds = 30
    }
    Test-Application $syntheticApp
    $badScale = $syntheticApp.Clone()
    $badScale.minReplicas = 2
    Expect-Failure { Test-Application $badScale } 'Minimum replicas above maximum were accepted.'
    $badImage = $syntheticApp.Clone()
    $badImage.imageDigest = 'latest'
    Expect-Failure { Test-Application $badImage } 'A mutable image tag was accepted as a digest.'
    $badTenant = $syntheticApp.Clone()
    $badTenant.entraTenantId = [guid]::Empty.ToString()
    Expect-Failure { Test-Application $badTenant } 'An empty application tenant was accepted.'
    $insecureSource = $syntheticApp.Clone()
    $insecureSource.holidaySourceUrl = 'http://www.gov.uk/bank-holidays.json'
    Expect-Failure { Test-Application $insecureSource } 'A non-https holiday source URL was accepted.'
    $badDatabase = $syntheticApp.Clone()
    $badDatabase.databaseName = 'bad name;drop'
    Expect-Failure { Test-Application $badDatabase } 'An unsafe database name was accepted.'

    $badSqlAdmin = Copy-TestValues $syntheticFoundation
    $badSqlAdmin.sqlAdminObjectId = [guid]::Empty.ToString()
    Expect-Failure { Test-Foundation $badSqlAdmin } 'An empty SQL administrator object ID was accepted.'
    $badSqlVcores = Copy-TestValues $syntheticFoundation
    $badSqlVcores.sqlMinVcores = '4'
    Expect-Failure { Test-Foundation $badSqlVcores } 'Minimum vCores above maximum were accepted.'
    $badLtr = Copy-TestValues $syntheticFoundation
    $badLtr.sqlLongTermRetention.weeklyRetention = '7 days'
    Expect-Failure { Test-Foundation $badLtr } 'A non-ISO 8601 LTR duration was accepted.'
    foreach ($entry in $templates.GetEnumerator()) {
        $template = $entry.Value
        foreach ($name in $template.parameters.Keys) {
            $schema = $template.parameters[$name]
            if ($entry.Key -ceq 'main' -and $name -ceq 'monitoringAccessProfile') {
                Assert-Condition ($schema.defaultValue -ceq 'Private') 'Monitoring must default to Private.'
            } else {
                Assert-Condition (-not $schema.ContainsKey('defaultValue')) 'A parameter has an unapproved default.'
            }
        }
        if ($entry.Key -cin @('main', 'app')) {
            Assert-Condition ($template.ContainsKey('outputs')) 'Entry-point outputs must be preserved.'
        }
        if ($template.ContainsKey('outputs')) {
            foreach ($name in $template.outputs.Keys) {
                Assert-Condition ($name -notmatch '(?i)password|accesskey|connectionstring|secretvalue') 'A template exposes a credential-shaped output.'
            }
        }
    }

    $topLevelFoundationResources = if ($templates.main.resources -is [hashtable]) { @($templates.main.resources.Values) } else { @($templates.main.resources) }
    $vaultModule = @($topLevelFoundationResources | Where-Object {
        $_.type -eq 'Microsoft.Resources/deployments' -and
        $_.properties.parameters.ContainsKey('enableVaultForDeployment')
    })
    Assert-Condition ($vaultModule.Count -eq 1) 'Expected the foundation Key Vault module.'
    Assert-Condition (-not $vaultModule[0].properties.parameters.ContainsKey('keys')) 'Foundation must not ask the AVM module to create keys.'
    Assert-Condition ($vaultModule[0].properties.parameters.publicNetworkAccess.value -ceq 'Disabled') 'Key Vault public access was enabled.'
    Assert-Condition ($vaultModule[0].properties.parameters.networkAcls.value.bypass -ceq 'None') 'Key Vault trusted-service bypass was enabled.'

    $registryModule = @($topLevelFoundationResources | Where-Object {
        $_.type -eq 'Microsoft.Resources/deployments' -and $_.properties.parameters.ContainsKey('acrSku')
    })
    Assert-Condition ($registryModule.Count -eq 1) 'Expected the foundation ACR module.'
    $registryParameters = $registryModule[0].properties.parameters
    Assert-Condition ($registryParameters.publicNetworkAccess.value -ceq 'Disabled') 'ACR public access was enabled.'
    Assert-Condition ($registryParameters.networkRuleSetDefaultAction.value -ceq 'Deny') 'ACR network access was not deny-by-default.'
    Assert-Condition ($templates.main.parameters.acrSku.allowedValues.Count -eq 1 -and $templates.main.parameters.acrSku.allowedValues[0] -ceq 'Premium') 'The template must require explicit Premium approval for ACR Private Link.'
    $registryService = @($templates.main.variables.privateServices | Where-Object { $_.groupId -ceq 'registry' })
    Assert-Condition ($registryService.Count -eq 1 -and $registryService[0].zone -ceq 'privatelink.azurecr.io' -and $registryService[0].id -match 'Microsoft.ContainerRegistry/registries') 'ACR private endpoint and DNS configuration is missing.'

    $environmentModule = @($topLevelFoundationResources | Where-Object {
        $_.type -eq 'Microsoft.Resources/deployments' -and $_.properties.parameters.ContainsKey('infrastructureSubnetResourceId')
    })
    Assert-Condition ($environmentModule.Count -eq 1) 'Expected the foundation ACA environment module.'
    Assert-Condition ($environmentModule[0].properties.parameters.internal.value -eq $true) 'ACA must use an internal load balancer.'
    Assert-Condition ($environmentModule[0].properties.parameters.publicNetworkAccess.value -ceq 'Disabled') 'ACA environment public access was enabled.'
    $ingressRules = @($topLevelFoundationResources | Where-Object { $_.type -eq 'Microsoft.Network/networkSecurityGroups' } | ForEach-Object { $_.properties.securityRules } | Where-Object { $_.properties.direction -ceq 'Inbound' -and $_.properties.access -ceq 'Allow' })
    Assert-Condition (@($ingressRules | Where-Object { $_.properties.sourceAddressPrefix -ceq 'Internet' }).Count -eq 0) 'An Internet ingress NSG allow rule was retained.'
    Assert-Condition (@($ingressRules | Where-Object { $_.properties.sourceAddressPrefix -ceq 'VirtualNetwork' -and '443' -cin $_.properties.destinationPortRanges -and '31443' -cin $_.properties.destinationPortRanges }).Count -eq 1) 'Private HTTPS ingress ports are missing.'

    $dnsModule = @($topLevelFoundationResources | Where-Object {
        $_.type -eq 'Microsoft.Resources/deployments' -and $_.properties.parameters.ContainsKey('ipv4Address')
    })
    Assert-Condition ($dnsModule.Count -eq 1) 'Expected private DNS for the internal ACA environment.'
    Assert-Condition ($dnsModule[0].properties.parameters.dnsZoneName.value -match 'outputs.defaultDomain' -and $dnsModule[0].properties.parameters.ipv4Address.value -match 'outputs.staticIp') 'ACA DNS must use the environment domain and internal address.'
    $dnsResources = if ($dnsModule[0].properties.template.resources -is [hashtable]) { @($dnsModule[0].properties.template.resources.Values) } else { @($dnsModule[0].properties.template.resources) }
    $dnsRecords = @($dnsResources | Where-Object { $_.type -eq 'Microsoft.Network/privateDnsZones/A' })
    Assert-Condition ($dnsRecords.Count -eq 1 -and $dnsRecords[0].name.Contains("createArray('@', '*')") -and $dnsRecords[0].properties.aRecords[0].ipv4Address.Contains("parameters('ipv4Address')")) 'ACA private DNS must map apex and wildcard records to the internal address.'
    Assert-Condition (@($dnsResources | Where-Object { $_.type -eq 'Microsoft.Network/privateDnsZones/virtualNetworkLinks' }).Count -eq 1) 'ACA private DNS VNet link is missing.'

    $topLevelApplicationResources = if ($templates.app.resources -is [hashtable]) { @($templates.app.resources.Values) } else { @($templates.app.resources) }
    $applicationModule = @($topLevelApplicationResources | Where-Object {
        $_.type -eq 'Microsoft.Resources/deployments' -and $_.properties.parameters.ContainsKey('ingressExternal')
    })
    Assert-Condition ($applicationModule.Count -eq 1) 'Expected the application activation module.'
    Assert-Condition ($applicationModule[0].properties.parameters.ingressExternal.value -eq $true -and $applicationModule[0].properties.parameters.ingressAllowInsecure.value -eq $false) 'The internal ACA environment must expose HTTPS to private-network clients.'
    $appEnvText = $applicationModule[0].properties.parameters.containers | ConvertTo-Json -Depth 20
    foreach ($name in @('ConnectionStrings__HolidayContext', 'AzureAd__TenantId', 'AzureAd__ClientId', 'KeyVault__Uri', 'AZURE_CLIENT_ID', 'APPLICATIONINSIGHTS_CONNECTION_STRING')) {
        Assert-Condition ($appEnvText.Contains($name)) "Application setting is missing: $name."
    }
    Assert-Condition ($appEnvText -match 'Active Directory Managed Identity' -and $appEnvText -notmatch '(?i)password=|pwd=|Integrated Security|accountkey') 'The SQL connection string must use managed identity only.'
    Assert-Condition ($templates.app.parameters.holidaySourceUrl.ContainsKey('minLength')) 'holidaySourceUrl must be constrained.'

    $sqlModule = @($topLevelFoundationResources | Where-Object {
        $_.type -eq 'Microsoft.Resources/deployments' -and $_.properties.parameters.ContainsKey('administrators')
    })
    Assert-Condition ($sqlModule.Count -eq 1) 'Expected the foundation SQL server module.'
    $sqlParameters = $sqlModule[0].properties.parameters
    Assert-Condition ($sqlParameters.publicNetworkAccess.value -ceq 'Disabled') 'SQL public access was enabled.'
    Assert-Condition ($sqlParameters.minimalTlsVersion.value -ceq '1.2') 'SQL must require TLS 1.2.'
    Assert-Condition ($sqlParameters.administrators.value.azureADOnlyAuthentication -eq $true) 'SQL must be Entra-only (no SQL authentication).'
    $sqlService = @($templates.main.variables.privateServices | Where-Object { $_.groupId -ceq 'sqlServer' })
    Assert-Condition ($sqlService.Count -eq 1 -and $sqlService[0].id -match 'Microsoft.Sql/servers' -and $sqlService[0].zone -match 'privatelink') 'SQL private endpoint and DNS configuration is missing.'

    $monitoringPublicExpression = "[if(and(equals(parameters('monitoringAccessProfile'), 'Transition'), not(empty(parameters('monitoringTransitionApprovalReference')))), 'Enabled', 'Disabled')]"
    Assert-Condition ($templates.main.variables.monitoringPublicNetworkAccess -ceq $monitoringPublicExpression) 'Compiled ARM must enable public monitoring only for an explicitly referenced Transition; Private must resolve to Disabled.'
    $monitoringModules = @($topLevelFoundationResources | Where-Object {
        $_.type -eq 'Microsoft.Resources/deployments' -and $_.properties.parameters.ContainsKey('publicNetworkAccessForIngestion')
    })
    Assert-Condition ($monitoringModules.Count -eq 2) 'Expected workspace and Application Insights public-access controls.'
    $monitoringFlags = 0
    foreach ($module in $monitoringModules) {
        foreach ($name in @('publicNetworkAccessForIngestion', 'publicNetworkAccessForQuery')) {
            Assert-Condition ($module.properties.parameters[$name].value -ceq "[variables('monitoringPublicNetworkAccess')]") "Private profile does not control $name."
            $monitoringFlags++
        }
    }
    Assert-Condition ($monitoringFlags -eq 4) 'All four public-access settings must resolve to Disabled in Private.'
    $insightsModule = @($monitoringModules | Where-Object { $_.properties.parameters.ContainsKey('disableLocalAuth') })
    Assert-Condition ($insightsModule.Count -eq 1 -and $insightsModule[0].properties.parameters.disableLocalAuth.value -eq $false) 'Application Insights local authentication is expected to stay enabled until the application authenticates telemetry with Entra ID (see main.bicep); update this check when that changes.'

    $monitorScopeModule = @($topLevelFoundationResources | Where-Object {
        $_.type -eq 'Microsoft.Resources/deployments' -and $_.properties.parameters.ContainsKey('createScope')
    })
    Assert-Condition ($monitorScopeModule.Count -eq 1) 'Expected one AMPLS management module.'
    Assert-Condition ($monitorScopeModule[0].condition -ceq "[equals(parameters('monitoringAmpls').associationOwner, 'Project')]") 'Platform-owned AMPLS associations must not be written.'
    Assert-Condition ($monitorScopeModule[0].properties.parameters.createScope.value -ceq "[equals(parameters('monitoringAmpls').mode, 'Create')]") 'Existing AMPLS must not be redeclared as a managed scope.'
    Assert-Condition ($monitorScopeModule[0].subscriptionId -match "split\(variables\('monitorAmplsId'\)" -and $monitorScopeModule[0].resourceGroup -match "split\(variables\('monitorAmplsId'\)") 'Associations must target the AMPLS resource ID subscription/resource group.'
    Assert-Condition ('logs' -cin $monitorScopeModule[0].dependsOn -and 'insights' -cin $monitorScopeModule[0].dependsOn) 'AMPLS associations must wait for both monitoring resources.'
    $scopeResources = @(Get-TemplateResources $templates['modules\monitor-private-link-scope'])
    $managedScope = @($scopeResources | Where-Object { $_.type -ceq 'Microsoft.Insights/privateLinkScopes' })
    Assert-Condition ($managedScope.Count -eq 1 -and $managedScope[0].condition -ceq "[parameters('createScope')]") 'Shared AMPLS settings must not be modified in Existing mode.'
    Assert-Condition ($managedScope[0].properties.accessModeSettings.ingestionAccessMode -ceq 'PrivateOnly' -and $managedScope[0].properties.accessModeSettings.queryAccessMode -ceq 'PrivateOnly') 'New AMPLS access modes must both be PrivateOnly.'
    $associations = @($scopeResources | Where-Object { $_.type -ceq 'Microsoft.Insights/privateLinkScopes/scopedResources' })
    Assert-Condition ($associations.Count -eq 2) 'AMPLS must associate both the workspace and Application Insights.'
    $linkedIds = @($associations | ForEach-Object { $_.properties.linkedResourceId })
    Assert-Condition ("[parameters('workspaceResourceId')]" -cin $linkedIds -and "[parameters('applicationInsightsResourceId')]" -cin $linkedIds) 'AMPLS associations target incorrect resources.'
    foreach ($association in $associations) {
        Assert-Condition ($association.dependsOn.Count -eq 1 -and $association.dependsOn[0] -match 'Microsoft.Insights/privateLinkScopes') 'Associations must wait for new AMPLS creation.'
    }

    $monitorDnsModule = @($topLevelFoundationResources | Where-Object {
        $_.type -eq 'Microsoft.Resources/deployments' -and $_.properties.parameters.ContainsKey('createZone') -and $_.properties.parameters.createZone.value -eq $true
    })
    Assert-Condition ($monitorDnsModule.Count -eq 1 -and $monitorDnsModule[0].copy.count -ceq "[length(variables('monitorNewZones'))]") 'Expected one owned DNS zone creation loop.'
    Assert-Condition ($templates.main.variables.monitorZones -ceq "[if(equals(parameters('monitoringDns').mode, 'Managed'), parameters('monitoringDns').zones, createArray())]") 'PlatformManaged DNS must not deploy zone or link modules.'
    Assert-Condition ($templates.main.variables.monitorNewZones -match "^.filter\(" -and $templates.main.variables.monitorNewZones -match "\.mode, 'Create'" -and $templates.main.variables.monitorLinkedZones -match "\.linkMode, 'Create'") 'Reference-only DNS must not require deployments into platform-owned resource groups.'
    Assert-Condition ($monitorDnsModule[0].properties.parameters.createVnetLink.value -eq $false) 'Do not publish VNet links while DNS zones are empty.'
    $monitorLinkModule = @($topLevelFoundationResources | Where-Object {
        $_.type -eq 'Microsoft.Resources/deployments' -and $_.properties.parameters.ContainsKey('createVnetLink') -and $_.properties.parameters.createVnetLink.value -eq $true
    })
    Assert-Condition ($monitorLinkModule.Count -eq 1 -and $monitorLinkModule[0].copy.count -ceq "[length(variables('monitorLinkedZones'))]" -and $monitorLinkModule[0].properties.parameters.createZone.value -eq $false) 'Only explicitly owned VNet links may be published.'
    foreach ($dependency in @('network', 'monitorEndpoint', 'monitorDnsZones')) {
        Assert-Condition ($dependency -cin $monitorLinkModule[0].dependsOn) 'DNS links must wait for the VNet, endpoint DNS group and owned zones.'
    }
    $monitorDnsResources = @(Get-TemplateResources $templates['modules\monitor-private-dns-zone'])
    $monitorZones = @($monitorDnsResources | Where-Object { $_.type -ceq 'Microsoft.Network/privateDnsZones' })
    $monitorLinks = @($monitorDnsResources | Where-Object { $_.type -ceq 'Microsoft.Network/privateDnsZones/virtualNetworkLinks' })
    Assert-Condition ($monitorZones.Count -eq 1 -and $monitorZones[0].condition -ceq "[parameters('createZone')]") 'Existing private DNS zones must remain reference-only.'
    Assert-Condition ($monitorLinks.Count -eq 1 -and $monitorLinks[0].condition -ceq "[parameters('createVnetLink')]" -and $monitorLinks[0].properties.registrationEnabled -eq $false) 'Only explicitly owned VNet links may be created; registration must be disabled.'

    $monitorEndpointModule = @($topLevelFoundationResources | Where-Object {
        $_.type -eq 'Microsoft.Resources/deployments' -and $_.properties.parameters.ContainsKey('amplsResourceId')
    })
    Assert-Condition ($monitorEndpointModule.Count -eq 1 -and $monitorEndpointModule[0].condition -ceq "[equals(parameters('monitoringPrivateEndpoint').mode, 'Create')]") 'Existing Monitor endpoints must not be overwritten.'
    Assert-Condition ($monitorEndpointModule[0].properties.parameters.subnetResourceId.value -match '/subnets/private-endpoints') 'Monitor must reuse the private-endpoint subnet.'
    foreach ($dependency in @('network', 'monitorScope', 'monitorDnsZones')) {
        Assert-Condition ($dependency -cin $monitorEndpointModule[0].dependsOn) "Monitor endpoint is missing dependency: $dependency."
    }
    $monitorEndpointResources = @(Get-TemplateResources $templates['modules\monitor-private-endpoint'])
    $monitorEndpoints = @($monitorEndpointResources | Where-Object { $_.type -ceq 'Microsoft.Network/privateEndpoints' })
    $monitorGroups = @($monitorEndpointResources | Where-Object { $_.type -ceq 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups' })
    Assert-Condition ($monitorEndpoints.Count -eq 1 -and $monitorGroups.Count -eq 1) 'Monitor requires one endpoint and one DNS zone group, not one endpoint per zone.'
    $connection = $monitorEndpoints[0].properties.privateLinkServiceConnections[0].properties
    Assert-Condition ($connection.privateLinkServiceId -ceq "[parameters('amplsResourceId')]" -and $connection.groupIds.Count -eq 1 -and $connection.groupIds[0] -ceq 'azuremonitor') 'Incorrect AMPLS endpoint target/group.'
    Assert-Condition (-not $monitorEndpoints[0].properties.ContainsKey('ipConfigurations')) 'Monitor IP addresses must not be hardcoded.'
    $zoneSchema = $templates['modules\monitor-private-endpoint'].parameters.dnsZoneResourceIds
    Assert-Condition ($zoneSchema.minLength -eq 5 -and $zoneSchema.maxLength -eq 5) 'Monitor endpoint requires all five DNS zones.'
    $zoneCopy = $monitorGroups[0].properties.copy[0]
    Assert-Condition ($zoneCopy.name -ceq 'privateDnsZoneConfigs' -and $zoneCopy.count -ceq "[length(parameters('dnsZoneResourceIds'))]" -and $zoneCopy.input.properties.privateDnsZoneId -match "parameters\('dnsZoneResourceIds'\)") 'The single Monitor DNS group must reference every supplied zone.'

    $foundation = $null
    if ($FoundationParameters) {
        $foundation = Read-ParameterValues $FoundationParameters $templates.main
        Test-Foundation $foundation
        Write-Output 'Foundation parameter structure and local constraints passed.'
        if ($foundation.monitoringAccessProfile -ceq 'Transition') {
            Write-Warning 'Transition retains public monitoring. Confirm the referenced approval and complete private-path validation before switching to Private.'
        }
    }
    if ($ApplicationParameters) {
        $application = Read-ParameterValues $ApplicationParameters $templates.app
        Test-Application $application
        if ($foundation) {
            foreach ($name in @('location', 'prefix', 'environmentName')) {
                Assert-Condition ($foundation[$name] -ceq $application[$name]) "Foundation and application disagree on $name."
            }
        }
        Write-Output 'Application parameter structure and local constraints passed.'
    }
    Write-Output 'All seven Bicep files compile; Create/Existing AMPLS, project/platform/mixed DNS, endpoint reuse, negative ownership/ID/capacity cases, all four Private public-access flags, ARM dependency contracts and existing ACA/ACR/Key Vault/SQL regressions passed.'
    Write-Output 'Only the approved Private profile default is permitted. No credential-shaped outputs or Azure operations. Live free IPs, association ownership, endpoint approval, DNS and ingestion/query reachability still require owner verification.'
}
finally {
    foreach ($file in $temporaryFiles) {
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force }
    }
}
