#tool "nuget:?package=JetBrains.dotCover.CommandLineTools"
#tool "nuget:?package=NUnit.ConsoleRunner"
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

Task("Build-iOS")
	.IsDependentOn("NuGetRestore")
	.Does (() =>
	{
            var path = "./**/*.iOS/*.csproj";

    		DotNetBuild(path, settings => 
			settings.SetConfiguration(configuration)   
			.WithTarget("Build")
			.WithProperty("Platform", "iPhoneSimulator")
			.WithProperty("OutputPath", "bin/iPhoneSimulator")
			.WithProperty("TreatWarningsAsErrors", "false"));
			// .SetVerbosity(Verbosity.Minimal));
	});


Task("UnitTest")
    .IsDependentOn("Build")
    .Does(() =>
{
    var path = "./**/*.Tests/**/bin/**/*.Tests.dll";
    Information(path);

    NUnit3(path, new NUnit3Settings {
            NoResults = false,
            NoHeader = true,
            TeamCity = true,
            Workers = 5,
            Timeout = 10000,
            Results = new[] { new NUnit3Result { FileName = "TestResult.xml" } },   
        });
});

Task("UnitTestWithCoverage")
    .IsDependentOn("Build")
    .Does(() =>
{
    var path = "./**/*.Tests/**/bin/**/*.Tests.dll";
    Information(path);

    DotCoverCover((ICakeContext c) => {
            c.NUnit3(path,
                new NUnit3Settings {
                    NoResults = false,
                    NoHeader = true,
                    TeamCity = true,
                    Workers = 5,
                    Timeout = 10000,
                    Results = new[] { new NUnit3Result { FileName = "TestResult.xml" } },   
                }
            );
        },
        "CoverageResult",
        new DotCoverCoverSettings()
            .WithFilter(string.Format("+:{0}", buildConfiguration.MainProjectName))
            .WithFilter(string.Format("-:{0}.Tests", buildConfiguration.MainProjectName)));
});

Task("Default")
    .IsDependentOn("Debug");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);