#tool "nuget:?package=MSBuild.SonarQube.Runner.Tool"
#tool "nuget:?package=JetBrains.dotCover.CommandLineTools"
#tool "nuget:?package=NUnit.ConsoleRunner"
#tool "nuget:?package=NUnit.Extension.TeamCityEventListener"
#tool "nuget:?package=xunit.runner.console"

#addin "nuget:?package=Cake.CoreCLR"
#addin "nuget:?package=Cake.Figlet"
#addin "nuget:?package=Newtonsoft.Json"
#addin "nuget:?package=Cake.Sonar"
#addin "nuget:?package=Cake.Xamarin"
#addin "nuget:?package=Cake.AppCenter"

#load "./models/BuildConfiguration.cake"

using Newtonsoft.Json;

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var artifacts = new DirectoryPath("./artifacts").MakeAbsolute(Context.Environment);

BuildConfiguration buildConfiguration;

//////////////////////////////////////////////////////////////////////
// Criteria
//////////////////////////////////////////////////////////////////////

Func<bool> HasIOSPropjectFile => () => !string.IsNullOrEmpty(buildConfiguration.IOSProjectFile);

Func<bool> HasDroidPropjectFile => () => !string.IsNullOrEmpty(buildConfiguration.AndroidProjectFile);

//////////////////////////////////////////////////////////////////////
// PREPARING
//////////////////////////////////////////////////////////////////////

Setup(context =>
{
    // Read config or find projects
    var currentDirectory = Environment.CurrentDirectory;
    Information("Current directory: " + currentDirectory);

    FilePath filePaths = File("build.config");

    if (FileExists(filePaths.FullPath))
    {
        // TODO: find out if using cake.config is better
        Information("Using configuration file (build.config)");
        
        var configData = System.IO.File.ReadAllText(filePaths.FullPath, Encoding.UTF8);

        buildConfiguration = JsonConvert.DeserializeObject<BuildConfiguration>(configData);
    }
    else
    {
        Information("Configuration file does not exists (build.config).");
        Information("Trying to find solution & projects.");

        buildConfiguration = new BuildConfiguration();

        var solutionPath = "./**/*.sln";
        var solutionFiles = GetFiles(solutionPath);

        if(solutionFiles.Any())
        {
            buildConfiguration.SolutionFile = solutionFiles.FirstOrDefault().ToString();
            buildConfiguration.MainProjectName = solutionFiles.FirstOrDefault().GetFilenameWithoutExtension().ToString();
        }

        var iosPath = "./**/*iOS*.csproj";
        var iosFiles = GetFiles(iosPath);

        if(iosFiles.Any())
        {
            buildConfiguration.IOSProjectFile = iosFiles.FirstOrDefault().ToString();
        }

        var droidPath = "./**/*Droid*.csproj";
        var droidFiles = GetFiles(droidPath);

        if(droidFiles.Any())
        {
            buildConfiguration.AndroidProjectFile = droidFiles.FirstOrDefault().ToString();
        }

        var testPath = "./**/*.Tests.csproj";
        var testFiles = GetFiles(testPath);

        if(testFiles.Any())
        {
            buildConfiguration.TestProjectFile = testFiles.FirstOrDefault().ToString();
            buildConfiguration.TestProjectDirectory = testFiles.FirstOrDefault().GetDirectory().ToString();
        }
    }

    // TODO: validate config, should have solution
    Information(Figlet(buildConfiguration.MainProjectName));

    if(string.IsNullOrEmpty(buildConfiguration.SolutionFile))
        throw new Exception("Cannot start without solution file.");

    Information("Solution: " + buildConfiguration.SolutionFile);

    if(!string.IsNullOrEmpty(buildConfiguration.IOSProjectFile))
        Information("iOS project: " + buildConfiguration.IOSProjectFile);
    else
        Information("iOS project: NOT FOUND!");

    if(!string.IsNullOrEmpty(buildConfiguration.AndroidProjectFile))
        Information("Droid project: " + buildConfiguration.AndroidProjectFile);
    else
        Information("Droid project: NOT FOUND!");

    if(!string.IsNullOrEmpty(buildConfiguration.TestProjectFile))
    {
        Information("Test project: " + buildConfiguration.TestProjectFile);
        Information("Test project directory: " + buildConfiguration.TestProjectDirectory);
    }
    else
    {
        Information("Test project: NOT FOUND!");
        Information("Test project directory: NOT FOUND!");
    }
    
    EnsureDirectoryExists(artifacts);
    EnsureDirectoryExists(artifacts + "/tests");
    EnsureDirectoryExists(artifacts + "/coverage");
});


