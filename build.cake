#tool "nuget:?package=JetBrains.dotCover.CommandLineTools"
#tool "nuget:?package=NUnit.ConsoleRunner"
#tool "nuget:?package=NUnit.Extension.TeamCityEventListener"
#tool "nuget:?package=xunit.runner.console"
#tool "nuget:?package=MSBuild.SonarQube.Runner.Tool"

#addin "nuget:?package=Cake.CoreCLR"
#addin "nuget:?package=Cake.Figlet"
#addin "nuget:?package=Cake.Xamarin"
#addin "nuget:?package=Cake.AppCenter"
#addin "nuget:?package=Cake.Tfs.Build.Variables"
#addin "nuget:?package=Cake.Incubator"
#addin "nuget:?package=Cake.Plist"
#addin "nuget:?package=Cake.AndroidAppManifest"
#addin "nuget:?package=Newtonsoft.Json"
#addin "nuget:?package=Cake.Coverlet"
#addin "nuget:?package=Cake.Sonar"

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
    Information("Initialize Configuration");
    Configurator.Initialize(Context);

    if(target.ToLower() != "help")
    {
        Information("Creating artifact directories");
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
        Information("## Restoring " + file.ToString());
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
		c.Configuration = Configurator.BuildConfiguration;        
		c.MSBuildPlatform = Cake.Common.Tools.MSBuild.MSBuildPlatform.x86;
        c.MaxCpuCount = 0;
	});
});

Task("Build-Apps")
    .IsDependentOn("SonarQubeCoverage")
    .IsDependentOn("Build-Droid")
    .IsDependentOn("Build-iOS");

Task("Build-Apps-Appcenter")
    .IsDependentOn("SonarQubeCoverage")
    .IsDependentOn("AppCenterLogin")
    .IsDependentOn("Build-iOS")
    .IsDependentOn("AppCenterRelease-iOSUpload")
    .IsDependentOn("Build-Droid")
    .IsDependentOn("AppCenterRelease-DroidUpload")
    .IsDependentOn("AppCenterLogout");

//Task("Apps-Release")
//    .IsDependentOn("SonarQubeCoverage")
//    .IsDependentOn("Release-iOS")
//    .IsDependentOn("Release-Droid");



//////////////////////////////////////////////////////////////////////
// BUILDING ANDROID
//////////////////////////////////////////////////////////////////////

Task("Build-Droid")
    .WithCriteria(() => Configurator.IsValidForBuildingAndroid)
	.IsDependentOn("Clean")
    .IsDependentOn("NuGetRestore")
    .IsDependentOn("SetDroidVersion")
	.Does(() =>
    { 		
        Information($"## Build Droid {Configurator.BuildConfiguration}");

        Information($"With keystore {Configurator.AndroidKeystoreFile}");
        //https://docs.microsoft.com/en-us/xamarin/android/deploy-test/building-apps/build-process
        // TODO: verify & validate
        var file = BuildAndroidApk(Configurator.AndroidProjectFile, true, Configurator.BuildConfiguration, settings =>
            settings.SetConfiguration(Configurator.BuildConfiguration) 
                    .WithProperty("Verbosity","q")
                    .WithProperty("AndroidKeyStore", "true")
                    .WithProperty("AndroidSigningStorePass", Configurator.AndroidKeystorePassword)
                    .WithProperty("AndroidSigningKeyStore", Configurator.AndroidKeystoreFile.Quote())
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

            //versioning
            manifest.VersionName = Configurator.FullVersion;
            manifest.VersionCode = int.Parse(Configurator.AndroidVersion.Replace(".",""));

            //theming
            if(!string.IsNullOrEmpty(Configurator.AndroidStyle))
                manifest.ApplicationTheme = Configurator.AndroidStyle;

            if(!string.IsNullOrEmpty(Configurator.AndroidIcon))
                manifest.ApplicationIcon = Configurator.AndroidIcon;

            //app name
            manifest.PackageName = Configurator.AppPackageName;
            manifest.ApplicationLabel = Configurator.AndroidDisplayName;

            SerializeAppManifest(manifestPath, manifest);
        }
        else
        {
            throw new Exception("Can't find AndroidManifest.xml");
        }
    });

