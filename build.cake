#tool "nuget:?package=JetBrains.dotCover.CommandLineTools"
#tool "nuget:?package=NUnit.ConsoleRunner"
#tool "nuget:?package=NUnit.Extension.TeamCityEventListener"
#tool "nuget:?package=xunit.runner.console"
#tool "nuget:?package=MSBuild.SonarQube.Runner.Tool"
#tool "nuget:?package=ReportGenerator"
#tool "nuget:?package=Xamarin.UITest&version=3.0.0"

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
using System.Xml;

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

    var files = GetFiles("**/*.opencover.xml");

    foreach (var file in files)
    {
        DeleteFile(file.ToString());
    }

     CleanDirectory("./packages");
});

Task("NuGetRestore")
    .Does(()=>
    {
        Information("## Restoring " + Configurator.SolutionFile);
        DotNetCoreRestore(Configurator.SolutionFile);

        var files = GetFiles("**/*.csproj");

        foreach (var file in files)
        {
            Information($"## Restoring{file.ToString()}");
            //droid or iOS projects are typically not dotnet standard. they need a different restore.
            if(file.ToString().ToLower().Contains("droid") || file.ToString().ToLower().Contains("ios") || file.ToString().ToLower().Contains("touch"))
            {
                Information("## Restoring Oldscool" + file.ToString());
                NuGetRestore(file);
            }
        }
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

Task("Build-MultiTarget")
    .IsDependentOn("Clean")
    .IsDependentOn("NuGetRestore")
    .Does(() =>
{
    var settings = new DotNetCoreBuildSettings
    {
        Configuration = Configurator.BuildConfiguration
    };

    DotNetCoreBuild(Configurator.SolutionFile, settings);
});

Task("Test-Apps")
    //.IsDependentOn("UnitTest")
    //.IsDependentOn("MutationTest")
    .IsDependentOn("SonarQubeCoverage");
    
Task("Build-Apps")
    .IsDependentOn("UnitTest")
    .IsDependentOn("SonarQubeCoverage")
    .IsDependentOn("Build-Droid")
    .IsDependentOn("Build-iOS");

Task("Build-Apps-Appcenter")
    .IsDependentOn("UnitTest")
    .IsDependentOn("CoverletCoverage")
    .IsDependentOn("AppCenterLogin")
    .IsDependentOn("Build-iOS")
    .IsDependentOn("AppCenterRelease-iOSUpload")
    .IsDependentOn("Build-Droid")
    .IsDependentOn("AppCenterRelease-DroidUpload")
    .IsDependentOn("AppCenterLogout");

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

            Information($"Changing manifest {manifestPath}");

            var manifest = DeserializeAppManifest(manifestPath);

            //versioning
            manifest.VersionName = Configurator.FullVersion;
            manifest.VersionCode = int.Parse(Configurator.AndroidVersion.Replace(".",""));

            Information($"Changing manifest version name {manifest.VersionName} , code : {manifest.VersionCode}");

            //theming
            if(!string.IsNullOrEmpty(Configurator.AndroidStyle))
            {
                manifest.ApplicationTheme = Configurator.AndroidStyle;
                Information($"Changing manifest style {manifest.ApplicationTheme}");
            }

            if(!string.IsNullOrEmpty(Configurator.AndroidIcon))
            {
                manifest.ApplicationIcon = Configurator.AndroidIcon;
                Information($"Changing manifest icon {manifest.ApplicationIcon}");
            }

            //app name
            manifest.PackageName = Configurator.AppPackageName;
            manifest.ApplicationLabel = Configurator.AndroidDisplayName;

            Information($"Changing manifest packageName {manifest.PackageName}");
            Information($"Changing manifest app label {manifest.ApplicationLabel}");

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

        var apkFilePattern = "./**/*Signed.apk";
        var aabFilePattern = "./**/*Signed.aab";
        var foundBuildFiles = GetFiles(apkFilePattern);

        if(!foundBuildFiles.Any())
        {
            foundBuildFiles = GetFiles(aabFilePattern);
        }
        if(foundBuildFiles.Any())
        {
            Information($"Appcenter upload file {foundBuildFiles.FirstOrDefault().ToString()}");

            Information($"Appcenter upload droid owner {Configurator.AppCenterOwner}, appname {Configurator.AppCenterDroidAppName}, distribution {Configurator.AppCenterDistributionGroup}");
            AppCenterDistributeRelease(
                new AppCenterDistributeReleaseSettings() 
                { 
                    App = Configurator.AppCenterOwner + "/" + Configurator.AppCenterDroidAppName, 
                    File = foundBuildFiles.FirstOrDefault().ToString(),
                    Group = Configurator.AppCenterDistributionGroup,
                    Debug = true
                });
        }
    });

