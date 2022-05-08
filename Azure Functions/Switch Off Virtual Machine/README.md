**Summary of what this code does**
1. Azure function which switches off a VM using MSI (Managed Service Identity).
2. Since we're using MSI it does this without ever needing to maintain passwords in code, which is a plus for security.

**Additional notes**
- This code requires knowledge on what MSI (Managed Service Identity) in Azure is and how to configure it on a function app. See notes below if you need more information.

**Configuring a Function App for MSI**
- For more info on MSI's and how to enable it for your function app, see [here](https://docs.microsoft.com/en-us/azure/app-service/overview-managed-identity?tabs=portal%2Chttp).
- To give your MSI for the function app access to your resource group you'll need to give access at a resource group level.
- To do this, navigate to the resource group in the portal.
- Under Access Control (IAM) you should see roles.
- Make sure you have access to add a role, and select to add a new role.
- For this project I just set it to Contributor access. In production you should fine tune this access and follow the minimum required access approach (i.e. only give the Function app what it needs to do it's job and nothing else).