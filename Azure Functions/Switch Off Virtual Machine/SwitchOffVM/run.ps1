using namespace System.Net

param($Request, $TriggerMetadata)

#Notes:
# We can use Azure AD with RBAC to control this dynamically and allow/disallow users
# No need to load Azure Powershell module
# this is done already in function host using Managed Dependency = True
# This can also be specified in the requirements file

$tenantId = "<Tenant ID>"
$subscriptionId = "<Subscription ID>"
#Resource Group where VM is contained
$rgName = "<Resource Group Name>"
#Actual VM name
$vmName = "<VM Name>"

# First navigate to the subscription when want to perform an action in
Select-AzSubscription -SubscriptionID $subscriptionId -TenantID $tenantId

# Stop VM
Stop-AzVM -ResourceGroupName $rgName -Name $vmName

#If needed you can restart the VM again
#Start-AzVM -ResourceGroupName $rsgName -Name $vmName

#If needed you can also run commands on the VM after start up
#https://docs.microsoft.com/en-us/powershell/module/az.compute/invoke-azvmruncommand?view=azps-7.5.0
#Invoke-AzVMRunCommand
