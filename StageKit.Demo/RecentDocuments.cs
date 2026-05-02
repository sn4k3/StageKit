namespace StageKit.Demo;

public class RecentDocuments : RootCollectionFile<RecentDocuments, string>
{
    public RecentDocuments()
    {
        AutoSave = true;
    }
}