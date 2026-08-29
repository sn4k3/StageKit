namespace StageKit.Demo;

public sealed class RecentDocuments : RootCollectionFile<RecentDocuments, string>
{
    public RecentDocuments()
    {
        AutoSave = true;
        DirectoryPath = ApplicationKit.ConfigsPath;
        TrimCollectionWhenExceeding = 10;
    }
}
