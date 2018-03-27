#tool nuget:?package=NUnit.ConsoleRunner
#tool "nuget:?package=NUnit.Extension.TeamCityEventListener"

#addin nuget:?package=Cake.CoreCLR
#addin nuget:?package=Cake.Figlet
#addin nuget:?package=Newtonsoft.Json

#load "./models/BuildConfiguration.cake"

using Newtonsoft.Json;

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

BuildConfiguration buildConfiguration;

Task("Debug").Does(() => 
{
    FilePath filePaths = File("build.config");

    if (FileExists(filePaths.FullPath))
    {
        Information("File exists!");
        
        var configData = System.IO.File.ReadAllText(filePaths.FullPath, Encoding.UTF8);

        buildConfiguration = JsonConvert.DeserializeObject<BuildConfiguration>(configData);
        Information(Figlet(buildConfiguration.MainProjectName));
    }
    else
    {
        Information("Configuration file does not exists!");
        Information("Trying to fetch environment vars");
    }
});

Task("Clean")
    .IsDependentOn("Debug")
    .Does(() =>
{
    CleanDirectories("./**/bin");
    CleanDirectories("./**/obj");
});

Task("NuGetRestore")
    .IsDependentOn("Clean")
    .Does(() =>
{
   NuGetRestore(buildConfiguration.SolutionFile);
});

Task("Build")
    .IsDependentOn("NuGetRestore")
    .Does(() =>
{
    MSBuild (buildConfiguration.SolutionFile, c => {
		c.Configuration = configuration;
		c.MSBuildPlatform = Cake.Common.Tools.MSBuild.MSBuildPlatform.x86;
        c.MaxCpuCount = 10;
	});
});

Task("UnitTest")
    .IsDependentOn("Build")
    .Does(() =>
{
    var path = "./**/*.Tests/**/bin/**/*.Tests.dll";
    Information(path);

    NUnit3(path, new NUnit3Settings {
            NoResults = true,
            NoHeader = true,
            TeamCity = true,
            Workers = 5,
            Timeout = 10000,
            Results = new[] { new NUnit3Result { FileName = "TestResult.xml" } },   
        });
});

Task("Default")
    .IsDependentOn("Debug");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);