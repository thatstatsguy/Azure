# Write Resource Group Billing Information to Blob Storage

**Summary of what this code does**
1. Azure function which queries the Azure Cost Management API,
2. Reads in a template XLSX file from blob storage
3. Fills it in with the appropriate information
4. Writes it back out to blob storage
5. Since we're using MSI it does this without ever needing to maintain passwords in code, which is a plus for security.

**Additional notes**
- This code requires knowledge on what MSI (Managed Service Identity) in Azure is and how to configure it on a function app. See notes below if you need more information.
- This code assumes that a resource group, blob storage (with pre-existing container) has been made. It also assumes that the template file exists on the blob storage.
- In order for your function to access the key vault, you will need to change the access policy of the key vault to allow your managed identity to access secrets. For more information on the error you'll get if you don't do this  (and how to fix it), see [this link](https://docs.microsoft.com/en-us/answers/questions/714847/error-the-user-group-or-application-does-not-have.html).

**Configuring a Function App for MSI**
- For more info on MSI's and how to enable it for your function app, see [here](https://docs.microsoft.com/en-us/azure/app-service/overview-managed-identity?tabs=portal%2Chttp).
- To give your MSI for the function app access to your resource group you'll need to give access at a resource group level.
- To do this, navigate to the resource group in the portal.
- Under Access Control (IAM) you should see roles.
- Make sure you have access to add a role, and select to add a new role.
- For this project I just set it to Contributor access. In production you should fine tune this access and follow the minimum required access approach (i.e. only give the Function app what it needs to do it's job and nothing else).

**TODO**
- The scope for the request (subscription ID/Resource Group name) can be passed in using the Function request body if needed. This would make it a lot more flexible.