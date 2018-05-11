public class AppCenterSettings
{
    public string Owner { get; set; }

    public string AppName { get; set; }

    public string DistributionGroup { get; set; }

    public bool IsValidForDistribution => 
        !string.IsNullOrEmpty(Owner) &&
        !string.IsNullOrEmpty(AppName) &&
        !string.IsNullOrEmpty(DistributionGroup);
}

