<#
.SYNOPSIS
Assigns the scheduled refresh job's own Entra ID service identity (managed identity, workload-identity-federated
app registration, or certificate-backed app registration) to the Bank Holidays API BankHolidays.Refresh app role.
Safe by default: it only prints what it would do unless -Apply is given.

.DESCRIPTION
Run by the identity team, signed in to Azure CLI (az login) with Application.ReadWrite.All (or equivalent)
Microsoft Graph permission. Application-type role assignments are not exposed by `az ad app permission`, so this
script calls the Microsoft Graph appRoleAssignments API directly via `az rest`. Operators (users/groups) are
assigned in the portal instead.

.PARAMETER ConsumerServicePrincipalObjectId
The OBJECT ID (not client/application ID) of the job's service principal, for example:
  az identity show --name <uami-name> --resource-group <rg> --query principalId -o tsv

.EXAMPLE
./infra/entra/Grant-BankHolidaysRole.ps1 -BankHolidaysClientId '<app-id>' -ConsumerServicePrincipalObjectId '<sp-object-id>' -Apply
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F-]{36}$')]
    [string] $BankHolidaysClientId,

    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F-]{36}$')]
    [string] $ConsumerServicePrincipalObjectId,

    [switch] $Apply
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$role = 'BankHolidays.Refresh'

$apiSpId = az ad sp show --id $BankHolidaysClientId --query id -o tsv
if ($LASTEXITCODE -ne 0 -or -not $apiSpId) { throw "No service principal found for app '$BankHolidaysClientId'. Create it with New-BankHolidaysEntraRegistration.ps1 first." }

$appRoleId = az ad sp show --id $apiSpId --query "appRoles[?value=='$role'].id | [0]" -o tsv
if ($LASTEXITCODE -ne 0 -or -not $appRoleId -or $appRoleId -eq 'null') { throw "App role '$role' was not found on the Bank Holidays registration." }

$consumer = az ad sp show --id $ConsumerServicePrincipalObjectId --query '{displayName:displayName, appId:appId}' -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or -not $consumer) { throw "No service principal found with object ID '$ConsumerServicePrincipalObjectId'." }

Write-Output "Bank Holidays app   : $BankHolidaysClientId (service principal $apiSpId)"
Write-Output "Consumer            : $($consumer.displayName) (appId $($consumer.appId), object ID $ConsumerServicePrincipalObjectId)"
Write-Output "Role to grant       : $role (app role ID $appRoleId)"

if (-not $Apply) {
    Write-Output ''
    Write-Output 'Plan only. Re-run with -Apply to grant the role.'
    return
}

$existingAssignments = az rest --method GET `
    --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$ConsumerServicePrincipalObjectId/appRoleAssignments" `
    --query "value[?resourceId=='$apiSpId' && appRoleId=='$appRoleId']" -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Failed to check existing app-role assignments via Microsoft Graph.' }
if ($existingAssignments.Count -gt 0) {
    Write-Output ''
    Write-Output "Already granted - no change made (assignment id $($existingAssignments[0].id))."
    return
}

$bodyFile = Join-Path ([IO.Path]::GetTempPath()) "bank-holidays-grant-$([guid]::NewGuid().ToString('N')).json"
try {
    [ordered]@{
        principalId = $ConsumerServicePrincipalObjectId
        resourceId = $apiSpId
        appRoleId = $appRoleId
    } | ConvertTo-Json | Set-Content -LiteralPath $bodyFile -Encoding utf8

    az rest --method POST `
        --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$ConsumerServicePrincipalObjectId/appRoleAssignments" `
        --body "@$bodyFile" --headers 'Content-Type=application/json' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The Microsoft Graph app-role assignment request failed.' }

    Write-Output ''
    Write-Output "Granted $role to $($consumer.displayName)."
}
finally {
    if (Test-Path -LiteralPath $bodyFile) { Remove-Item -LiteralPath $bodyFile -Force }
}
