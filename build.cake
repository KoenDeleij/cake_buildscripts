#tool nuget:?package=MSBuild.SonarQube.Runner.Tool
#addin nuget:?package=Cake.Sonar

#tool "nuget:?package=JetBrains.dotCover.CommandLineTools&version=2018.1.0"
#tool "nuget:?package=NUnit.ConsoleRunner"
#tool "nuget:?package=NUnit.Extension.TeamCityEventListener"
#tool "nuget:?package=xunit.runner.console&version=2.4.0-beta.1.build3958"

#addin nuget:?package=Cake.CoreCLR
#addin nuget:?package=Cake.Figlet
#addin nuget:?package=Newtonsoft.Json

#load "./models/BuildConfiguration.cake"

using Newtonsoft.Json;

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
// var workingDirectory = Argument("workingdirectory", "./");

BuildConfiguration buildConfiguration;

Task("Debug").Does(() => 
{
    var currentDirectory = Environment.CurrentDirectory;
    Information("Current directory: " + currentDirectory);

    // Information("Working directory: " + workingDirectory);

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
        Information("Trying to find solution & projects");

        buildConfiguration = new BuildConfiguration();

        var solutionPath = "./**/*.sln";
        var solutionFiles = GetFiles(solutionPath);

        if(solutionFiles.Any())
        {
            buildConfiguration.SolutionFile = solutionFiles.FirstOrDefault().ToString();
            buildConfiguration.MainProjectName = solutionFiles.FirstOrDefault().GetFilenameWithoutExtension().ToString();
            Information("Going to use solution: " + solutionFiles.FirstOrDefault());
        }

        var iosPath = "./**/*iOS*.csproj";
        var iosFiles = GetFiles(iosPath);

        if(iosFiles.Any())
        {
            buildConfiguration.IOSProjectFile = iosFiles.FirstOrDefault().ToString();
            Information("Going to use iOS project file: " + iosFiles.FirstOrDefault());
        }

        var droidPath = "./**/*Droid*.csproj";
        var droidFiles = GetFiles(droidPath);

        if(droidFiles.Any())
        {
            buildConfiguration.AndroidProjectFile = droidFiles.FirstOrDefault().ToString();
            Information("Going to use Droid project file: " + droidFiles.FirstOrDefault());
        }

        var testPath = "./**/*.Tests.csproj";
        var testFiles = GetFiles(testPath);

        if(testFiles.Any())
        {
            buildConfiguration.TestProjectFile = testFiles.FirstOrDefault().ToString();
            Information("Going to use Test project file: " + testFiles.FirstOrDefault());
        }
    }

    // TODO: validate config, should have solution
    Information(Figlet(buildConfiguration.MainProjectName));
});

//////////////////////////////////////////////////////////////////////
// PREPARING
//////////////////////////////////////////////////////////////////////

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

//////////////////////////////////////////////////////////////////////
// BUILDING
//////////////////////////////////////////////////////////////////////

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

Task("Build-Android")
	.IsDependentOn("NuGetRestore")
	.Does(() =>
{ 		
        MSBuild (buildConfiguration.AndroidProjectFile, c => 
        {
		    c.Configuration = configuration;
		    c.MSBuildPlatform = Cake.Common.Tools.MSBuild.MSBuildPlatform.x86;
            c.MaxCpuCount = 10;
	    });
		// DotNetBuild(buildConfiguration.AndroidProjectFile, settings =>
		// 	settings.SetConfiguration(configuration)           
		// 	.WithProperty("DebugSymbols", "false")
		// 	.WithProperty("TreatWarningsAsErrors", "false")
		// 	.SetVerbosity(Verbosity.Minimal));
});

Task("Build-iOS")
	.IsDependentOn("NuGetRestore")
	.Does (() =>
	{
        // TODO: test is iOS project exists
            // var path = "./*.iOS/*.csproj";

    		MSBuild(buildConfiguration.IOSProjectFile, settings => 
			settings.SetConfiguration(configuration)   
			.WithTarget("Build")
			.WithProperty("Platform", "iPhone")
			.WithProperty("OutputPath", "bin/iPhone")
			.WithProperty("TreatWarningsAsErrors", "false"));
	});

