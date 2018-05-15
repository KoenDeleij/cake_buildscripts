public static class Configurator
{
    /// Main

    public static string ProjectName { get; private set; }

    public static string SolutionFile { get; private set; }

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

    public static string AppCenterOwner { get; private set; }

    public static string AppCenterAppName { get; private set; }

    public static string AppCenterDistributionGroup { get; private set; }

    public static bool IsValidForAppCenterDistribution => 
        !string.IsNullOrEmpty(AppCenterOwner) &&
        !string.IsNullOrEmpty(AppCenterAppName) &&
        !string.IsNullOrEmpty(AppCenterDistributionGroup);

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

        ShowSettings();
    }

    private static void ShowSettings()
    {
        _context.Information(_context.Figlet(ProjectName));
        _context.Information("");
        _context.Information("============ Main ============");
        _context.Information(string.Format("Solution: {0}", !string.IsNullOrEmpty(SolutionFile) ? SolutionFile : "NOT FOUND"));
        _context.Information("");
        _context.Information("============ iOS ============");
        _context.Information(string.Format("iOS project: {0}", !string.IsNullOrEmpty(IOSProjectFile) ? IOSProjectFile : "NOT FOUND"));
        _context.Information(string.Format("Configuration complete for building iOS: {0}", IsValidForBuildingIOS));

        _context.Information("");
        _context.Information("============ Droid ============");
        _context.Information(string.Format("Droid project: {0}", !string.IsNullOrEmpty(AndroidProjectFile) ? AndroidProjectFile : "NOT SET"));
        _context.Information(string.Format("Droid keystore: {0}", !string.IsNullOrEmpty(AndroidKeystoreFile) ? AndroidKeystoreFile : "NOT SET"));
        _context.Information(string.Format("Droid keystore alias: {0}", !string.IsNullOrEmpty(AndroidKeystoreAlias) ? AndroidKeystoreAlias : "NOT SET"));
        _context.Information(string.Format("Droid keystore password: {0}", !string.IsNullOrEmpty(AndroidKeystoreAlias) ? "SET" : "NOT SET"));
        _context.Information(string.Format("Configuration complete for building Android: {0}", IsValidForBuildingAndroid));

        _context.Information("");
        _context.Information("============ Test ============");
        _context.Information(string.Format("Test project: {0}", !string.IsNullOrEmpty(TestProjectFile) ? TestProjectFile : "NOT FOUND"));
        _context.Information(string.Format("Test directory: {0}", !string.IsNullOrEmpty(TestProjectDirectory) ? TestProjectDirectory : "NOT SET"));
        _context.Information(string.Format("Configuration complete for running tests: {0}", IsValidForRunningTests));

        _context.Information("");
        _context.Information("============ AppCenter ============");
        _context.Information(string.Format("Owner: {0}", !string.IsNullOrEmpty(AppCenterOwner) ? AppCenterOwner : "NOT SET"));
        _context.Information(string.Format("App name: {0}", !string.IsNullOrEmpty(AppCenterAppName) ? AppCenterAppName : "NOT SET"));
        _context.Information(string.Format("Distribution group: {0}", !string.IsNullOrEmpty(AppCenterDistributionGroup) ? AppCenterDistributionGroup : "NOT SET"));
        _context.Information(string.Format("Configuration complete for appcenter release: {0}", IsValidForAppCenterDistribution));
    }

    private static void ReadMainBuildSettings()
    {
        SolutionFile = _context.EvaluateTfsBuildVariable("solution_file", _context.EnvironmentVariable("solution_file") ?? _context.Argument("solution_file", string.Empty));
        ProjectName = _context.EvaluateTfsBuildVariable("project_name", _context.EnvironmentVariable("project_name") ?? _context.Argument("project_name", string.Empty));
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
        AppCenterOwner = _context.EvaluateTfsBuildVariable("appcenter_owner", _context.EnvironmentVariable("appcenter_owner") ?? _context.Argument("appcenter_owner", string.Empty));
        AppCenterAppName = _context.EvaluateTfsBuildVariable("appcenter_appname", _context.EnvironmentVariable("appcenter_appname") ?? _context.Argument("appcenter_appname", string.Empty));
        AppCenterDistributionGroup = _context.EvaluateTfsBuildVariable("appcenter_distributiongroup", _context.EnvironmentVariable("appcenter_distributiongroup") ?? _context.Argument("appcenter_distributiongroup", string.Empty));
    }
}