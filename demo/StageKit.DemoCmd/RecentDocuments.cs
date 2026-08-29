namespace StageKit.DemoCmd;

public class RecentDocuments : RootCollectionFile<RecentDocuments, string>
{
    public RecentDocuments()
    {
        AutoSave = true;
        DirectoryPath = ApplicationKit.ConfigsPath;
    }
}
