public class UnitTestProject
{
    public string File { get; private set; }

    public string Name { get; private set; }

    public string Directory { get; private set; }

    

    public UnitTestProject(string file, string name, string directory)
    {
        File = file;
        Name = name;
        Directory = directory;
    }
}