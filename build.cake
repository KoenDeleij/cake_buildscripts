#tool "nuget:?package=JetBrains.dotCover.CommandLineTools"
#tool "nuget:?package=NUnit.ConsoleRunner"
#tool "nuget:?package=NUnit.Extension.TeamCityEventListener"
#tool "nuget:?package=xunit.runner.console"

#addin "nuget:?package=Cake.CoreCLR"
#addin "nuget:?package=Cake.Figlet"
#addin "nuget:?package=Cake.Xamarin"
#addin "nuget:?package=Cake.AppCenter"
#addin "nuget:?package=Cake.Tfs.Build.Variables"
#addin "nuget:?package=Cake.Incubator"
#addin "nuget:?package=Cake.Plist"
#addin "nuget:?package=Cake.AndroidAppManifest"
#addin "nuget:?package=Newtonsoft.Json"

#load "./helpers/Configurator.cake"

using Newtonsoft.Json;

var target = Argument("target", "Help");
var configuration = Argument("configuration", "Release");

var artifacts = new DirectoryPath("./artifacts").MakeAbsolute(Context.Environment);

//////////////////////////////////////////////////////////////////////
// PREPARING
//////////////////////////////////////////////////////////////////////

Setup(context =>
{
    if(target.ToLower() != "help")
    {
        Configurator.Initialize(Context);

        EnsureDirectoryExists(artifacts);
        EnsureDirectoryExists(artifacts + "/tests");
        EnsureDirectoryExists(artifacts + "/coverage");
    }    
});


Task("Clean")
    .Does(() =>
{
    CleanDirectory(artifacts);
    CleanDirectory(artifacts + "/tests");
    CleanDirectory(artifacts + "/coverage");

    if(Configurator.ShouldClean)
    {
        CleanDirectories("./**/bin");
        CleanDirectories("./**/obj");
    }
});

Task("NuGetRestore")
    .DoesForEach(GetFiles("**/*.csproj"), (file) => 
    {
        Information("Restoring " + file.ToString());
        NuGetRestore(file);
        DotNetCoreRestore(file.ToString());
    })
    .OnError(exception =>
    {
        Information("Possible errors while restoring packages, continuing seems to work.");
        Information(exception);
    })
    .DeferOnError();

//////////////////////////////////////////////////////////////////////
// BUILDING
//////////////////////////////////////////////////////////////////////

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("NuGetRestore")
    .Does(() =>
{
    MSBuild (Configurator.SolutionFile, c => {
		c.Configuration = configuration;
		c.MSBuildPlatform = Cake.Common.Tools.MSBuild.MSBuildPlatform.x86;
        c.MaxCpuCount = 10;
	});
});

//////////////////////////////////////////////////////////////////////
// BUILDING ANDROID
//////////////////////////////////////////////////////////////////////

Task("Build-Droid")
    .WithCriteria(() => Configurator.IsValidForBuildingAndroid)
	.IsDependentOn("Build")
    .IsDependentOn("SetDroidVersion")
	.Does(() =>
{ 		
        //https://docs.microsoft.com/en-us/xamarin/android/deploy-test/building-apps/build-process
        // TODO: verify & validate
        var file = BuildAndroidApk(Configurator.AndroidProjectFile, true, configuration, settings =>
            settings.SetConfiguration(configuration)
                    .WithProperty("AndroidKeyStore", "true")
                    .WithProperty("AndroidSigningStorePass", Configurator.AndroidKeystorePassword)
                    .WithProperty("AndroidSigningKeyStore", Configurator.AndroidKeystoreFile)
                    .WithProperty("AndroidSigningKeyAlias", Configurator.AndroidKeystoreAlias)
                    .WithProperty("AndroidSigningKeyPass", Configurator.AndroidKeystorePassword)
            );

        Information(file.ToString());
});

Task("SetDroidVersion")
    .Does(() =>
    {
        var manifestPattern = "./**/AndroidManifest.xml";
        var foundManifestFiles = GetFiles(manifestPattern);
        if(foundManifestFiles.Any())
        {
            var manifestPath = foundManifestFiles.FirstOrDefault();
            var manifest = DeserializeAppManifest(manifestPath);

            Information("manifest -> {0}", manifest.Dump());

            //manifest.PackageName = "com.example.mycoolapp";
            manifest.VersionName = Configurator.FullVersion;
            manifest.VersionCode = int.Parse(Configurator.FullVersion.Replace(".",""));
            // manifest.ApplicationIcon = "@mipmap/ic_launcher";
            // manifest.ApplicationLabel = "Android Application";
            // manifest.Debuggable = false;

            // data["CFBundleShortVersionString"] = Configurator.Version;
            // data["CFBundleVersion"] = Configurator.FullVersion;

            // data["CFBundleShortVersionString"] = Configurator.Version;
            // data["CFBundleVersion"] = Configurator.FullVersion;

            SerializeAppManifest(manifestPath, manifest);
        }
        else
        {
            throw new Exception("Can't find AndroidManifest.xml");
        }
    });

