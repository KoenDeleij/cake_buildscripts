#load "../models/UnitTestProject.cake"

public static class Configurator
{
    /// Main

    public static string ProjectName { get; private set; }

    public static string SolutionFile { get; private set; }

    public static string AppDisplayName { get; private set; }

    public static string Version { get; private set; }

    public static string FullVersion { get; private set; }
    
    public static bool ShouldClean { get; private set; } 

    public static string BuildConfiguration { get; private set; }

    public static string AppPackageName { get; private set; }

    /// iOS

    public static string IOSProjectFile { get; private set; }

    public static string IOSSplashXib { get; private set; }

    public static string IOSAppIconsSet { get; private set; }

    public static string IOSURLSchema { get; private set; }

    public static string IOSAssociatedDomain { get; private set; }

    public static string IOSAppIdentifier { get; private set; }

    public static string IOSURLIdentifier { get; private set; }

    public static bool IsValidForBuildingIOS => !string.IsNullOrEmpty(IOSProjectFile) &&
                                                !string.IsNullOrEmpty(AppPackageName);

    /// Android

    public static string AndroidProjectFile { get; private set; }

    public static string AndroidKeystoreFile { get; private set; }

    public static string AndroidKeystoreAlias { get; private set; }

    public static string AndroidKeystorePassword { get; private set; }

    public static string AndroidStyle { get; private set; }

    public static string AndroidIcon { get; private set; }

    public static bool IsValidForBuildingAndroid => !string.IsNullOrEmpty(AndroidProjectFile) && 
                                                    !string.IsNullOrEmpty(AndroidKeystoreFile) &&
                                                    !string.IsNullOrEmpty(AndroidKeystoreAlias) &&
                                                    !string.IsNullOrEmpty(AndroidKeystorePassword) &&
                                                    !string.IsNullOrEmpty(AppPackageName);

    /// Tests
    
    public static List<UnitTestProject> UnitTestProjects { get; private set;}

    public static bool IsValidForRunningTests => UnitTestProjects != null &&
                                                    UnitTestProjects.Any();

    public static string TestResultOutputFolder => FilePath.FromString("TestResults").MakeAbsolute(_context.Environment).ToString();

    public static string OpenCoverOutputFolder => $"{Configurator.TestResultOutputFolder}/report.opencover.xml";

    public static string SonarQubeUrl { get; private set; }   
    
    public static string SonarQubeBranch { get; private set; }
    
    public static string SonarQubeToken { get; private set; }

    public static string SonarQubeExclusions => "**/*Should.cs,**/*Test.cs,**/*Tests.cs";

    public static bool IsValidForSonarQube => !string.IsNullOrEmpty(SonarQubeUrl) && 
                                              !string.IsNullOrEmpty(SonarQubeBranch) &&
                                              !string.IsNullOrEmpty(SonarQubeToken);

    public static string TestConfiguration { get; private set; }

    /// AppCenter 

    public static string AppCenterToken { get; private set; }

    public static string AppCenterOwner { get; private set; }

    public static string AppCenterDroidAppName { get; private set; }

    public static string AppCenteriOSAppName { get; private set; }

    public static string AppCenterDistributionGroup { get; private set; }

    public static bool IsValidForiOSAppCenterDistribution => 
        !string.IsNullOrEmpty(AppCenterToken) &&
        !string.IsNullOrEmpty(AppCenterOwner) &&
        !string.IsNullOrEmpty(AppCenteriOSAppName) &&
        !string.IsNullOrEmpty(AppCenterDistributionGroup);

    public static bool IsValidForDroidAppCenterDistribution => 
        !string.IsNullOrEmpty(AppCenterToken) &&
        !string.IsNullOrEmpty(AppCenterOwner) &&
        !string.IsNullOrEmpty(AppCenterDroidAppName) &&
        !string.IsNullOrEmpty(AppCenterDistributionGroup);

    /// Nuget

    public static string NugetUrl { get; private set; }

    public static string NugetToken { get; private set; }

    public static string NugetPackageVersion { get; private set; }

    public static string NugetRootProject { get; private set; }

    public static string NugetPreReleaseFlag { get; private set; }

    public static string NuspecFile { get; private set; }

    public static bool IsValidForPushingPackage => !string.IsNullOrEmpty(NugetUrl) && 
                                                   !string.IsNullOrEmpty(NugetToken) && 
                                                   !string.IsNullOrEmpty(NugetPackageVersion);