Task("CreateNugetPackage")
    .IsDependentOn("Debug")
    .Does(() =>
    {
        Information(buildConfiguration.NuspecFile);
        NuGetPack(buildConfiguration.NuspecFile, new NuGetPackSettings());
    });

Task("PushNugetPackage")
    .IsDependentOn("CreateNugetPackage")
    .Does(() =>
    {
        var nugetUrl = Environment.GetEnvironmentVariable("NugetUrl");
        var nugetApiKey = Environment.GetEnvironmentVariable("NugetApiKey");

        Information(nugetUrl);
        Information(nugetApiKey);

        var path = "./*.nupkg";
        var files = GetFiles(path);

        foreach(FilePath file in files)
        {
            Information("Uploading " + file);
            NuGetPush(file, new NuGetPushSettings {
                Source = nugetUrl,
                ApiKey = nugetApiKey
            });
        }
    });
    

//////////////////////////////////////////////////////////////////////
// TESTING
//////////////////////////////////////////////////////////////////////

Task("UnitTest")
    .IsDependentOn("Build")
    .Does(() =>
{
    DotNetCoreTest(
                buildConfiguration.TestProjectFile,
                new DotNetCoreTestSettings()
                {
                    Configuration = configuration,
                    ArgumentCustomization = args => args.Append("--logger \"trx;LogFileName=TestResults.xml\""),
                    NoBuild = true
                });
});

// TODO environ var NUnit / xUnit
Task("NUnitTestWithCoverage")
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
            .WithFilter(string.Format("+:{0}.*", buildConfiguration.MainProjectName))
            .WithFilter(string.Format("-:{0}.Tests", buildConfiguration.MainProjectName)));
});

Task("xUnitTestWithCoverage")
    .IsDependentOn("Build")
    .Does(() =>
{
        var path = "./**/*.Tests/**/bin/**/*.Tests.dll";

DotCoverCover(tool => {
        tool.DotNetCoreTool(
            path,
            "xunit",
            new ProcessArgumentBuilder()
                .AppendSwitchQuoted("-xml", string.Format("{0}/tests/{1}.xml", artifacts, testProject.GetFilenameWithoutExtension()))
                .AppendSwitch("-configuration", configuration)
                .Append("-noshadow"),
            new DotNetCoreToolSettings() {
                // EnvironmentVariables = GitVersionEnvironmentVariables,
            });
        },
        artifacts + "/coverage/coverage-"+ testProject.GetFilenameWithoutExtension() + ".dcvr",
        new DotCoverCoverSettings() {
                // TargetWorkingDir = testProject.GetDirectory(),
                // WorkingDirectory = testProject.GetDirectory(),
                // EnvironmentVariables = GitVersionEnvironmentVariables,
            }
            .WithFilter("+:OmniSharp.*")
    );

        // DotCoverCover(tool => {
        //     tool.XUnit2(path,
        //         new XUnit2Settings {
        //             ShadowCopy = false
        //         });
        // },
        // new FilePath("./result.dcvr"),
        // new DotCoverCoverSettings()
        //     .WithFilter("+:App")
        //     .WithFilter("-:App.Tests"));
});

Task("SonarBegin")
  .Does(() => {
     SonarBegin(new SonarBeginSettings{
        Url = "http://rhm-d-dock01.boolhosting.tld:9000/",
        Login = "5779d7544d436849f9f8afc51c42331def4e700d",
        Password = "admin",
        Name = string.Format("Appollo-{0}", buildConfiguration.MainProjectName),
        Version = "123",
        Verbose = true
     });
  });

Task("SonarEnd")
  .Does(() => {
     SonarEnd(new SonarEndSettings{
        Login = "admin",
        Password = "admin"
     });
  });

Task("Sonar")
  .IsDependentOn("SonarBegin")
  .IsDependentOn("NUnitTestWithCoverage")
  .IsDependentOn("SonarEnd");


Task("Default")
    .IsDependentOn("Debug");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);