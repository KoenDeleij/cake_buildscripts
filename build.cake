// #addin nuget:?package=Cake.Core&version=0.26.1
#addin nuget:?package=Cake.CoreCLR
#addin nuget:?package=Cake.Figlet
#addin nuget:?package=Newtonsoft.Json

#load "./models/BuildConfiguration.cake"

using Newtonsoft.Json;

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

BuildConfiguration configuration;

Task("Debug").Does(() => 
{
    FilePath filePaths = File("../../build.config");

    if (FileExists(filePaths.FullPath))
    {
        Information("File exists!");
        
        var configData = System.IO.File.ReadAllText(filePaths.FullPath, Encoding.UTF8);

        configuration = JsonConvert.DeserializeObject<BuildConfiguration>(configData);
        Information(Figlet(configuration.MainProjectName));
    }
    else
    {
        Information("Configuration file does not exists!");
        Information("Trying to fetch environment vars");
    }
});

Task("Clean")
    .Does(() =>
{
    CleanDirectories("./**/bin");
    CleanDirectories("./**/obj");
});

Task("NuGetRestore")
    .IsDependentOn("Clean")
    .Does(() =>
{
   NuGetRestore(configuration.SolutionFile);
});

Task("Default")
    .IsDependentOn("Debug");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);