    public static bool IsValidForCustomNuspec =>   !string.IsNullOrEmpty(Configurator.NuspecFile) &&
                                                   !string.IsNullOrEmpty(NugetPackageVersion);

    public static string NugetFullPackageVersion => !string.IsNullOrEmpty(Configurator.NugetPreReleaseFlag)?$"{Configurator.NugetPackageVersion}-{Configurator.NugetPreReleaseFlag}":$"{Configurator.NugetPackageVersion}";


    private static ICakeContext _context;

    public static void Initialize(ICakeContext context)
    {
        _context = context;

        var currentDirectory = context.Environment.WorkingDirectory;
        
        ReadMainBuildSettings();
        
        ReadIOSBuildSettings();

        ReadDroidBuildSettings();

        ReadTestBuildSettings();

        ReadAppCenterSettings();

        ReadNugetSettings();

        ShowSettings();
    }

    private static void ShowSettings()
    {
        _context.Information(_context.Figlet(ProjectName));
        _context.Information("");
        _context.Information("============ Main ============");
        _context.Information(string.Format("Solution: {0}", !string.IsNullOrEmpty(SolutionFile) ? SolutionFile : "NOT FOUND"));
        _context.Information(string.Format("AppDisplayName: {0}", !string.IsNullOrEmpty(AppDisplayName) ? AppDisplayName : "NOT SET: app_display_name"));        
        _context.Information(string.Format("FullVersion: {0}", !string.IsNullOrEmpty(FullVersion) ? FullVersion : "NOT SET: buildversion"));
        _context.Information(string.Format("Version: {0}", !string.IsNullOrEmpty(Version) ? Version : "NOT SET: AppVersion"));
        _context.Information(string.Format("App bundle/package identifier: {0}", !string.IsNullOrEmpty(AppPackageName) ? AppPackageName : "NOT SET: app_packagename"));    
        _context.Information(string.Format("Cleaning: {0}", ShouldClean));
        _context.Information($"BuildConfiguration : {BuildConfiguration}");

        _context.Information("");
        _context.Information("============ iOS ============");
        _context.Information(string.Format("iOS project: {0}", !string.IsNullOrEmpty(IOSProjectFile) ? IOSProjectFile : "NOT FOUND"));
        _context.Information(string.Format("iOS Splash XIB: {0}", !string.IsNullOrEmpty(IOSSplashXib) ? IOSSplashXib : "NOT SET: ios_splash_xib"));    
        _context.Information(string.Format("iOS App Icons: {0}", !string.IsNullOrEmpty(IOSAppIconsSet) ? IOSAppIconsSet : "NOT SET: ios_appicons_set"));
        _context.Information(string.Format("iOS Bundle schemes: {0}", !string.IsNullOrEmpty(IOSURLSchema) ? IOSURLSchema : "NOT SET: ios_url_schema"));                
        _context.Information(string.Format("iOS Url identifier: {0}", !string.IsNullOrEmpty(IOSURLIdentifier) ? IOSURLIdentifier : "NOT SET: ios_url_identifier"));   
        _context.Information(string.Format("Configuration complete for building iOS: {0}", IsValidForBuildingIOS));

        _context.Information("");
        _context.Information("============ Droid ============");
        _context.Information(string.Format("Droid project: {0}", !string.IsNullOrEmpty(AndroidProjectFile) ? AndroidProjectFile : "NOT FOUND"));
        _context.Information(string.Format("Droid packagename: {0}", !string.IsNullOrEmpty(AndroidPackageName) ? AndroidPackageName : "NOT FOUND"));
        _context.Information(string.Format("Droid keystore: {0}", !string.IsNullOrEmpty(AndroidKeystoreFile) ? AndroidKeystoreFile : "NOT SET: android_keystorefile"));
        _context.Information(string.Format("Droid keystore alias: {0}", !string.IsNullOrEmpty(AndroidKeystoreAlias) ? AndroidKeystoreAlias : "NOT SET: android_keystorealias"));
        _context.Information(string.Format("Droid keystore password: {0}", !string.IsNullOrEmpty(AndroidKeystorePassword) ? "SET" : "NOT SET: android_keystorepasswd"));
        _context.Information(string.Format("Droid style: {0}", !string.IsNullOrEmpty(AndroidStyle) ? AndroidStyle : "NOT SET: android_style"));
        _context.Information(string.Format("Droid icon: {0}", !string.IsNullOrEmpty(AndroidIcon) ? AndroidIcon : "NOT SET: android_icon"));

        _context.Information(string.Format("Configuration complete for building Android: {0}", IsValidForBuildingAndroid));

        _context.Information("");
        _context.Information("============ Test ============");
        foreach(var testProject in UnitTestProjects)
        {
            _context.Information(string.Format("Test project: {0}", testProject.File));
            _context.Information(string.Format("Test project name: {0}", testProject.Name));            
            _context.Information(string.Format("Test directory: {0}", testProject.Directory));
        }

        _context.Information($"TestConfiguration : {TestConfiguration}");
        _context.Information($"TestResult outputFolder : {TestResultOutputFolder}");
        _context.Information($"OpenCover outputFolder : {OpenCoverOutputFolder}");

        _context.Information(string.Format("Configuration complete for running tests: {0}", IsValidForRunningTests));

        _context.Information("");
        _context.Information("============ AppCenter ============");
        _context.Information(string.Format("Token: {0}", !string.IsNullOrEmpty(AppCenterToken) ? AppCenterToken : "NOT SET: appcenter_token"));
        _context.Information(string.Format("Owner: {0}", !string.IsNullOrEmpty(AppCenterOwner) ? AppCenterOwner : "NOT SET: appcenter_owner"));
        _context.Information(string.Format("App name iOS: {0}", !string.IsNullOrEmpty(AppCenteriOSAppName) ? AppCenteriOSAppName : "NOT SET: appcenter_ios_appname"));
        _context.Information(string.Format("App name Droid: {0}", !string.IsNullOrEmpty(AppCenterDroidAppName) ? AppCenterDroidAppName : "NOT SET: appcenter_droid_appname"));
        _context.Information(string.Format("Distribution group: {0}", !string.IsNullOrEmpty(AppCenterDistributionGroup) ? AppCenterDistributionGroup : "NOT SET: appcenter_distributiongroup"));
        _context.Information(string.Format("Configuration complete for ios appcenter release: {0}", IsValidForiOSAppCenterDistribution));
        _context.Information(string.Format("Configuration complete for android appcenter release: {0}", IsValidForDroidAppCenterDistribution));

        _context.Information("");
        _context.Information("============ Nuget ============");
        _context.Information(string.Format("API: {0}", !string.IsNullOrEmpty(NugetUrl) ? NugetUrl : "NOT SET: nuget_url"));
        _context.Information(string.Format("Token: {0}", !string.IsNullOrEmpty(NugetToken) ? NugetToken : "NOT SET: nuget_token"));     
        _context.Information(string.Format("Configuration complete for pushing nuget packages: {0}", IsValidForPushingPackage));          
        _context.Information(string.Format("Nuget package version: {0}", NugetPackageVersion)); 
        _context.Information(string.Format("Nuspec: {0}", NuspecFile));  

        _context.Information("");
        _context.Information("===============================");
        _context.Information("");
    }