Task("AppCenterRelease-Droid")
    .IsDependentOn("Build-Droid")
    .IsDependentOn("AppCenterLogin")
    .IsDependentOn("AppCenterRelease-DroidUpload")
    .IsDependentOn("AppCenterLogout")
    .WithCriteria(() => Configurator.IsValidForDroidAppCenterDistribution);

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

            if(!string.IsNullOrEmpty(Configurator.IOSAPSEnvironment))
            {
                Information(string.Format("Writing aps environment: {0} {1}", Configurator.IOSAPSEnvironment,entitlementsData["aps-environment"]));
                entitlementsData["aps-environment"] = Configurator.IOSAPSEnvironment;
            }
            //else
            //{
            //    Information(string.Format("Writing aps environment: {0} {1}", "production",entitlementsData["aps-environment"]));
            //    entitlementsData["aps-environment"] = "production";
            //}

            SerializePlist(entitlementsPath, entitlementsData);
        } 

        //firebase
        Information("## SetIOS notification parameters");
        var firebaseConfigFilePattern = $"./**/{Configurator.IOSFirebaseConfigFile}";
        var foundConfigFiles = GetFiles(firebaseConfigFilePattern);
        if(foundConfigFiles.Any())
        {
            var firebasePath = foundConfigFiles.FirstOrDefault().ToString();
            dynamic firebaseData = DeserializePlist(firebasePath);

            if(!string.IsNullOrEmpty(Configurator.IOSFirebaseClientId))
            {
                Information(string.Format("Writing firebase client id: {0} {1}", Configurator.IOSFirebaseClientId,firebaseData["CLIENT_ID"]));
                firebaseData["CLIENT_ID"] = Configurator.IOSFirebaseClientId;
            }

            if(!string.IsNullOrEmpty(Configurator.IOSFirebaseReversedClientId))
            {
                Information(string.Format("Writing firebase client id: {0} {1}", Configurator.IOSFirebaseReversedClientId,firebaseData["REVERSED_CLIENT_ID"]));
                firebaseData["REVERSED_CLIENT_ID"] = Configurator.IOSFirebaseReversedClientId;
            }

            if(!string.IsNullOrEmpty(Configurator.AppPackageName))
            {
                Information(string.Format("Writing bundle identifier: {0}", Configurator.AppPackageName));
                firebaseData["BUNDLE_ID"] = Configurator.AppPackageName;
            }

            SerializePlist(firebasePath, firebaseData);
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
                    Group = Configurator.AppCenterDistributionGroup,
                    Debug = true
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
        Information($"Login with token {Configurator.AppCenterToken}");
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
        AppCenterLogout(new AppCenterLogoutSettings());//{ Token = Configurator.AppCenterToken }
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
    .IsDependentOn("UnitTest")
    .IsDependentOn("UpdateNugetPackageVersion")
    .IsDependentOn("Build")
    .IsDependentOn("CreateNugetBySpec")
    .IsDependentOn("PushPackage");

Task("PushNugetPackageWithSQ")   
    .IsDependentOn("UnitTest")
    //.IsDependentOn("MutationTest")
    .IsDependentOn("SonarQubeCoverage")
    .IsDependentOn("UpdateNugetPackageVersion")
    .IsDependentOn("Build")
    .IsDependentOn("CreateNugetBySpec")
    .IsDependentOn("PushPackage");

Task("PushMultiTargetNugetPackage")
    .IsDependentOn("UnitTest")
    .IsDependentOn("UpdateNugetPackageVersion")
    .IsDependentOn("Build-MultiTarget")
    .IsDependentOn("CreateNugetBySpec")
    .IsDependentOn("PushPackage");

Task("PushPackage")

    .WithCriteria(() => Configurator.IsValidForPushingPackage)
    .Does(() => 
    {
        Information("## PushNugetPackage");

        CleanDirectory("./packages");
        
        var path = string.Format("./**/{0}*.nupkg", Configurator.ProjectName);
        PublishNugetFromFolder(GetFiles(path));
    });

private bool PublishNugetFromFolder(FilePathCollection files)
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
        return true;
    }
    else
    {
        return false;
    }
}
    

