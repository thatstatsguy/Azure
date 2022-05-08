#Requirement: Global Azure admin rights
#Original article - https://aztoso.com/security/microsoft-graph-permissions-managed-identity/
#Open the Microsoft cloud shell environment

Connect-AzureAD
# Microsoft Graph App ID (DON'T CHANGE)
#https://www.shawntabrizi.com/aad/common-microsoft-resources-azure-active-directory/
$GraphAppId = "00000003-0000-0000-c000-000000000000"
# Name of the manage identity (same as the Logic App name)
$DisplayNameOfMSI="<FunctionAppName>" 
# Check the Microsoft Graph documentation for the permission you need for the operation
$PermissionName = "Sites.Selected" 
$MSI = (Get-AzureADServicePrincipal -Filter "displayName eq '$DisplayNameOfMSI'")
Start-Sleep -Seconds 10
$GraphServicePrincipal = Get-AzureADServicePrincipal -Filter "appId eq '$GraphAppId'"
$AppRole = $GraphServicePrincipal.AppRoles | `Where-Object {$_.Value -eq $PermissionName -and $_.AllowedMemberTypes -contains "Application"}
New-AzureAdServiceAppRoleAssignment -ObjectId $MSI.ObjectId -PrincipalId $MSI.ObjectId ` -ResourceId $GraphServicePrincipal.ObjectId -Id $AppRole.Id

#After this is completed, to check if it worked:
#look in azure portal under Azure AD > Enterprise Apps > Filter on Application Type = ManagedIdentity > Apply Filter > Select Identity > Permissions