    private static void ReadMainBuildSettings()
    {
        SolutionFile = _context.EvaluateTfsBuildVariable("solution_file", _context.EnvironmentVariable("solution_file") ?? _context.Argument("solution_file", string.Empty));
        ProjectName = _context.EvaluateTfsBuildVariable("project_name", _context.EnvironmentVariable("project_name") ?? _context.Argument("project_name", string.Empty));
        AppDisplayName = _context.EvaluateTfsBuildVariable("app_display_name", _context.EnvironmentVariable("app_display_name") ?? _context.Argument("app_display_name", string.Empty));

        FullVersion = _context.EvaluateTfsBuildVariable("buildversion", _context.EnvironmentVariable("buildversion") ?? _context.Argument("buildversion", string.Empty));
        Version = _context.EvaluateTfsBuildVariable("AppVersion", _context.EnvironmentVariable("AppVersion") ?? _context.Argument("AppVersion", string.Empty));

        BuildConfiguration = _context.EvaluateTfsBuildVariable("configuration", _context.EnvironmentVariable("configuration") ?? _context.Argument("configuration", string.Empty));

        ShouldClean = _context.Argument("clean", false);

        if(string.IsNullOrEmpty(SolutionFile))
        {
            var solutionPath = "./**/*.sln";
            var solutionFiles = GlobbingAliases.GetFiles(_context, solutionPath);

            if(solutionFiles.Any())
            {
                SolutionFile = solutionFiles.FirstOrDefault().ToString();

                if(string.IsNullOrEmpty(ProjectName))
                    ProjectName = solutionFiles.FirstOrDefault().GetFilenameWithoutExtension().ToString();
            }
        }

        AppPackageName = _context.EvaluateTfsBuildVariable("app_packagename", _context.EnvironmentVariable("app_packagename") ?? _context.Argument("app_packagename", string.Empty));
    }

