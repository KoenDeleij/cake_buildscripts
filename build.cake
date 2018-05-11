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
#addin "nuget:?package=Cake.Tfs.Build.Variables"

#load "./models/BuildConfiguration.cake"
#load "./models/AppCenterSettings.cake"

using Newtonsoft.Json;

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var artifacts = new DirectoryPath("./artifacts").MakeAbsolute(Context.Environment);

BuildConfiguration buildConfiguration;
AppCenterSettings appCenterSettings;

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
        else
        {
            droidPath = "./**/*Android*.csproj";
            droidFiles = GetFiles(droidPath);

            if(droidFiles.Any())
            {
                buildConfiguration.AndroidProjectFile = droidFiles.FirstOrDefault().ToString();
            }
        }

        if(!string.IsNullOrEmpty(buildConfiguration.AndroidProjectFile))
        {
            buildConfiguration.AndroidKeystoreFile = EvaluateTfsBuildVariable("android_keystorefile", EnvironmentVariable("android_keystorefile") ?? Argument("android_keystorefile", string.Empty));
            buildConfiguration.AndroidKeystoreAlias = EvaluateTfsBuildVariable("android_keystorealias", EnvironmentVariable("android_keystorealias") ?? Argument("android_keystorealias", string.Empty));
            buildConfiguration.AndroidKeystorePassword = EvaluateTfsBuildVariable("android_keystorepasswd", EnvironmentVariable("android_keystorepasswd") ?? Argument("android_keystorepasswd", string.Empty));            
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
    {
        Information("Droid project: " + buildConfiguration.AndroidProjectFile);
        Information("Droid keystore: " + buildConfiguration.AndroidKeystoreFile);
        Information("Droid keystore alias: " + buildConfiguration.AndroidKeystoreAlias);
        if(!string.IsNullOrEmpty(buildConfiguration.AndroidKeystorePassword))
            Information("Droid keystore password set.");
    }
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

    Information("Build configuration: " + configuration);
    
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
    })
    .OnError(exception =>
    {
        Information("Possible errors with restoring packages");
        Information(exception);
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

Task("Build-Droid")
    .WithCriteria(HasDroidPropjectFile)
    .WithCriteria(() => buildConfiguration.IsValidForAndroidSigning)
	.IsDependentOn("NuGetRestore")
	.Does(() =>
{ 		
        //https://docs.microsoft.com/en-us/xamarin/android/deploy-test/building-apps/build-process
        // TODO: verify & validate
        var file = BuildAndroidApk(buildConfiguration.AndroidProjectFile, true, configuration, settings =>
            settings.SetConfiguration(configuration)
                    .WithProperty("AndroidKeyStore", "true")
                    .WithProperty("AndroidSigningStorePass", "keyStorePassword")
                    .WithProperty("AndroidSigningKeyStore", "keyStore")
                    .WithProperty("AndroidSigningKeyAlias", "keyStoreAlias")
                    .WithProperty("AndroidSigningKeyPass", "keyStorePassword")
            );

        Information(file.ToString());
});

Task("AppCenterRelease-Droid")
    .IsDependentOn("Build-Droid")
    .IsDependentOn("AppCenterSettings")
    .IsDependentOn("AppCenterLogin")
    .WithCriteria(() => appCenterSettings.IsValidForDistribution)
    .Does(() =>
    {
        //https://cakebuild.net/api/Cake.AppCenter/
        // MBP-JacobD:Redhotminute.Appollo.Cake.BuildScripts jacob.duijzer$ appcenter apps set-current CakeTestApp/CakeTestApp-Dev
        // MBP-JacobD:Redhotminute.Appollo.Cake.BuildScripts jacob.duijzer$ appcenter distribute release -f CakeTestApp.iOS/bin/iPhone/CakeTestApp.iOS.ipa -g Collaborators
        // Error: binary file 'CakeTestApp.iOS/bin/iPhone/CakeTestApp.iOS.ipa' doesn't exist
        // MBP-JacobD:Redhotminute.Appollo.Cake.BuildScripts jacob.duijzer$ ls CakeTestApp
        // MBP-JacobD:Redhotminute.Appollo.Cake.BuildScripts jacob.duijzer$ appcenter distribute release -f CakeTestApp/CakeTestApp.iOS/bin/iPhone/CakeTestApp.iOS.ipa -g Collaborators

        var apkFilePattern = "./**/*.apk";
        var foundApkFiles = GetFiles(apkFilePattern);

        if(foundApkFiles.Any())
        {
            AppCenterDistributeRelease(
                new AppCenterDistributeReleaseSettings() 
                { 
                    App = appCenterSettings.Owner + "/" + appCenterSettings.AppName, 
                    File = foundApkFiles.FirstOrDefault().ToString(),
                    Group = appCenterSettings.DistributionGroup
                });
        }
    })
    .Finally(() =>
    {  
        // TODO: move to settings
        AppCenterLogout(new AppCenterLogoutSettings { Token = "8600137f6b1b07c5e1a4d7792da999249631e148" });
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
        // TODO: BuildiOSIpa (Cake.Xamarin, https://github.com/Redth/Cake.Xamarin/blob/master/src/Cake.Xamarin/Aliases.cs)
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
    .IsDependentOn("AppCenterSettings")
    .IsDependentOn("AppCenterLogin")
    .WithCriteria(() => appCenterSettings.IsValidForDistribution)
    .Does(() =>
    {
        //https://cakebuild.net/api/Cake.AppCenter/
        // MBP-JacobD:Redhotminute.Appollo.Cake.BuildScripts jacob.duijzer$ appcenter apps set-current CakeTestApp/CakeTestApp-Dev
        // MBP-JacobD:Redhotminute.Appollo.Cake.BuildScripts jacob.duijzer$ appcenter distribute release -f CakeTestApp.iOS/bin/iPhone/CakeTestApp.iOS.ipa -g Collaborators
        // Error: binary file 'CakeTestApp.iOS/bin/iPhone/CakeTestApp.iOS.ipa' doesn't exist
        // MBP-JacobD:Redhotminute.Appollo.Cake.BuildScripts jacob.duijzer$ ls CakeTestApp
        // MBP-JacobD:Redhotminute.Appollo.Cake.BuildScripts jacob.duijzer$ appcenter distribute release -f CakeTestApp/CakeTestApp.iOS/bin/iPhone/CakeTestApp.iOS.ipa -g Collaborators

        var ipaFilePattern = "./**/*iOS*.ipa";
        var foundIpaFiles = GetFiles(ipaFilePattern);

        if(foundIpaFiles.Any())
        {
            AppCenterDistributeRelease(
                new AppCenterDistributeReleaseSettings() 
                { 
                    App = appCenterSettings.Owner + "/" + appCenterSettings.AppName, 
                    File = foundIpaFiles.FirstOrDefault().ToString(),
                    Group = appCenterSettings.DistributionGroup
                });
        }
    })
    .Finally(() =>
    {  
        // TODO: move to settings
        AppCenterLogout(new AppCenterLogoutSettings { Token = "8600137f6b1b07c5e1a4d7792da999249631e148" });
    });

//////////////////////////////////////////////////////////////////////
// AppCenter Tasks]
//
// Make sure the appcenter cli tools are installed
//
//////////////////////////////////////////////////////////////////////

Task("AppCenterSettings")
    .Does(() => 
    {
        appCenterSettings = new AppCenterSettings();
        appCenterSettings.Owner = EvaluateTfsBuildVariable("appcenter_owner", EnvironmentVariable("appcenter_owner") ?? Argument("appcenter_owner", string.Empty));
        appCenterSettings.AppName = EvaluateTfsBuildVariable("appcenter_appname", EnvironmentVariable("appcenter_appname") ?? Argument("appcenter_appname", string.Empty));
        appCenterSettings.DistributionGroup = EvaluateTfsBuildVariable("appcenter_distributiongroup", EnvironmentVariable("appcenter_distributiongroup") ?? Argument("appcenter_distributiongroup", string.Empty));
        
        Information("Owner: " + appCenterSettings.Owner);
        Information("AppName: " + appCenterSettings.AppName);
        Information("DistributionGroup: " + appCenterSettings.DistributionGroup);
        Information("ValidForDistribution: " + appCenterSettings.IsValidForDistribution);
    });

Task("AppCenterLogin")
    .Does(() => 
    {
        // TODO: move to settings
        AppCenterLogin(new AppCenterLoginSettings { Token = "8600137f6b1b07c5e1a4d7792da999249631e148" });
    })
    .OnError(exception =>
    {
        Information(exception);

        Information("Make sure the appcenter cli tools are installed!");
        Information("npm install -g appcenter-cli");
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