Task("Clean")
    .Does(() =>
{
    CleanDirectory(artifacts);
    CleanDirectory(artifacts + "/tests");
    CleanDirectory(artifacts + "/coverage");
    CleanDirectories("./**/bin");
    CleanDirectories("./**/obj");
});

Task("NuGetRestore")
    .IsDependentOn("Clean")
    .Does(() =>
{
    NuGetRestore(buildConfiguration.SolutionFile);
    DotNetCoreRestore(buildConfiguration.SolutionFile);
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

//////////////////////////////////////////////////////////////////////
// BUILDING ANDROID
//////////////////////////////////////////////////////////////////////

Task("Build-Android")
    .WithCriteria(HasDroidPropjectFile)
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

//////////////////////////////////////////////////////////////////////
// BUILDING iOS
//////////////////////////////////////////////////////////////////////

Task("Build-iOS")
    .WithCriteria(IsRunningOnUnix())
    .WithCriteria(HasIOSPropjectFile)
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
        .WithProperty("BuildIpa", "true")
        .WithProperty("TreatWarningsAsErrors", "false"));
	});

Task("AppCenterRelease-iOS")
    .IsDependentOn("Build-iOS")
    .Does(() =>
    {
        MobileCenterLogin(new MobileCenterLoginSettings { Token = "8600137f6b1b07c5e1a4d7792da999249631e148" });
    })
    .Finally(() =>
    {  
        MobileCenterLogout(new MobileCenterLoginSettings { Token = "8600137f6b1b07c5e1a4d7792da999249631e148" });
    });

//////////////////////////////////////////////////////////////////////
// ETC
//////////////////////////////////////////////////////////////////////

Task("CreateNugetPackage")
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

    DotNetCorePublish(buildConfiguration.TestProjectFile);

    DotCoverAnalyse((ctx) => {
        ctx.NUnit3(path);
    },
    "coverage.html",
    new DotCoverAnalyseSettings {
        ReportType = DotCoverReportType.HTML
    }
    .WithFilter(string.Format("+:{0}.*", buildConfiguration.MainProjectName))
    .WithFilter(string.Format("-:{0}.Tests", buildConfiguration.MainProjectName)));
});

Task("xUnitTestWithCoverage")
    .IsDependentOn("Build")
    .Does(() =>
{
    DotCoverCover(tool => {
        tool.DotNetCoreTool(
            buildConfiguration.TestProjectDirectory,
            "xunit",
            new ProcessArgumentBuilder()
                .AppendSwitchQuoted("-xml", artifacts + "/tests/results.xml")
                .AppendSwitch("-configuration", configuration)
                .Append("-noshadow")
                .Append("-nobuild"),
            new DotNetCoreToolSettings() {
                // EnvironmentVariables = GitVersionEnvironmentVariables,
            });
        },
        artifacts + "/coverage/coverage.dcvr",
        new DotCoverCoverSettings() {
                TargetWorkingDir = buildConfiguration.TestProjectDirectory,
                WorkingDirectory = buildConfiguration.TestProjectDirectory,
                // EnvironmentVariables = GitVersionEnvironmentVariables,
            }
            .WithFilter("+:" + buildConfiguration.MainProjectName + ".*")
            .WithFilter("-:" + buildConfiguration.MainProjectName + ".Tests*")
    );
})
.Finally(() => 
{
    DotCoverReport(
        artifacts + "/coverage/coverage.dcvr",
        new FilePath("coverage.xml"),
        new DotCoverReportSettings {
            ReportType = DotCoverReportType.DetailedXML
        }
    );

    DotCoverReport(
        artifacts + "/coverage/coverage.dcvr",
        new FilePath(artifacts + "/coverage/coverage.html"),
        new DotCoverReportSettings {
            ReportType = DotCoverReportType.HTML
        }
    );
});

Task("SonarBegin")
  .Does(() => {
     SonarBegin(new SonarBeginSettings{
        Url = "http://rhm-d-dock01.boolhosting.tld:9000/",
        Key = string.Format("Appollo-{0}", buildConfiguration.MainProjectName),
        Name = string.Format("Appollo-{0}", buildConfiguration.MainProjectName),
        Version = "123", // TODO
        Verbose = true
     });
  });

Task("SonarEnd")
  .Does(() => {
     SonarEnd(new SonarEndSettings
     {
        
     });
  });

Task("Sonar-xUnit")
  .IsDependentOn("SonarBegin")
  .IsDependentOn("xUnitTestWithCoverage")
  .IsDependentOn("SonarEnd");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);