//////////////////////////////////////////////////////////////////////
// TESTING
//////////////////////////////////////////////////////////////////////

bool IsTestProjectPath(SolutionProject solutionProject) => solutionProject.Path.ToString().Contains("Tests.csproj");

Task("MutationTest")
    .IsDependentOn("NugetRestore")
    .Does(()=>{
        StartProcess(new FilePath("dotnet"),new ProcessSettings(){
            Arguments = new ProcessArgumentBuilder()
                .Append("tool install -g dotnet-stryker")
        });

        Information($"STRYKER Solution: {Configurator.SolutionFile}");
        var solutionResult = ParseSolution(new FilePath(Configurator.SolutionFile)); 

        string oldWorkingDirectory = Configurator.CakeEnvironment.WorkingDirectory.ToString();

        foreach(var project in solutionResult.Projects)
        {
            if(IsTestProjectPath(project))
            {
                Information($"STRYKER : {project.Path.ToString()}");

                Configurator.CakeEnvironment.WorkingDirectory = project.Path.GetDirectory();

                var strykerPath = $"{project.Path.GetDirectory()}/StrykerOutput";
                var outputPath =  $"{project.Path.GetDirectory()}/Output";
                CleanDirectory(strykerPath);
                CleanDirectory(outputPath);

                XmlDocument doc = new XmlDocument();
                doc.Load(project.Path.ToString());
                var nodes = doc.SelectNodes("/Project/ItemGroup/ProjectReference/@Include");
                foreach (System.Xml.XmlAttribute node in nodes)
                {
                    var projReference = node.Value.ToString();
                    if(projReference.Contains(project.Name.Replace(".Tests","")))
                    {
                        var projectPath = new FilePath(projReference);
                        var projectName = projectPath.GetFilenameWithoutExtension().ToString();

                        Information($"STRYKER Project: {projectName}");
                        
                        StartProcess(new FilePath("dotnet"),new ProcessSettings(){
                        Arguments = new ProcessArgumentBuilder()
                            .Append("stryker")
                            .Append("-r")
                            .Append("\"['html']\"")
                            .Append("-p")
                            .Append(projectName)
                        });

                        var htmlReport = GetFiles($"{strykerPath}/**/*.html").FirstOrDefault();
                        var reportOutputLocation = $"{outputPath}/{projectName}.html";
                        Information($"STRYKER Copy: {htmlReport.ToString()} to {reportOutputLocation}");

                        EnsureDirectoryExists(new DirectoryPath(outputPath));

                        MoveFile(htmlReport,new FilePath(reportOutputLocation));
                    }
                }
            }
        }
        Information($"STRYKER set working directory back to {oldWorkingDirectory}");
        Configurator.CakeEnvironment.WorkingDirectory = new DirectoryPath(oldWorkingDirectory);
    });
    

Task("UnitTest")
    .IsDependentOn("Clean")
    .IsDependentOn("NuGetRestoreTests")
    .WithCriteria(() => Configurator.IsValidForRunningTests)
    .Does(() =>
{
    Information($"OUTPUT UNITTEST : {Configurator.SolutionFile}");

    var solutionResult = ParseSolution(new FilePath(Configurator.SolutionFile)); 

    foreach(var project in solutionResult.Projects)
    {
        if(IsTestProjectPath(project))
        {
            Information($"## Testing {project.Path}");
            DotNetCoreTest(
                project.Path.ToString() ,
                new DotNetCoreTestSettings()
                {
                    Configuration = Configurator.TestConfiguration,
                    Logger = $"trx;LogFileName={Configurator.TestResultOutputFolder}/{project.Name}_TestResults.xml",
                    ResultsDirectory = new DirectoryPath(Configurator.TestResultOutputFolder),
                    NoBuild = false,
                    NoRestore = false
                });
        }
    }
});

