public class UnitTestProject
{
    public string ProjectFile { get; private set; }

    public string ProjectDirectory { get; private set; }

    public string ProjectName { get; private set; }

    public UnitTestProject(string projectFile, string projectDirectory, string projectName)
    {
        ProjectFile = projectFile;
        ProjectDirectory = projectDirectory;
        ProjectName = projectName;
    }
}