Task("AppCenterRelease-DroidUpload")
    .WithCriteria(() => Configurator.IsValidForDroidAppCenterDistribution)
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
            Information($"Appcenter upload droid owner {Configurator.AppCenterOwner}, appname {Configurator.AppCenterDroidAppName}, distribution {Configurator.AppCenterDistributionGroup}");
            AppCenterDistributeRelease(
                new AppCenterDistributeReleaseSettings() 
                { 
                    App = Configurator.AppCenterOwner + "/" + Configurator.AppCenterDroidAppName, 
                    File = foundApkFiles.FirstOrDefault().ToString(),
                    Group = Configurator.AppCenterDistributionGroup
                });
        }
    })
    .Finally(() =>
    {  
        // TODO: move to settings
        AppCenterLogout(new AppCenterLogoutSettings { Token = Configurator.AppCenterToken });
    });

Task("AppCenterRelease-Droid")
    .IsDependentOn("Build-Droid")
    .IsDependentOn("AppCenterLogin")
    .IsDependentOn("AppCenterRelease-DroidUpload")
    .IsDependentOn("AppCenterLogout")
    .WithCriteria(() => Configurator.IsValidForDroidAppCenterDistribution);

//Task("Release-Droid")
//    .IsDependentOn("Build-Droid")
//    .Does(() =>
//    {
//        Information($"## Release Droid");
//        
    });

//////////////////////////////////////////////////////////////////////
// BUILDING iOS
//////////////////////////////////////////////////////////////////////

Task("Build-iOS")
    .WithCriteria(IsRunningOnUnix())
    .WithCriteria(() => Configurator.IsValidForBuildingIOS)
    .IsDependentOn("Clean")
    .IsDependentOn("NuGetRestore")
    .IsDependentOn("SetIOSParameters")
	.Does (() =>
	{
        Information("## Build");
        // TODO: BuildiOSIpa (Cake.Xamarin, https://github.com/Redth/Cake.Xamarin/blob/master/src/Cake.Xamarin/Aliases.cs)
        MSBuild(Configurator.IOSProjectFile, settings => 
            settings.SetConfiguration(Configurator.BuildConfiguration)   
            .WithTarget("Build")
            .WithProperty("Platform", "iPhone")
            .WithProperty("OutputPath", "bin/iPhone")
            .WithProperty("BuildIpa", "true")
            .WithProperty("TreatWarningsAsErrors", "false")
            .WithProperty("Verbosity","q"));
	});