    private static void ReadIOSBuildSettings()
    {
        var iosPath = "./**/*iOS*.csproj";
        var iosFiles = GlobbingAliases.GetFiles(_context, iosPath);

        if(iosFiles.Any())
            IOSProjectFile = iosFiles.FirstOrDefault().ToString();
  
        IOSSplashXib = _context.EvaluateTfsBuildVariable("ios_splash_xib",  _context.EnvironmentVariable("ios_splash_xib") ??  _context.Argument("ios_splash_xib", string.Empty));            
        IOSAppIconsSet = _context.EvaluateTfsBuildVariable("ios_appicons_set",  _context.EnvironmentVariable("ios_appicons_set") ??  _context.Argument("ios_appicons_set", string.Empty));                    
        IOSURLSchema = _context.EvaluateTfsBuildVariable("ios_url_schema",  _context.EnvironmentVariable("ios_url_schema") ??  _context.Argument("ios_url_schema", string.Empty)); 
        IOSAssociatedDomain = _context.EvaluateTfsBuildVariable("ios_associateddomain",  _context.EnvironmentVariable("ios_associateddomain") ??  _context.Argument("ios_associateddomain", string.Empty)); 
        IOSAppIdentifier = _context.EvaluateTfsBuildVariable("ios_appidentifier",  _context.EnvironmentVariable("ios_appidentifier") ??  _context.Argument("ios_appidentifier", string.Empty)); 
    }

    private static void ReadDroidBuildSettings()
    {
        var droidPath = "./**/*Droid*.csproj";
        var droidFiles = GlobbingAliases.GetFiles(_context, droidPath);

        if(droidFiles.Any())
            AndroidProjectFile = droidFiles.FirstOrDefault().ToString();
        else
        {
            droidPath = "./**/*Android*.csproj";
            droidFiles = GlobbingAliases.GetFiles(_context, droidPath);

            if(droidFiles.Any())
            {
                AndroidProjectFile = droidFiles.FirstOrDefault().ToString();
            }
        }
        
        var storeFile = _context.EvaluateTfsBuildVariable("android_keystorefile", _context.EnvironmentVariable("android_keystorefile") ?? _context.Argument("android_keystorefile", string.Empty));

        if(string.IsNullOrEmpty(storeFile)){
            var keystorePath = "./**/*.keystore";
            var keystoreFiles = GlobbingAliases.GetFiles(_context, keystorePath);

            if(keystoreFiles.Any())
                AndroidKeystoreFile = keystoreFiles.FirstOrDefault().ToString();
        }else{
            AndroidKeystoreFile = FilePath.FromString(storeFile).MakeAbsolute(_context.Environment).ToString();
        }
        AndroidKeystoreAlias =  _context.EvaluateTfsBuildVariable("android_keystorealias",  _context.EnvironmentVariable("android_keystorealias") ??  _context.Argument("android_keystorealias", string.Empty));
        AndroidKeystorePassword =  _context.EvaluateTfsBuildVariable("android_keystorepasswd",  _context.EnvironmentVariable("android_keystorepasswd") ??  _context.Argument("android_keystorepasswd", string.Empty));            

        AndroidStyle = _context.EvaluateTfsBuildVariable("android_style", _context.EnvironmentVariable("android_style") ?? _context.Argument("android_style", string.Empty));
        AndroidIcon = _context.EvaluateTfsBuildVariable("android_icon", _context.EnvironmentVariable("android_icon") ?? _context.Argument("android_icon", string.Empty));
    }

