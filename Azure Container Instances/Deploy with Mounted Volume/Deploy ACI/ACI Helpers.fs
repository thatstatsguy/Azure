namespace DeployACI

module ACIHelpers =
    open System.IO
    open Azure
    open Azure.Storage.Files.Shares
    open Microsoft.Azure.Management.ContainerInstance.Fluent.Models
    open Microsoft.Azure.Management.Fluent
    open Microsoft.Azure.Management.ResourceManager.Fluent
    open Microsoft.Azure.Management.ResourceManager.Fluent.Core
    open Microsoft.Rest
    
    // TODO move this into Azure key vault when using this for production
    let storageAccountConnectionString = "<Storage Account Connection String>"  
    //Can also just be extracted straight out of connection string
    let storageKey = "<Storage Account Key>"
    let fileShareName = "<File Share Name>"  
    let storageAccountName = "<Storage Account Name>"
    
    let subscriptionID = "<Subscription ID>"
    
    let uploadFileToFileShare (fileName : string) (filePath : string) (inMemoryText : string option) =
        let defaultText = inMemoryText |> Option.defaultValue ""
        printfn $"Sending file {fileName} ....{defaultText}"
        let share = ShareClient(storageAccountConnectionString, fileShareName)
        printfn "Got file share client"
        let directory = share.GetDirectoryClient("")  

        let file = directory.GetFileClient(fileName)

        
        //may need this if we store things in separate folders
//        (*try
//            directory.Create() |> ignore
//        with
//        //will throw exception if directory is already there
//        | _ -> ()*)
        
        use ms = 
            match inMemoryText with
            | Some s ->
                new MemoryStream(System.Text.Encoding.ASCII.GetBytes(s)) :> Stream
            | _ ->
                File.OpenRead(filePath) :> Stream
        
        //create file before uploading anything
        file.Create(ms.Length) |> ignore
        
        //Note - this overwrites previous file   
        file.UploadRange(  
                HttpRange(0, ms.Length),  
                ms)
            |> ignore
        
    let buildACIAndGiveDeleteMechanism() = 
 
        
        printfn "Getting Credentials"
        let credentials = SdkContext.AzureCredentialsFactory.FromFile("C:/<Path To File>/my.azureauth");
        printfn "Got Credentials"
        let azure =
            Azure
                .Configure()
                .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                .Authenticate(credentials)
                .WithSubscription(subscriptionID)
                //if you'd like to use the subscription specified in the auth file
                //.WithDefaultSubscription();

        printfn "Got azure subscription"
        
        let rgName = "<Resource Group Name>"
        //generate random names
        let aciName = SdkContext.RandomResourceName("ACINAME", 15)
        let containerImageName = "<DockerHub Image Name>"
        //name of the volume mount- purely used for reference when defining file share mount and using it later in the code
        let volumeMountName = "testfilesharemount"
        let region = Region.SouthAfricaNorth
        azure
            .ContainerGroups
            .Define(aciName)
            .WithRegion(region)
            .WithExistingResourceGroup(rgName)
            .WithLinux()
            .WithPublicImageRegistryOnly()
            // Use existing file share or create a new file share on the fly
            // WithNewAzureFileShareVolume(volumeMountName, shareName)
            .DefineVolume(volumeMountName)
                .WithExistingReadWriteAzureFileShare(fileShareName)
                .WithStorageAccountName(storageAccountName)
                .WithStorageAccountKey(storageKey)
                .Attach()
            .DefineContainerInstance(aciName)
                .WithImage(containerImageName)
                .WithExternalTcpPort(80)                
                //where it will be mounted to on the container
                .WithVolumeMountSetting(volumeMountName, "/myspecialfileshare")
                .WithStartingCommandLine("/bin/bash")
                .WithStartingCommandLine("-c")
                .WithStartingCommandLine("cd myspecialfileshare; python3 MyScript.py")
                .Attach()
            //default behaviour is to keep restarting so set it to never
            .WithRestartPolicy(ContainerGroupRestartPolicy.Never)
            //if you pass in null it get's a randomly assigned ip - alternatively you can give it a custom dns here
            .WithDnsPrefix(null)
            .Create()
            |> ignore
            
        printfn "Created Container"
        fun () -> azure.ContainerGroups.DeleteByResourceGroup(rgName,aciName)