Task("AppCenterRelease-Droid")
    .IsDependentOn("Build-Droid")
    .IsDependentOn("AppCenterLogin")
    .WithCriteria(() => Configurator.IsValidForAppCenterDistribution)
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
                    App = Configurator.AppCenterOwner + "/" + Configurator.AppCenterAppName, 
                    File = foundApkFiles.FirstOrDefault().ToString(),
                    Group = Configurator.AppCenterDistributionGroup
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
    .WithCriteria(() => Configurator.IsValidForBuildingIOS)
    .IsDependentOn("Build")
    .IsDependentOn("SetIOSVersion")
	.Does (() =>
	{
        // TODO: BuildiOSIpa (Cake.Xamarin, https://github.com/Redth/Cake.Xamarin/blob/master/src/Cake.Xamarin/Aliases.cs)
        MSBuild(Configurator.IOSProjectFile, settings => 
            settings.SetConfiguration(configuration)   
            .WithTarget("Build")
            .WithProperty("Platform", "iPhone")
            .WithProperty("OutputPath", "bin/iPhone")
            .WithProperty("BuildIpa", "true")
            .WithProperty("TreatWarningsAsErrors", "false"));
	});

Task("SetIOSVersion")
    .Does(() =>
    {
        var plistPattern = "./**/Info.plist";
        var foundPListFiles = GetFiles(plistPattern);
        if(foundPListFiles.Any())
        {
            var plistPath = foundPListFiles.FirstOrDefault().ToString();
            dynamic data = DeserializePlist(plistPath);

            data["CFBundleShortVersionString"] = Configurator.Version;
            data["CFBundleVersion"] = Configurator.FullVersion;

            SerializePlist(plistPath, data);
        }
        else
        {
            throw new Exception("Can't find Info.plist");
        }
    });

Task("AppCenterRelease-iOS")
    .WithCriteria(() => Configurator.IsValidForAppCenterDistribution)
    .IsDependentOn("Build-iOS")
    .IsDependentOn("AppCenterLogin")
    .Does(() =>
    {
        var ipaFilePattern = "./**/*iOS*.ipa";
        var foundIpaFiles = GetFiles(ipaFilePattern);

        if(foundIpaFiles.Any())
        {
            AppCenterDistributeRelease(
                new AppCenterDistributeReleaseSettings() 
                { 
                    App = Configurator.AppCenterOwner + "/" + Configurator.AppCenterAppName, 
                    File = foundIpaFiles.FirstOrDefault().ToString(),
                    Group = Configurator.AppCenterDistributionGroup
                });
        }
    })
    .Finally(() =>
    {  
        // TODO: move to settings
        AppCenterLogout(new AppCenterLogoutSettings { Token = Configurator.AppCenterToken });
    });

//////////////////////////////////////////////////////////////////////
//
// AppCenter Tasks
//
//////////////////////////////////////////////////////////////////////

Task("AppCenterLogin")
    .Does(() => 
    {
        // TODO: move to settings
        AppCenterLogin(new AppCenterLoginSettings { Token = Configurator.AppCenterToken });
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
        throw new NotImplementedException();
        // Information(buildConfiguration.NuspecFile);
        // NuGetPack(buildConfiguration.NuspecFile, new NuGetPackSettings());
    });

Task("PushNugetPackage")    
    .WithCriteria(() => Configurator.IsValidForPushingPackage)
    .DoesForEach(GetFiles("**/" + Configurator.ProjectName + "*.nupkg"), (file) => 
    {
        Information("Pushing " + file.ToString());
        NuGetPush(file, new NuGetPushSettings {
            Source = Configurator.NugetUrl,
            ApiKey = Configurator.NugetToken
        });
    })
    .OnError(exception =>
    {
        Information(exception);
    });
    

//////////////////////////////////////////////////////////////////////
// TESTING
//////////////////////////////////////////////////////////////////////

Task("UnitTest")
    .IsDependentOn("Build")
    .Does(() =>
{
    DotNetCoreTest(
                Configurator.TestProjectFile,
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

    DotNetCorePublish(Configurator.TestProjectFile);

    DotCoverAnalyse((ctx) => {
        ctx.NUnit3(path);
    },
    "coverage.html",
    new DotCoverAnalyseSettings {
        ReportType = DotCoverReportType.HTML
    }
    .WithFilter(string.Format("+:{0}.*", Configurator.ProjectName))
    .WithFilter(string.Format("-:{0}.Tests", Configurator.ProjectName)));
});

Task("xUnitTestWithCoverage")
    .IsDependentOn("Build")
    .Does(() =>
{
    DotCoverCover(tool => {
        tool.DotNetCoreTool(
            Configurator.TestProjectDirectory,
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
                TargetWorkingDir = Configurator.TestProjectDirectory,
                WorkingDirectory = Configurator.TestProjectDirectory,
                // EnvironmentVariables = GitVersionEnvironmentVariables,
            }
            .WithFilter("+:" + Configurator.ProjectName + ".*")
            .WithFilter("-:" + Configurator.ProjectName + ".Tests*")
            // .WithFilter("-:" + Configurator.ProjectName + ".Tests*")
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

//////////////////////////////////////////////////////////////////////
// Help
//////////////////////////////////////////////////////////////////////

Task("Help")
    .Does(() => 
    {
        Information(target);
        //--target
        Information("--target {target name}");
        Information("Specify target. Available targets: many");
        Information("");

        Information("--solution_file: path to the solution file");
        Information("--project_name: name of the project");

        Information("For more info & questions: ask Jacob.");
    });

Task("Test")
    .Does(() =>
    {

    });

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);