    private static void ReadTestBuildSettings()
    {
        UnitTestProjects = new List<UnitTestProject>();

        var testProjectFile = _context.EvaluateTfsBuildVariable("test_solution_file", _context.EnvironmentVariable("test_solution_file") ?? _context.Argument("test_solution_file", string.Empty));
        var testProjectDirectory = _context.EvaluateTfsBuildVariable("test_solution_directory", _context.EnvironmentVariable("test_solution_directory") ?? _context.Argument("test_solution_directory", string.Empty));

        if(!string.IsNullOrEmpty(testProjectFile) && !string.IsNullOrEmpty(testProjectDirectory))
        {
            UnitTestProjects.Add(new UnitTestProject(testProjectFile, testProjectFile.Replace(".csproj", ""), testProjectDirectory));
        }
        else
        {
            Func<IFileSystemInfo, bool> exclude_ui_tests = fileSystemInfo => !fileSystemInfo.Path.FullPath.Contains("UI");

            var testPath = "./**/*Tests.csproj";
            var testFiles = GlobbingAliases.GetFiles(_context, testPath, exclude_ui_tests);

            if(testFiles.Any())
            {
                foreach(var testFile in testFiles)
                {
                    UnitTestProjects.Add(new UnitTestProject(testFile.ToString(), testFile.GetFilenameWithoutExtension().ToString(), testFile.GetDirectory().ToString()));
                }                
            }
        }    
        TestConfiguration = _context.EvaluateTfsBuildVariable("testconfiguration", _context.EnvironmentVariable("testconfiguration") ?? _context.Argument("testconfiguration", string.Empty));

        if(string.IsNullOrEmpty(TestConfiguration)){
            TestConfiguration = BuildConfiguration;
        }

        SonarQubeUrl = _context.EvaluateTfsBuildVariable("sonarqube_url", _context.EnvironmentVariable("sonarqube_url") ?? _context.Argument("sonarqube_url", string.Empty));    //"http://rhm-d-ranch01.boolhosting.tld:9000/""52ad219e8d1eec9bc631beb648e78fa0f6390425"
        SonarQubeBranch = _context.EvaluateTfsBuildVariable("sonarqube_branch", _context.EnvironmentVariable("sonarqube_branch") ?? _context.Argument("sonarqube_branch", string.Empty));
        SonarQubeToken = _context.EvaluateTfsBuildVariable("sonarqube_token", _context.EnvironmentVariable("sonarqube_token") ?? _context.Argument("sonarqube_token", string.Empty));    
    }

    private static void ReadAppCenterSettings()
    {
        AppCenterToken = _context.EvaluateTfsBuildVariable("appcenter_token", _context.EnvironmentVariable("appcenter_token") ?? _context.Argument("appcenter_token", string.Empty));
        AppCenterOwner = _context.EvaluateTfsBuildVariable("appcenter_owner", _context.EnvironmentVariable("appcenter_owner") ?? _context.Argument("appcenter_owner", string.Empty));
        AppCenteriOSAppName = _context.EvaluateTfsBuildVariable("appcenter_ios_appname", _context.EnvironmentVariable("appcenter_ios_appname") ?? _context.Argument("appcenter_ios_appname", string.Empty));
        AppCenterDroidAppName = _context.EvaluateTfsBuildVariable("appcenter_droid_appname", _context.EnvironmentVariable("appcenter_droid_appname") ?? _context.Argument("appcenter_droid_appname", string.Empty));
        AppCenterDistributionGroup = _context.EvaluateTfsBuildVariable("appcenter_distributiongroup", _context.EnvironmentVariable("appcenter_distributiongroup") ?? _context.Argument("appcenter_distributiongroup", string.Empty));
    }

    private static void ReadNugetSettings()
    {
        NugetUrl = _context.EvaluateTfsBuildVariable("nuget_url", _context.EnvironmentVariable("nuget_url") ?? _context.Argument("nuget_url", string.Empty));
        NuspecFile = _context.EvaluateTfsBuildVariable("nuget_spec", _context.EnvironmentVariable("nuget_spec") ?? _context.Argument("nuget_spec", string.Empty));
        NugetToken = _context.EvaluateTfsBuildVariable("nuget_token", _context.EnvironmentVariable("nuget_token") ?? _context.Argument("nuget_token", string.Empty));
        NugetPackageVersion = _context.EvaluateTfsBuildVariable("nuget_packageversion", _context.EnvironmentVariable("nuget_packageversion") ?? _context.Argument("nuget_packageversion", string.Empty));
        NugetRootProject = _context.EvaluateTfsBuildVariable("nuget_rootproject", _context.EnvironmentVariable("nuget_rootproject") ?? _context.Argument("nuget_rootproject", string.Empty));
        NugetPreReleaseFlag = _context.EvaluateTfsBuildVariable("nuget_prerelease_flag", _context.EnvironmentVariable("nuget_prerelease_flag") ?? _context.Argument("nuget_prerelease_flag", string.Empty));
    }
}