public static class Configurator
{
    /// Main

    public static string ProjectName { get; private set; }

    public static string SolutionFile { get; private set; }

    public static string Version { get; private set; }

    public static string FullVersion { get; private set; }

    public static bool ShouldClean { get; private set; } 

    /// iOS

    public static string IOSProjectFile { get; private set; }

    public static bool IsValidForBuildingIOS => !string.IsNullOrEmpty(IOSProjectFile);

    /// Android

    public static string AndroidProjectFile { get; private set; }

    public static string AndroidKeystoreFile { get; private set; }

    public static string AndroidKeystoreAlias { get; private set; }

    public static string AndroidKeystorePassword { get; private set; }

    public static bool IsValidForBuildingAndroid => !string.IsNullOrEmpty(AndroidProjectFile) && 
                                                    !string.IsNullOrEmpty(AndroidKeystoreFile) &&
                                                    !string.IsNullOrEmpty(AndroidKeystoreAlias) &&
                                                    !string.IsNullOrEmpty(AndroidKeystorePassword);

    /// Tests
    
    public static string TestProjectFile { get; private set; }

    public static string TestProjectDirectory { get; private set; }

    public static bool IsValidForRunningTests => !string.IsNullOrEmpty(TestProjectFile) && 
                                                    !string.IsNullOrEmpty(TestProjectDirectory);

    /// AppCenter 

    public static string AppCenterToken { get; private set; }

    public static string AppCenterOwner { get; private set; }

    public static string AppCenterAppName { get; private set; }

    public static string AppCenterDistributionGroup { get; private set; }

    public static bool IsValidForAppCenterDistribution => 
        !string.IsNullOrEmpty(AppCenterToken) &&
        !string.IsNullOrEmpty(AppCenterOwner) &&
        !string.IsNullOrEmpty(AppCenterAppName) &&
        !string.IsNullOrEmpty(AppCenterDistributionGroup);

    /// Nuget

    public static string NugetUrl { get; private set; }

    public static string NugetToken { get; private set; }

    public static bool IsValidForPushingPackage => !string.IsNullOrEmpty(NugetUrl)&& 
                                                    !string.IsNullOrEmpty(NugetToken);

    ///

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
        _context.Information(string.Format("FullVersion: {0}", !string.IsNullOrEmpty(FullVersion) ? FullVersion : "NOT SET: buildversion"));
        _context.Information(string.Format("Version: {0}", !string.IsNullOrEmpty(Version) ? Version : "NOT SET: AppVersion"));
        _context.Information(string.Format("Cleaning: {0}", ShouldClean));
        _context.Information("");
        _context.Information("============ iOS ============");
        _context.Information(string.Format("iOS project: {0}", !string.IsNullOrEmpty(IOSProjectFile) ? IOSProjectFile : "NOT FOUND"));
        _context.Information(string.Format("Configuration complete for building iOS: {0}", IsValidForBuildingIOS));

        _context.Information("");
        _context.Information("============ Droid ============");
        _context.Information(string.Format("Droid project: {0}", !string.IsNullOrEmpty(AndroidProjectFile) ? AndroidProjectFile : "NOT FOUND"));
        _context.Information(string.Format("Droid keystore: {0}", !string.IsNullOrEmpty(AndroidKeystoreFile) ? AndroidKeystoreFile : "NOT SET: android_keystorefile"));
        _context.Information(string.Format("Droid keystore alias: {0}", !string.IsNullOrEmpty(AndroidKeystoreAlias) ? AndroidKeystoreAlias : "NOT SET: android_keystorealias"));
        _context.Information(string.Format("Droid keystore password: {0}", !string.IsNullOrEmpty(AndroidKeystorePassword) ? "SET" : "NOT SET: android_keystorepasswd"));
        _context.Information(string.Format("Configuration complete for building Android: {0}", IsValidForBuildingAndroid));

        _context.Information("");
        _context.Information("============ Test ============");
        _context.Information(string.Format("Test project: {0}", !string.IsNullOrEmpty(TestProjectFile) ? TestProjectFile : "NOT FOUND"));
        _context.Information(string.Format("Test directory: {0}", !string.IsNullOrEmpty(TestProjectDirectory) ? TestProjectDirectory : "NOT SET"));
        _context.Information(string.Format("Configuration complete for running tests: {0}", IsValidForRunningTests));

