<#
.SYNOPSIS
Creates the Microsoft Entra app registration for the Defra Bank Holidays API: single-tenant API (no sign-in
redirect URIs - the protected write endpoints are called service-to-service or by operators with a token), an
Application ID URI, and the BankHolidays.Refresh app role. Safe by default: it only prints what it would do
unless -Apply is given.

.DESCRIPTION
Run by the identity team, signed in to Azure CLI (az login) with permission to create app registrations in
the approved tenant. The role value must stay BankHolidays.Refresh: it is the constant
AuthorizationPolicies.RefreshRole in the application (Settings/AuthorizationPolicies.cs).

The role allows Application members (the scheduled refresh job's managed identity) and User members
(operators or operator groups assigned in the portal). Assign the scheduled job with Grant-BankHolidaysRole.ps1.

.EXAMPLE
./infra/entra/New-BankHolidaysEntraRegistration.ps1 -DisplayName 'Defra Bank Holidays (UAT)'
Shows the plan.

.EXAMPLE
./infra/entra/New-BankHolidaysEntraRegistration.ps1 -DisplayName 'Defra Bank Holidays (UAT)' -Apply
Creates the registration and its service principal.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateLength(1, 120)]
    [string] $DisplayName,

    [switch] $Apply
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$appRoles = @(
    [ordered]@{
        id = [guid]::NewGuid().ToString()
        allowedMemberTypes = @('Application', 'User')
        displayName = 'Bank Holidays Refresh'
        description = 'Replace the stored bank holiday data (Home/Update, Home/Refresh).'
        value = 'BankHolidays.Refresh'
        isEnabled = $true
    }
)

Write-Output "Display name        : $DisplayName"
Write-Output 'Sign-in audience    : AzureADMyOrg (single tenant)'
Write-Output 'Redirect URI        : none'
Write-Output 'App roles           : BankHolidays.Refresh (allowedMemberTypes: Application, User)'
Write-Output 'Client secret       : none created (token validation needs no secret of its own)'
Write-Output 'Implicit grant      : disabled'

if (-not $Apply) {
    Write-Output ''
    Write-Output 'Plan only. Re-run with -Apply to create the registration.'
    return
}

$existing = az ad app list --display-name $DisplayName --query '[].appId' -o tsv
if ($LASTEXITCODE -ne 0) { throw 'az ad app list failed. Sign in with az login first.' }
if ($existing) { throw "An app registration named '$DisplayName' already exists ($existing). Choose another name or update it in the portal." }

$rolesFile = Join-Path ([IO.Path]::GetTempPath()) "bank-holidays-roles-$([guid]::NewGuid().ToString('N')).json"

try {
    $appRoles | ConvertTo-Json -Depth 5 -AsArray | Set-Content -LiteralPath $rolesFile -Encoding utf8
    $clientId = az ad app create `
        --display-name $DisplayName `
        --sign-in-audience 'AzureADMyOrg' `
        --enable-id-token-issuance false `
        --enable-access-token-issuance false `
        --app-roles "@$rolesFile" `
        --query appId -o tsv
    if ($LASTEXITCODE -ne 0 -or -not $clientId) { throw 'The app registration was not created.' }

    az ad app update --id $clientId --identifier-uris "api://$clientId" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "The registration $clientId was created but its identifier URI was not set. Set it in the portal." }

    az ad sp create --id $clientId --query id -o tsv | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "The registration $clientId was created but its service principal was not. Create it in the portal." }

    $tenantId = az account show --query tenantId -o tsv

    Write-Output ''
    Write-Output "Created. AzureAd__TenantId = $tenantId (entraTenantId)"
    Write-Output "         AzureAd__ClientId = $clientId (entraClientId)"
    Write-Output "         Application ID URI = api://$clientId"
    Write-Output ''
    Write-Output 'Next: run Grant-BankHolidaysRole.ps1 for the scheduled refresh job''s managed identity, and assign any'
    Write-Output 'operator users/groups to the BankHolidays.Refresh role in the portal (Enterprise applications).'
}
finally {
    if (Test-Path -LiteralPath $rolesFile) { Remove-Item -LiteralPath $rolesFile -Force }
}
