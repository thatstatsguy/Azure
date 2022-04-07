open DeployACI.ACIHelpers

//upload some files to file share
//TODO use options for non needed file path
uploadFileToFileShare "ClientScript.py" "<Full Path to MyScript.py>" None
uploadFileToFileShare "TestFile.txt" "<Full Path to TestFile.txt>" None
uploadFileToFileShare "TestFile2.txt" "" (Some "Text to insert into TestFile2.txt")

//deploy ACI on azure with python script which will use these files
//function returns a function which can be used to delete the container when you're ready
let deleteMechanism = buildACIAndGiveDeleteMechanism()

// Some
// other 
// code

//finally delete the container when you're done
deleteMechanism()