        _context.Information("");
        _context.Information("============ AppCenter ============");
        _context.Information(string.Format("Token: {0}", !string.IsNullOrEmpty(AppCenterToken) ? AppCenterToken : "NOT SET: appcenter_token"));
        _context.Information(string.Format("Owner: {0}", !string.IsNullOrEmpty(AppCenterOwner) ? AppCenterOwner : "NOT SET: appcenter_owner"));
        _context.Information(string.Format("App name: {0}", !string.IsNullOrEmpty(AppCenterAppName) ? AppCenterAppName : "NOT SET: appcenter_appname"));
        _context.Information(string.Format("Distribution group: {0}", !string.IsNullOrEmpty(AppCenterDistributionGroup) ? AppCenterDistributionGroup : "NOT SET: appcenter_distributiongroup"));
        _context.Information(string.Format("Configuration complete for appcenter release: {0}", IsValidForAppCenterDistribution));

        _context.Information("");
        _context.Information("============ Nuget ============");
        _context.Information(string.Format("API: {0}", !string.IsNullOrEmpty(NugetUrl) ? NugetUrl : "NOT SET: nuget_url"));
        _context.Information(string.Format("Token: {0}", !string.IsNullOrEmpty(NugetToken) ? NugetToken : "NOT SET: nuget_token"));     
        _context.Information(string.Format("Configuration complete for pushing nuget packages: {0}", IsValidForPushingPackage));          
    }

    private static void ReadMainBuildSettings()
    {
        SolutionFile = _context.EvaluateTfsBuildVariable("solution_file", _context.EnvironmentVariable("solution_file") ?? _context.Argument("solution_file", string.Empty));
        ProjectName = _context.EvaluateTfsBuildVariable("project_name", _context.EnvironmentVariable("project_name") ?? _context.Argument("project_name", string.Empty));
        FullVersion = _context.EvaluateTfsBuildVariable("buildversion", _context.EnvironmentVariable("buildversion") ?? _context.Argument("buildversion", string.Empty));
        Version = _context.EvaluateTfsBuildVariable("AppVersion", _context.EnvironmentVariable("AppVersion") ?? _context.Argument("AppVersion", string.Empty));

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
    }

    private static void ReadIOSBuildSettings()
    {
        var iosPath = "./**/*iOS*.csproj";
        var iosFiles = GlobbingAliases.GetFiles(_context, iosPath);

        if(iosFiles.Any())
            IOSProjectFile = iosFiles.FirstOrDefault().ToString();
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

        var keystorePath = "./**/*.keystore";
        var keystoreFiles = GlobbingAliases.GetFiles(_context, keystorePath);

        if(keystoreFiles.Any())
            AndroidKeystoreFile = keystoreFiles.FirstOrDefault().ToString();
        else
            AndroidKeystoreFile = _context.EvaluateTfsBuildVariable("android_keystorefile", _context.EnvironmentVariable("android_keystorefile") ?? _context.Argument("android_keystorefile", string.Empty));
        AndroidKeystoreAlias =  _context.EvaluateTfsBuildVariable("android_keystorealias",  _context.EnvironmentVariable("android_keystorealias") ??  _context.Argument("android_keystorealias", string.Empty));
        AndroidKeystorePassword =  _context.EvaluateTfsBuildVariable("android_keystorepasswd",  _context.EnvironmentVariable("android_keystorepasswd") ??  _context.Argument("android_keystorepasswd", string.Empty));            
    }

    private static void ReadTestBuildSettings()
    {
        var testPath = "./**/*.Tests.csproj";
        var testFiles = GlobbingAliases.GetFiles(_context, testPath);

        if(testFiles.Any())
        {
            TestProjectFile = testFiles.FirstOrDefault().ToString();
            TestProjectDirectory = testFiles.FirstOrDefault().GetDirectory().ToString();
        }
    }

    private static void ReadAppCenterSettings()
    {
        AppCenterToken = _context.EvaluateTfsBuildVariable("appcenter_token", _context.EnvironmentVariable("appcenter_token") ?? _context.Argument("appcenter_token", string.Empty));
        AppCenterOwner = _context.EvaluateTfsBuildVariable("appcenter_owner", _context.EnvironmentVariable("appcenter_owner") ?? _context.Argument("appcenter_owner", string.Empty));
        AppCenterAppName = _context.EvaluateTfsBuildVariable("appcenter_appname", _context.EnvironmentVariable("appcenter_appname") ?? _context.Argument("appcenter_appname", string.Empty));
        AppCenterDistributionGroup = _context.EvaluateTfsBuildVariable("appcenter_distributiongroup", _context.EnvironmentVariable("appcenter_distributiongroup") ?? _context.Argument("appcenter_distributiongroup", string.Empty));
    }

    private static void ReadNugetSettings()
    {
        NugetUrl = _context.EvaluateTfsBuildVariable("nuget_url", _context.EnvironmentVariable("nuget_url") ?? _context.Argument("nuget_url", string.Empty));
        NugetToken = _context.EvaluateTfsBuildVariable("nuget_token", _context.EnvironmentVariable("nuget_token") ?? _context.Argument("nuget_token", string.Empty));
    }
}