Task("NuGetRestoreTests")
    .Does(()=>
    {
        Information($"## Restoring Tests {Configurator.SolutionFile}");
        var solutionResult = ParseSolution(new FilePath(Configurator.SolutionFile)); 
        foreach(var project in solutionResult.Projects){
            if(project.Path.ToString().Contains("Tests.csproj")){
                Information($"## Restoring Tests {project.Path}");
                DotNetCoreRestore(project.Path.ToString());
            }
        }
    })

    .DeferOnError();

Task("SonarBegin")
    .WithCriteria(() => Configurator.IsValidForSonarQube)
    .Does(() => 
{
    Information($"SQ BEGIN {Configurator.ProjectName} with output {Configurator.TestResultOutputFolder}");

    var settings = new SonarBeginSettings{
        Name = $"{Configurator.ProjectName}_{Configurator.SonarQubeBranch}",
        Key = $"{Configurator.ProjectName}_{Configurator.SonarQubeBranch}",
        Url = Configurator.SonarQubeUrl,
        Login = Configurator.SonarQubeToken,
        Verbose = true,
        ArgumentCustomization = args => 
        {
            args.Append("/d:sonar.cs.opencover.reportsPaths=\"**/coverage.opencover.xml\"");
            args.Append("/d:sonar.scm.provider=\"git\"");
            if(!string.IsNullOrEmpty(Configurator.SonarQubeExclusions))
            {
                args.Append($"/d:sonar.exclusions=\"{Configurator.SonarQubeExclusions}\"");
            }
        }
    };

//CoverageExclusions = Configurator.SonarQubeExclusions,
    if(!string.IsNullOrEmpty(Configurator.SonarQubeInclusions))
    {
        settings.Inclusions = Configurator.SonarQubeInclusions;
    }

    SonarBegin(settings);
});

Task("CoverletCoverage")
    .Does(() => 
{
    var solutionResult = ParseSolution(new FilePath(Configurator.SolutionFile)); 
        var coverletSettings = new CoverletSettings {
            CollectCoverage = true,
            CoverletOutputFormat = CoverletOutputFormat.opencover,
            Exclude = new List<string>(){"[xunit.*]*","[*]*Should","[*]*Test","*/Data/Migrations/*"}
        };

        Information($"COVERLET  {Configurator.SolutionFile} {Configurator.TestConfiguration}");

        var testSettings = new DotNetCoreTestSettings {
            Configuration = Configurator.TestConfiguration,
        };//Verbosity = DotNetCoreVerbosity.Quiet

        DotNetCoreTest(Configurator.SolutionFile, testSettings, coverletSettings);
});

Task("SonarEnd")
    .WithCriteria(() => Configurator.IsValidForSonarQube)
    .Does(() => 
{    
    Information($"SQ END");
    SonarEnd(new SonarEndSettings(){ Login = Configurator.SonarQubeToken});
});

Task("SonarQubeCoverage")
    .IsDependentOn("SonarBegin")
    .IsDependentOn("CoverletCoverage")
    .IsDependentOn("SonarEnd")
    .WithCriteria(() => Configurator.IsValidForSonarQube);

Task("UITest-Build")
    .Does(()=>{
        NuGetRestore(Configurator.UITestProject);

        MSBuild (Configurator.UITestProject, c => {
            c.Configuration = Configurator.BuildConfiguration;        
            c.MaxCpuCount = 0;
        });
    });
    
Task("UITest-Droid-Upload")
    .IsDependentOn("AppCenterLogin")
    .Does(()=>{
        var apkFile = GetFiles("./**/*.apk").FirstOrDefault();

        Information($"Uploading for UITest {apkFile.ToString()}");

        AppCenterTestRunUitest(
            new AppCenterTestRunUitestSettings() 
            { 
                App = Configurator.AppCenterOwner + "/" + Configurator.AppCenterDroidAppName, 
                AppPath = apkFile.ToString(),
                Devices = Configurator.AppCenterUITestDeviceSet,
                TestSeries = Configurator.AppCenterUITestTestSeries,
                Locale = Configurator.AppCenterUITestLocale,
                BuildDir = Configurator.AppCenterUITestOutputPath,
                UitestToolsDir = "./tools/Xamarin.UITest.3.0.0/tools"

            });
    });

Task("UITest-Droid")
	.IsDependentOn("Build-Droid")
    .IsDependentOn("UITest-Build")
    .IsDependentOn("UITest-Droid-Upload");

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