Task("SetIOSParameters")
    .Does(() =>
    {
        Information("## SetIOSParameters");
        var plistPattern = "./**/Info.plist";
        var foundPListFiles = GetFiles(plistPattern);
        if(foundPListFiles.Any())
        {
            var plistPath = foundPListFiles.FirstOrDefault().ToString();

            Information(string.Format("Plist file: {0}", plistPath.ToString()));

            dynamic data = DeserializePlist(plistPath);
            
            data["CFBundleShortVersionString"] = Configurator.iOSVersion;
            data["CFBundleVersion"] = Configurator.FullVersion;

            if(!string.IsNullOrEmpty(Configurator.AppPackageName))
            {
                Information(string.Format("Writing bundle identifier: {0}", Configurator.AppPackageName));
                data["CFBundleIdentifier"] = Configurator.AppPackageName;
            }

            if(!string.IsNullOrEmpty(Configurator.AppDisplayName))
            {
                    Information(string.Format("Writing app name: {0}", Configurator.AppDisplayName));
                    data["CFBundleName"] = Configurator.AppDisplayName;
                    data["CFBundleDisplayName"] = Configurator.AppDisplayName;              
            }     

            if(!string.IsNullOrEmpty(Configurator.IOSAppIconsSet))
            {
                Information(string.Format("Writing app icon set: {0}", Configurator.IOSAppIconsSet));
                data["XSAppIconAssets"] = Configurator.IOSAppIconsSet;
            }

            if(!string.IsNullOrEmpty(Configurator.IOSSplashXib))
            {
                Information(string.Format("Writing splash: {0}", Configurator.IOSSplashXib));
                data["UILaunchStoryboardName"] = Configurator.IOSSplashXib;
            }

            if(!string.IsNullOrEmpty(Configurator.IOSURLSchema))
            {
                Information(string.Format("Writing url schema: {0}", Configurator.IOSURLSchema));
                data["CFBundleURLTypes"][0]["CFBundleURLSchemes"][0] = Configurator.IOSURLSchema;
            }

            if(!string.IsNullOrEmpty(Configurator.IOSURLIdentifier))
            {
                Information(string.Format("Writing url identifier: {0}", Configurator.IOSURLIdentifier));
                data["CFBundleURLTypes"][0]["CFBundleURLName"] = Configurator.IOSURLIdentifier;
            }

            SerializePlist(plistPath, data);
        }
        else
        {
            throw new Exception("Can't find Info.plist");
        }

        //Entitlements
        var entitlementsPattern = "./**/Entitlements.plist";
        var foundEntitlementsFiles = GetFiles(entitlementsPattern);
        if(foundEntitlementsFiles.Any())
        {
            var entitlementsPath = foundEntitlementsFiles.FirstOrDefault().ToString();

            Information(string.Format("Entitlements file: {0}", entitlementsPath.ToString()));

            dynamic entitlementsData = DeserializePlist(entitlementsPath);
            
            if(!string.IsNullOrEmpty(Configurator.IOSAssociatedDomain))
            {
                Information(string.Format("Writing associated domain: {0} {1}", Configurator.IOSAssociatedDomain,entitlementsData["com.apple.developer.associated-domains"][0]));
                entitlementsData["com.apple.developer.associated-domains"][0] = Configurator.IOSAssociatedDomain;
            }

            if(!string.IsNullOrEmpty(Configurator.IOSAppIdentifier))
            {
                Information(string.Format("Writing app identifier: {0} {1}", Configurator.IOSAppIdentifier,entitlementsData["application-identifier"]));
                entitlementsData["application-identifier"] = Configurator.IOSAppIdentifier;
            } 

            SerializePlist(entitlementsPath, entitlementsData);
        } 
    });

Task("AppCenterRelease-iOSUpload")
    .WithCriteria(()=> Configurator.IsValidForiOSAppCenterDistribution)
    .Does(()=>{
        var ipaFilePattern = "./**/*iOS*.ipa";
        var foundIpaFiles = GetFiles(ipaFilePattern);

        if(foundIpaFiles.Any())
        {
            Information($"Appcenter upload ios owner {Configurator.AppCenterOwner}, appname {Configurator.AppCenteriOSAppName}, distribution {Configurator.AppCenterDistributionGroup}");
            
            AppCenterDistributeRelease(
                new AppCenterDistributeReleaseSettings() 
                { 
                    App = Configurator.AppCenterOwner + "/" + Configurator.AppCenteriOSAppName, 
                    File = foundIpaFiles.FirstOrDefault().ToString(),
                    Group = Configurator.AppCenterDistributionGroup
                });
        }

        // upload symbols
        var symbolsPattern = "./**/*.dSYM";
        var foundSymbols = GetDirectories(symbolsPattern);

        if(foundSymbols.Any())
        {
            var symbolsDirectory = foundSymbols.FirstOrDefault();
            Information(string.Format("Symbols directory found: {0}", symbolsDirectory.ToString()));

            AppCenterCrashesUploadSymbols(
                new AppCenterCrashesUploadSymbolsSettings() 
                { 
                    App = Configurator.AppCenterOwner + "/" + Configurator.AppCenteriOSAppName, 
                    Symbol = symbolsDirectory.ToString()
                });
        }
        else
        {
            Information("No symbols directory found!");
        }
    });

