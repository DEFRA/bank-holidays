targetScope = 'resourceGroup'

@minLength(1)
param location string

@minLength(2)
@maxLength(6)
param prefix string

@minLength(2)
@maxLength(3)
param environmentName string

param tags object

@description('Repository inside the foundation ACR, without a registry hostname or tag.')
@minLength(1)
param imageRepository string

@description('Immutable registry digest sha256:<64 lowercase hexadecimal characters>, not a local image ID.')
@minLength(71)
@maxLength(71)
param imageDigest string

@description('The Bank Holidays API\'s own Entra app registration (see infra/entra/New-BankHolidaysEntraRegistration.ps1); the audience every write-endpoint token must target.')
@minLength(36)
@maxLength(36)
param entraTenantId string

@minLength(36)
@maxLength(36)
param entraClientId string

@description('Must match the databaseName supplied to main.bicep.')
@minLength(1)
@maxLength(128)
param databaseName string

@description('Upstream bank-holiday feed read by POST /Home/Update and Home/ManualRefresh (BankHolidays:SourceUrl). Must be reachable from the ACA subnet over HTTPS.')
@minLength(1)
param holidaySourceUrl string

@minValue(1)
@maxValue(300)
param holidaySourceTimeoutSeconds int

@description('Explicit supported CPU:memory pair for the Consumption profile.')
@allowed([
  '0.25:0.5Gi'
  '0.5:1Gi'
  '0.75:1.5Gi'
  '1:2Gi'
  '1.25:2.5Gi'
  '1.5:3Gi'
  '1.75:3.5Gi'
  '2:4Gi'
  '2.25:4.5Gi'
  '2.5:5Gi'
  '2.75:5.5Gi'
  '3:6Gi'
  '3.25:6.5Gi'
  '3.5:7Gi'
  '3.75:7.5Gi'
  '4:8Gi'
])
param containerSize string

@minValue(0)
@maxValue(300)
param minReplicas int

@description('The refresh guard and the anonymous ManualRefresh antiforgery keys are per-instance (no shared Data Protection key ring), so 1 is the verified setting; see infra/README.md before raising it.')
@minValue(1)
@maxValue(300)
param maxReplicas int

@minValue(1)
@maxValue(10000)
param concurrentRequests int

@minValue(1)
@maxValue(600)
param terminationGracePeriodSeconds int

var stem = '${prefix}-${environmentName}'
var globalStem = '${prefix}${environmentName}${uniqueString(resourceGroup().id, prefix, environmentName)}'
var hostName = '${stem}-app.${managedEnvironment.properties.defaultDomain}'
var size = split(containerSize, ':')

resource managedEnvironment 'Microsoft.App/managedEnvironments@2026-01-01' existing = {
  name: '${stem}-env'
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' existing = {
  name: '${stem}-id'
}

resource registry 'Microsoft.ContainerRegistry/registries@2025-04-01' existing = {
  name: 'acr${globalStem}'
}

resource vault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: 'kv${globalStem}'
}

resource sql 'Microsoft.Sql/servers@2025-01-01' existing = {
  name: 'sql${globalStem}'
}

resource insights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: '${stem}-insights'
}

resource logs 'Microsoft.OperationalInsights/workspaces@2025-07-01' existing = {
  name: '${stem}-logs'
}

var appEnv = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
  { name: 'ASPNETCORE_HTTP_PORTS', value: '8080' }
  { name: 'AllowedHosts', value: hostName }
  { name: 'AzureAd__Instance', value: az.environment().authentication.loginEndpoint }
  { name: 'AzureAd__TenantId', value: entraTenantId }
  { name: 'AzureAd__ClientId', value: entraClientId }
  // Managed-identity SQL access; the identity is added as a contained database user by infra/Grant-DatabaseAccess.sql.
  {
    name: 'ConnectionStrings__HolidayContext'
    value: 'Server=tcp:${sql.properties.fullyQualifiedDomainName},1433;Database=${databaseName};Authentication=Active Directory Managed Identity;User Id=${identity.properties.clientId};Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=True;'
  }
  { name: 'ApplyMigrationsOnStartup', value: 'false' }
  { name: 'BankHolidays__SourceUrl', value: holidaySourceUrl }
  { name: 'BankHolidays__SourceTimeoutSeconds', value: string(holidaySourceTimeoutSeconds) }
  { name: 'KeyVault__Uri', value: vault.properties.vaultUri }
  // DefaultAzureCredential (Key Vault configuration provider) selects the user-assigned identity from this variable.
  { name: 'AZURE_CLIENT_ID', value: identity.properties.clientId }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: insights.properties.ConnectionString }
  { name: 'OTEL_SERVICE_NAME', value: 'bank-holidays' }
]

module application 'br/public:avm/res/app/container-app:0.23.0' = {
  name: '${stem}-activation'
  params: {
    name: '${stem}-app'
    location: location
    tags: tags
    enableTelemetry: false
    environmentResourceId: managedEnvironment.id
    workloadProfileName: 'Consumption'
    managedIdentities: {
      userAssignedResourceIds: [identity.id]
    }
    activeRevisionsMode: 'Single'
    // External to the app environment, but private to the internal environment's network.
    ingressExternal: true
    ingressAllowInsecure: false
    ingressTargetPort: 8080
    ingressTransport: 'http'
    terminationGracePeriodSeconds: terminationGracePeriodSeconds
    registries: [
      {
        server: registry.properties.loginServer
        identity: identity.id
      }
    ]
    containers: [
      {
        name: 'api'
        image: '${registry.properties.loginServer}/${imageRepository}@${imageDigest}'
        resources: {
          cpu: json(size[0])
          memory: size[1]
        }
        env: appEnv
        probes: [
          {
            type: 'Liveness'
            httpGet: {
              path: '/health/live'
              port: 8080
              scheme: 'HTTP'
              httpHeaders: [{ name: 'Host', value: hostName }]
            }
            initialDelaySeconds: 10
            periodSeconds: 10
            timeoutSeconds: 3
            failureThreshold: 3
          }
          {
            type: 'Readiness'
            httpGet: {
              path: '/health/ready'
              port: 8080
              scheme: 'HTTP'
              httpHeaders: [{ name: 'Host', value: hostName }]
            }
            initialDelaySeconds: 10
            periodSeconds: 10
            timeoutSeconds: 5
            failureThreshold: 3
          }
        ]
      }
    ]
    scaleSettings: {
      minReplicas: minReplicas
      maxReplicas: maxReplicas
      rules: [
        {
          name: 'http'
          http: {
            metadata: {
              concurrentRequests: string(concurrentRequests)
            }
          }
        }
      ]
    }
    diagnosticSettings: [
      {
        name: 'platform'
        workspaceResourceId: logs.id
      }
    ]
  }
}

output applicationResourceId string = application.outputs.resourceId
output applicationOrigin string = 'https://${application.outputs.fqdn}'
