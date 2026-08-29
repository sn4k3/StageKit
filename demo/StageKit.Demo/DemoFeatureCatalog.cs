namespace StageKit.Demo;

public sealed record DemoFeature(string Name, string Description, IReadOnlyList<string> APIs);

public static class DemoFeatureCatalog
{
    public static IReadOnlyList<DemoFeature> All { get; } =
    [
        new("Runtime", "Inspect process, packaging, and runtime metadata.",
            ["ApplicationKit", "EntryApplication", "RuntimeDiagnostics"]),
        new("Settings", "Exercise atomic autosave settings and collection persistence.",
            ["RootSettingsFile<T>", "RootCollectionFile<T, TItem>"]),
        new("Storage", "Create backups and support bundles, manage onboarding, and apply retention.",
            ["ApplicationBackup", "SupportBundleExporter", "OnboardingStateFile", "ApplicationRetention"]),
        new("Updates", "Check GitHub releases and safely download a verified update asset.",
            ["UpdatumManager", "EntryApplication.GenericRuntimeIdentifier"])
    ];
}