//Task("Release-iOS")
//    .IsDependentOn("Build-iOS")
//    .Does(() =>
//    {
//        Information($"## Release iOS");
//        
//        var uploader = "/Applications/Xcode.app/Contents/Applications/Application\ Loader.app/Contents/Frameworks/ITunesSoftwareService.framework/Support/altool"
//        
//        var ipaFilePattern = "./**/*iOS*.ipa";
//        var foundIpaFiles = GetFiles(ipaFilePattern);
//
//        if(foundIpaFiles.Any())
//        {
//            var ipaPath = foundIpaFiles.FirstOrDefault();
//            StartProcess(uploader, new ProcessSettings {
//                Arguments = new ProcessArgumentBuilder()
//                    .Append(@"--upload-app")
//                    .Append("-f")
//                    .Append(ipaPath)
//                    .Append("-u")
//                    .Append((environVarOrFail "AppStoreUser"))
//                    .Append("-p")
//                    .Append((environVarOrFail "AppStorePassword"))
//                }
//            );
//        }
//    });


Task("AppCenterRelease-iOS")
    .IsDependentOn("Build-iOS")
    .IsDependentOn("AppCenterLogin")
    .IsDependentOn("AppCenterRelease-iOSUpload")
    .IsDependentOn("AppCenterLogout")
    .WithCriteria(() => Configurator.IsValidForDroidAppCenterDistribution);

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

Task("AppCenterLogout")
    .Does(() => 
    {
        // TODO: move to settings
        AppCenterLogout(new AppCenterLogoutSettings { Token = Configurator.AppCenterToken });
    })
    .OnError(exception =>
    {
        Information(exception);

        Information("Make sure the appcenter cli tools are installed!");
        Information("npm install -g appcenter-cli");
    });


//////////////////////////////////////////////////////////////////////
// NUGET
//////////////////////////////////////////////////////////////////////

Task("UpdateNugetPackageVersion")   
    .WithCriteria(() => Configurator.IsValidForPushingPackage)
    .Does(() => 
    {
        if(!string.IsNullOrEmpty(Configurator.NugetRootProject)){
            Information("## Set Nuget package version");
            var projectFileContent = System.IO.File.ReadAllText(Configurator.NugetRootProject);
            
            Information($"For file :{Configurator.NugetRootProject}");
            
            Information($"Version :{Configurator.NugetFullPackageVersion}");
            
            projectFileContent = projectFileContent.Replace("<PackageVersion>1.0.0</PackageVersion>", $"<PackageVersion>{Configurator.NugetFullPackageVersion}</PackageVersion>"); 
            projectFileContent = projectFileContent.Replace("<PackageVersion></PackageVersion>", $"<PackageVersion>{Configurator.NugetFullPackageVersion}</PackageVersion>"); 
            System.IO.File.WriteAllText(Configurator.NugetRootProject, projectFileContent);
        }
    });

Task("CreateNugetBySpec")   
    .Does(() => 
    {
        Information($"## Valid nuspec {Configurator.IsValidForCustomNuspec}");
        if(Configurator.IsValidForCustomNuspec){
            Information($"## Create Nupkg {Configurator.NuspecFile}");

            var nuGetPackSettings = new NuGetPackSettings
	        {
		        IncludeReferencedProjects = true,
                Version = Configurator.NugetFullPackageVersion
	        };
            NuGetPack(Configurator.NuspecFile, nuGetPackSettings);
        }
    });

Task("PushNugetPackage")   
    .IsDependentOn("UpdateNugetPackageVersion")
    .IsDependentOn("Build")
    .IsDependentOn("CreateNugetBySpec")
    .WithCriteria(() => Configurator.IsValidForPushingPackage)
    .Does(() => 
    {
        Information("## PushNugetPackage");

        var path = string.Format("./**/{0}*.nupkg", Configurator.ProjectName);
        PublishNugetFromFolder(GetFiles(path));

        var pathRoot = string.Format("./*.nupkg", Configurator.ProjectName);
        PublishNugetFromFolder(GetFiles(pathRoot));
    });

