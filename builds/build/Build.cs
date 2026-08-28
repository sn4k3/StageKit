using Serilog;
using StageKit.Fallout;

internal class Build : StageKitBuild
{
    public Build()
    {
        BeforePublishRid = context =>
            Log.Information("Publishing {Rid} to {Path}",
                context.RuntimeIdentifier, context.PublishPath);

        // In this case, remove these tokens to MainProject to detect
        ExcludedProjectNameTokens.Remove("demo");
    }

    /// <summary>
    /// Gets the FakeApp product name, which differs from the solution name.
    /// </summary>
    public override string SoftwareName => MainProject.Name;

    /// <inheritdoc />
    protected override LinuxAppBundleOptions CreateLinuxAppBundleOptions()
    {
        var options = base.CreateLinuxAppBundleOptions();
        options.AppRunScriptBeforeExec = $$"""
                                           function help() {
                                              echo '   _____ __                   __ __ _ __' 
                                              echo '  / ___// /_____ _____ ____  / //_/(_) /_'
                                              echo '  \__ \/ __/ __ `/ __ `/ _ \/ ,<  / / __/'
                                              echo ' ___/ / /_/ /_/ / /_/ /  __/ /| |/ / /_'
                                              echo '/____/\__/\__,_/\__, /\___/_/ |_/_/\__/' 
                                                          /____/'
                                                    
                                               echo "
                                            --------------------------------------------------------------------------
                                               All the great {{SoftwareName}} functionality inside an AppImage package.
                                            --------------------------------------------------------------------------
                                            (This package uses the AppImage software packaging technology for Linux
                                             ['One App == One File'] for easy availability of the newest {{SoftwareName}}
                                             releases across all major Linux distributions.)
                                            Usage:  --help, -h
                                            ------     # This message
                                                    --appimage-extract
                                                       # Unpack this AppImage into a local sub-directory [currently named 'squashfs-root']
                                                    --appimage-help
                                                       # Show available AppImage options
                                           "
                                           }

                                           if [ "$1" == "--help" -o "$1" == "-h" ]; then
                                               help
                                               exit $?
                                           fi

                                           """;

        return options;
    }

    public new static int Main()
    {
        return Execute<Build>(x => x.Compile);
    }
}