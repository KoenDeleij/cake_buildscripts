public class BuildConfiguration
{
    public string MainProjectName { get; set; }

    public string SolutionFile { get; set; }

    public string IOSProjectFile { get; set; }

    public string AndroidProjectFile { get; set; }

    public string TestProjectDirectory { get; set; }

    public string TestProjectFile { get; set; }

    public string NuspecFile { get; set; }

    public string AndroidKeystoreFile { get; set; }

    public string AndroidKeystoreAlias { get; set; }

    public string AndroidKeystorePassword { get; set; }

    public bool IsValidForAndroidSigning => 
        !string.IsNullOrEmpty(AndroidKeystoreFile) &&
        !string.IsNullOrEmpty(AndroidKeystoreAlias) &&
        !string.IsNullOrEmpty(AndroidKeystorePassword);


}