private void PublishNugetFromFolder(FilePathCollection files)
{
    if(files.Any())
    {
        foreach (var file in files)
        {
            Information("Pushing " + file.ToString());
            NuGetPush(file, new NuGetPushSettings 
            {
                Source = Configurator.NugetUrl,
                ApiKey = Configurator.NugetToken
            });
        }
    }
}
    

//////////////////////////////////////////////////////////////////////
// TESTING
//////////////////////////////////////////////////////////////////////
Task("TestBuild")
    .IsDependentOn("Clean")
    .IsDependentOn("NuGetRestore")
    .Does(() =>
{
    MSBuild (Configurator.SolutionFile, c => {
		c.Configuration = Configurator.TestConfiguration;        
		c.MSBuildPlatform = Cake.Common.Tools.MSBuild.MSBuildPlatform.x86;
        c.MaxCpuCount = 0;
	});
});

Task("UnitTest")
    .IsDependentOn("TestBuild")
    .WithCriteria(() => Configurator.IsValidForRunningTests)
    .Does(() =>
{
    foreach(var testProject in Configurator.UnitTestProjects)
    {
        var outputFolder = $"--logger \"trx;LogFileName={Configurator.TestResultOutputFolder}/TestResults.xml\"";

        Information($"OUTPUT UNITTEST : {outputFolder}");
        DotNetCoreTest(
                testProject.File,
                new DotNetCoreTestSettings()
                {
                    Configuration = Configurator.TestConfiguration,
                    ArgumentCustomization = args => args.Append(outputFolder),
                    NoBuild = true
                });
    }
});

Task("SonarBegin")
    .WithCriteria(() => Configurator.IsValidForSonarQube)
    .Does(() => 
{
    Information($"SQ BEGIN {Configurator.ProjectName} with output {Configurator.TestResultOutputFolder}");

    SonarBegin(new SonarBeginSettings{
            Name = $"{Configurator.ProjectName}_{Configurator.SonarQubeBranch}",
            Key = $"{Configurator.ProjectName}_{Configurator.SonarQubeBranch}",
            Url = Configurator.SonarQubeUrl,
            Login = Configurator.SonarQubeToken,
            Verbose = true,
            CoverageExclusions = Configurator.SonarQubeExclusions,
            OpenCoverReportsPath= Configurator.OpenCoverOutputFolder
        });
});

Task("CoverletCoverage")
    .Does(() => 
{
    Func<IFileSystemInfo, bool> exclude_ui_tests = fileSystemInfo => !fileSystemInfo.Path.FullPath.Contains("UI");

    foreach(var testProject in Configurator.UnitTestProjects)
    {
        Information($"TESTING {testProject.File}");
        var coverletSettings = new CoverletSettings {
             CollectCoverage = true,
             CoverletOutputFormat = CoverletOutputFormat.opencover,
             CoverletOutputDirectory = Directory($"{Configurator.TestResultOutputFolder}"),
             CoverletOutputName = $"report",
             Exclude = new List<string>(){"[xunit.*]*"}
        };

        var projectFile = FilePath.FromString(testProject.File);
        
        Information($"COVERLET  {projectFile} {Configurator.TestConfiguration}");

        var testSettings = new DotNetCoreTestSettings {
            Configuration = Configurator.TestConfiguration,
            
        };//Verbosity =	DotNetCoreVerbosity.Quiet

        DotNetCoreTest(projectFile, testSettings, coverletSettings);
    }
});

Task("SonarEnd")
    .WithCriteria(() => Configurator.IsValidForSonarQube)
    .Does(() => 
{    
    //ReportGenerator(FilePath.FromString(@".\coverage-results\report.opencover.xml"), DirectoryPath.FromString(@".\coverage-results\"));

    Information($"SQ END");
    SonarEnd(new SonarEndSettings(){ Login = Configurator.SonarQubeToken});
});

Task("SonarQubeCoverage")
    .IsDependentOn("SonarBegin")
    //.IsDependentOn("UnitTest")
    .IsDependentOn("CoverletCoverage")
    .IsDependentOn("SonarEnd")
    .WithCriteria(() => Configurator.IsValidForSonarQube);


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


//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);