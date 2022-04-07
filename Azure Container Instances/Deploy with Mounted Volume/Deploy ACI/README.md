# Deploy ACI on Azure with Mounted Fileshare

**Summary**
- Allows users to automagically deploy containers to the cloud.
- Uses public registry containers (note that depending on the image you use it may take a while for azure to pull it over).
- mounts a given file share to the contaienr.

**Notes**
- To generate the auth credentials, see [this link](https://github.com/Azure/azure-libraries-for-net/blob/master/AUTH.md) 
- In addition to creating the auth file, the Azure.ContainerInstance provider may need to be [registered](https://docs.microsoft.com/en-us/azure/azure-resource-manager/troubleshooting/error-register-resource-provider?tabs=azure-cli).
  - This can be achieved by running the following in Azure CLI/Cloud Shell
  ```
  az account set --subscription <ID or name>
  az provider register --namespace Microsoft.ContainerInstance
  ```
  
- If the provider is not registered, you may get errors, such as the one below which is completely unhelpful in diagnosing your problem.
    ```
    Microsoft.Rest.Azure.CloudException: The client '<clientID>' with object id '<objectID>' does not have authorization to perform action 'Microsoft.ContainerInstance/register/action' over scope '/subscriptions/<subscriptionID>' or the scope is invalid. If access was recently granted, please refresh your credentials.
  ```
  
        
**TODO**
- Try using private registry image from ACR
- Deploy within Vnet

**DEBUG helpers**
If you have issues with this code, try using the following to help debug the issue

 1. To debug container creation, debug with
    ```
    az container create
        --name helloworld
        --image  <Dockerhub Image>
        --ip-address public \
        -g <Resource Group Name>
        --azure-file-volume-account-key <Storage Account Key>
        --azure-file-volume-account-name <Storage Account Name>
        --azure-file-volume-share-name <FileShareName>
        --azure-file-volume-mount-path <Fileshare mount path on container e.g./specialfileshare>
    ```
 2. To test connectivity to an external point from within container
    - In powershell ```Test-NetConnection 102.37.144.148 -Port 5000```
     In Linux
        ```
        apt-get update
        apt-get install telnet
        telnet <IP> <PORT>
        ```
3. If you want to understand to understand what's happening with your container logs you can attach to the live outputs in Azure CLI
    ```
    az account set --subscription <Subscription ID containing <RG>
    az container attach --resource-group <RG> --name <ContainerName>
    ```