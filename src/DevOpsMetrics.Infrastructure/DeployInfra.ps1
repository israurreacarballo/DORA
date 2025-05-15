#Install-Module -Name Az -Scope CurrentUser -Repository PSGallery -Force
# Tenant 8550a8f5-e879-4faf-ba1d-0597b89bc5bf


Connect-AzAccount -Tenant "8550a8f5-e879-4faf-ba1d-0597b89bc5bf" -UseDeviceAuthentication
Set-AzContext -SubscriptionId "8ebc8ccd-500a-4873-871e-12eb082ec4e2" 

$resourceGroupName="devopsmetrics"
$resourceLocation="westeurope"
$keyVaultName="devops-shiu-vault"
$storageName="devopsshiustorage"
$hostingName="devops-shiu-hosting"
$appInsightsName="devops-shiu-appinsights"
$serviceName="devops-shiu-service"
$websiteName="devops-shiu-web"
$functionName="devops-shiu-function"
$administrationEmailAccount="isra_u_hotmail.com#EXT#@TBSNG15gmail.onmicrosoft.com"
$fileRoot = "C:\Users\isra_\OneDrive\DEVOPS\DevOpsMetrics\src"
$templatesLocation="$fileRoot\DevOpsMetrics.Infrastructure\Templates"
$error.clear()


New-AzResourceGroup -Name $resourceGroupName -Location $resourceLocation


# Key Vault Deployment
$user = Get-AzADUser -UserPrincipalName $administrationEmailAccount
$administratorUserPrincipalId = $user.Id

New-AzResourceGroupDeployment -ResourceGroupName $resourceGroupName `
    -Name $keyVaultName `
    -TemplateFile "$templatesLocation\KeyVault.json" `
    -keyVaultName $keyVaultName `
    -administratorUserPrincipalId $administratorUserPrincipalId

# Storage Account Deployment
$storageDeployment = New-AzResourceGroupDeployment -resourceGroupNamefortemplate $resourceGroupName ``
    -Name $storageName `
    -TemplateFile "$templatesLocation\Storage.json" `
    -storageAccountName $storageName 
    -ResourceGroupName $resourceGroupName 

$storageAccountConnectionString = $storageDeployment.Outputs.storageAccountConnectionString.Value

Set-AzKeyVaultSecret -VaultName $keyVaultName `
    -Name "AppSettings--AzureStorageAccountConfigurationString" `
    -SecretValue (ConvertTo-SecureString $storageAccountConnectionString -AsPlainText -Force)

# ☁️ Hosting Plan Deployment
New-AzResourceGroupDeployment -ResourceGroupName $resourceGroupName `
    -Name $hostingName `
    -TemplateFile "$templatesLocation\WebHosting.json" `
    -hostingPlanName $hostingName `
    -actionGroupName $actionGroupName

# 📊 Application Insights Deployment
$appInsightsDeployment = New-AzResourceGroupDeployment -ResourceGroupName $resourceGroupName `
    -Name $appInsightsName `
    -TemplateFile "$templatesLocation\ApplicationInsights.json" `
    -applicationInsightsName $appInsightsName

$applicationInsightsInstrumentationKey = $appInsightsDeployment.Outputs.applicationInsightsInstrumentationKeyOutput.Value

# 🌐 Web Service Deployment
New-AzResourceGroupDeployment -ResourceGroupName $resourceGroupName `
    -Name $serviceName `
    -TemplateFile "$templatesLocation\Website.json" `
    -webSiteName $serviceName `
    -hostingPlanName $hostingName

# Empaquetado e implementación
dotnet publish "$fileRoot\DevOpsMetrics.Service\DevOpsMetrics.Service.csproj" `
    --configuration Debug `
    --output "$fileRoot\DevOpsMetrics.Service\bin\webservice"

Compress-Archive -Path "$fileRoot\DevOpsMetrics.Service\bin\webservice\*.*" `
    -DestinationPath "$fileRoot\DevOpsMetrics.Service\bin\webservice.zip" -Force

Publish-AzWebApp -ResourceGroupName $resourceGroupName `
    -Name $serviceName `
    -ArchivePath "$fileRoot\DevOpsMetrics.Service\bin\webservice.zip"

# 🔧 Configurar appsettings
Set-AzWebApp -ResourceGroupName $resourceGroupName `
    -Name $serviceName `
    -AppSettings @{
        "APPINSIGHTS_INSTRUMENTATIONKEY" = $applicationInsightsInstrumentationKey
        "AppSettings:KeyVaultURL" = "https://$keyVaultName.vault.azure.net/"
    }

#🖥️ Website Deployment
New-AzResourceGroupDeployment -ResourceGroupName $resourceGroupName `
    -Name $websiteName `
    -TemplateFile "$templatesLocation\Website.json" `
    -webSiteName $websiteName `
    -hostingPlanName $hostingName

Set-AzWebApp -ResourceGroupName $resourceGroupName `
    -Name $websiteName `
    -AppSettings @{
        "APPINSIGHTS_INSTRUMENTATIONKEY" = $applicationInsightsInstrumentationKey
    }

# ⚙️ Azure Function Deployment
New-AzResourceGroupDeployment -ResourceGroupName $resourceGroupName `
    -Name $functionName `
    -TemplateFile "$templatesLocation\function.json" `
    -webSiteName $functionName `
    -hostingPlanName $hostingName

Set-AzWebApp -ResourceGroupName $resourceGroupName `
    -Name $functionName `
    -AppSettings @{
        "APPINSIGHTS_INSTRUMENTATIONKEY" = $applicationInsightsInstrumentationKey
    }
