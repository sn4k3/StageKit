using System.Text.RegularExpressions;
using System.Xml.Linq;
using Fallout.Common.Utilities;
using StageKit.Primitives.Extensions;

namespace StageKit.Fallout;

/// <summary>
/// Generates desktop, AppImage, AppStream, and Flatpak files for a Linux application bundle.
/// </summary>
public static partial class LinuxAppBundle
{
    private const string FlatpakHostCommandPermission = "--talk-name=org.freedesktop.Flatpak";

    /// <summary>
    /// Identifies the upstream AppImage tooling repository.
    /// </summary>
    public const string AppImageToolRepositoryUrl = "https://github.com/AppImage/appimagetool";

    /// <summary>
    /// Generates a freedesktop desktop entry from the supplied options.
    /// </summary>
    /// <param name="options">The Linux bundle configuration.</param>
    /// <returns>A desktop entry document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A required option or list item is empty.</exception>
    public static string GetDesktopEntry(LinuxAppBundleOptions options)
    {
        return GetDesktopEntry(options, null, null);
    }

    internal static string GetDesktopEntry(LinuxAppBundleOptions options, string? executableName,
        string? iconName)
    {
        ValidateOptions(options);
        executableName ??= GetExecutableName(options);
        iconName ??= options.IconName ?? options.ProductName;

        ArgumentException.ThrowIfNullOrWhiteSpace(iconName);

        var mimeTypes = ResolveMimeTypes(options);
        var hasMimeTypes = mimeTypes.Count > 0;
        var execArgs = hasMimeTypes ? " %F" : string.Empty;
        var mimeTypeLine = hasMimeTypes
            ? $"\nMimeType={FormatDesktopList(mimeTypes, nameof(options.FileAssociations))}"
            : string.Empty;

        var desktopEntry = $$"""
                             [Desktop Entry]
                             Type=Application
                             Name={{EscapeDesktopValue(options.ProductName)}}
                             Comment={{EscapeDesktopValue(options.Summary)}}
                             Categories={{FormatDesktopList(options.Categories, nameof(options.Categories))}}
                             Keywords={{FormatDesktopList(options.Keywords, nameof(options.Keywords))}}
                             Icon={{EscapeDesktopValue(iconName)}}
                             Exec="{{EscapeDesktopExecDoubleQuoted(executableName)}}"{{execArgs}}{{mimeTypeLine}}
                             Terminal={{options.Terminal.ToString().ToLowerInvariant()}}
                             SingleMainWindow={{options.SingleMainWindow.ToString().ToLowerInvariant()}}
                             """;

        return AppendCustomBlock(desktopEntry, options.ExtraDesktopEntry).ReplaceLineEndings("\n");
    }

    /// <summary>
    /// Generates the AppImage <c>AppRun</c> launcher script from the supplied options.
    /// </summary>
    /// <param name="options">The Linux bundle configuration.</param>
    /// <returns>A Bash launcher script.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A required option is empty.</exception>
    public static string GetAppRunScript(LinuxAppBundleOptions options)
    {
        ValidateOptions(options);
        var executableName = GetExecutableName(options).EscapeBashDoubleQuoted();
        var customCode = string.IsNullOrWhiteSpace(options.AppRunScriptBeforeExec)
            ? string.Empty
            : $"{options.AppRunScriptBeforeExec}\n\n";

        return $$"""
                 #!/usr/bin/env bash

                 HERE="$(dirname "$(readlink -f "${0}")")"
                 export PATH="${HERE}/usr/bin/:${PATH}"

                 {{customCode}}exec "{{executableName}}" "$@"
                 """.ReplaceLineEndings("\n");
    }

    /// <summary>
    /// Generates an AppStream metadata document from the supplied options.
    /// </summary>
    /// <param name="options">The Linux bundle configuration.</param>
    /// <returns>An AppStream XML document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A required option is empty or a screenshot URL is invalid.</exception>
    public static string GetAppStreamMetadata(LinuxAppBundleOptions options)
    {
        ValidateOptions(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.MetadataLicense);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ContentRatingType);
        ValidateList(options.Controls, nameof(options.Controls));

        var component = new XElement(
            "component",
            new XAttribute("type", "desktop-application"),
            new XElement("id", options.ApplicationId),
            new XElement("name", options.ProductName),
            new XElement("metadata_license", options.MetadataLicense),
            new XElement("project_license", options.License),
            new XElement("content_rating", new XAttribute("type", options.ContentRatingType)),
            new XElement("summary", NormalizeSummary(options.Summary)),
            new XElement("description", new XElement("p", options.Description)),
            new XElement("categories", options.Categories.Select(category => new XElement("category", category))),
            new XElement("supports", options.Controls.Select(control => new XElement("control", control))));

        if (options.MinimumDisplayLength is not null)
        {
            if (options.MinimumDisplayLength <= 0)
            {
                throw new ArgumentException("The minimum display length must be greater than zero.",
                    nameof(options.MinimumDisplayLength));
            }

            component.Add(
                new XElement(
                    "recommends",
                    new XElement(
                        "display_length",
                        new XAttribute("compare", "ge"),
                        options.MinimumDisplayLength.Value)));
        }

        component.Add(new XElement("launchable", new XAttribute("type", "desktop-id"),
            $"{options.ApplicationId}.desktop"));

        var screenshots = CreateScreenshots(options.ScreenshotUrls);
        if (screenshots is not null)
        {
            component.Add(screenshots);
        }

        component.Add(
            new XElement("url", new XAttribute("type", "homepage"), options.RepositoryUrl),
            new XElement("developer", new XAttribute("id", options.DeveloperId),
                new XElement("name", options.Authors)));

        if (!string.IsNullOrWhiteSpace(options.UpdateContact))
        {
            component.Add(new XElement("update_contact", options.UpdateContact));
        }

        var provides = new XElement("provides", new XElement("binary", GetExecutableName(options)));
        foreach (var mimeType in ResolveMimeTypes(options))
        {
            provides.Add(new XElement("mediatype", mimeType));
        }

        component.Add(provides);

        var document = new XDocument(new XDeclaration("1.0", "UTF-8", null), component);
        return $"{document.Declaration}{Environment.NewLine}{document}".ReplaceLineEndings("\n");
    }

    /// <summary>
    /// Generates a Flatpak manifest for already-published application files.
    /// </summary>
    /// <param name="options">The Linux bundle configuration.</param>
    /// <returns>A YAML Flatpak manifest.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A required option or permission is empty.</exception>
    public static string GetFlatpakManifest(LinuxAppBundleOptions options)
    {
        ValidateOptions(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.FlatpakRuntime);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.FlatpakRuntimeVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.FlatpakSdk);
        ValidateList(options.FlatpakFinishArguments, nameof(options.FlatpakFinishArguments));

        var executableName = GetExecutableName(options);
        var sourceDirectory = options.ProductName;
        var executablePath = $"/app/bin/app-sources/{executableName}";
        IEnumerable<string> flatpakFinishArguments = options.FlatpakFinishArguments;
        if (options.FlatpakAllowHostCommandExecution &&
            !options.FlatpakFinishArguments.Contains(FlatpakHostCommandPermission, StringComparer.Ordinal))
        {
            flatpakFinishArguments = flatpakFinishArguments.Append(FlatpakHostCommandPermission);
        }

        var finishArguments = string.Join(
            Environment.NewLine,
            flatpakFinishArguments.Select(argument => $"  - {argument.SingleQuoteYaml()}"));

        return $$"""
                 app-id: {{options.ApplicationId.SingleQuoteYaml()}}
                 runtime: {{options.FlatpakRuntime.SingleQuoteYaml()}}
                 runtime-version: {{options.FlatpakRuntimeVersion.SingleQuoteYaml()}}
                 sdk: {{options.FlatpakSdk.SingleQuoteYaml()}}
                 command: {{executableName.SingleQuoteYaml()}}
                 finish-args:
                 {{finishArguments}}

                 modules:
                   - name: {{options.ProductName.SingleQuoteYaml()}}
                     buildsystem: simple
                     build-commands:
                       - {{"mkdir -p /app/bin".SingleQuoteYaml()}}
                       - {{"mkdir -p /app/share".SingleQuoteYaml()}}
                       - {{"mv ./app-sources /app/bin/app-sources".SingleQuoteYaml()}}
                       - {{$"chmod +x {executablePath.QuoteShell()}".SingleQuoteYaml()}}
                       - {{$"ln -s {executablePath.QuoteShell()} {$"/app/bin/{executableName}".QuoteShell()}".SingleQuoteYaml()}}
                       - {{"if [ -d /app/bin/app-sources/share ]; then mv /app/bin/app-sources/share/* /app/share/; fi".SingleQuoteYaml()}}
                     sources:
                       - type: dir
                         path: {{sourceDirectory.SingleQuoteYaml()}}
                         dest: app-sources
                 """.ReplaceLineEndings("\n");
    }

    private static XElement? CreateScreenshots(IEnumerable<string> screenshotUrls)
    {
        ArgumentNullException.ThrowIfNull(screenshotUrls);

        var screenshots = new XElement("screenshots");
        var index = 0;
        foreach (var screenshotUrl in screenshotUrls)
        {
            if (!Uri.TryCreate(screenshotUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https"))
            {
                throw new ArgumentException($"Screenshot URL at index {index} must be an absolute HTTP or HTTPS URL.",
                    nameof(screenshotUrls));
            }

            var screenshot = new XElement("screenshot", new XElement("image", screenshotUrl));
            if (index == 0)
            {
                screenshot.SetAttributeValue("type", "default");
            }

            screenshots.Add(screenshot);
            index++;
        }

        return index == 0 ? null : screenshots;
    }

    internal static List<string> ResolveMimeTypes(LinuxAppBundleOptions options)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var association in options.FileAssociations)
        {
            if (!string.IsNullOrWhiteSpace(association.MimeType))
            {
                set.Add(association.MimeType.Trim());
            }
            else
            {
                foreach (var extension in association.Extensions)
                {
                    var resolved = ResolveMimeTypeFromExtension(extension);
                    if (!string.IsNullOrWhiteSpace(resolved))
                    {
                        set.Add(resolved);
                    }
                }
            }
        }

        return set.ToList();
    }

    internal static string ResolveMimeTypeFromExtension(string extension)
    {
        var clean = extension.TrimStart('*').TrimStart('.').ToLowerInvariant();
        return clean switch
        {
            "" or "*" => string.Empty,

            // Archives and compressed formats
            "zip" => "application/zip",
            "tar" => "application/x-tar",
            "gz" or "tgz" => "application/gzip",
            "bz2" or "tbz2" => "application/x-bzip2",
            "xz" or "txz" => "application/x-xz",
            "zst" => "application/zstd",
            "7z" => "application/x-7z-compressed",
            "rar" => "application/vnd.rar",
            "iso" => "application/x-iso9660-image",
            "dmg" => "application/x-apple-diskimage",

            // Documents and text
            "txt" => "text/plain",
            "csv" => "text/csv",
            "tsv" => "text/tab-separated-values",
            "html" or "htm" => "text/html",
            "css" => "text/css",
            "js" or "mjs" => "text/javascript",
            "json" or "jsonc" => "application/json",
            "xml" => "application/xml",
            "yaml" or "yml" => "application/yaml",
            "md" or "markdown" => "text/markdown",
            "rtf" => "application/rtf",
            "pdf" => "application/pdf",
            "doc" => "application/msword",
            "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "xls" => "application/vnd.ms-excel",
            "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "ppt" => "application/vnd.ms-powerpoint",
            "pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "odt" => "application/vnd.oasis.opendocument.text",
            "ods" => "application/vnd.oasis.opendocument.spreadsheet",
            "odp" => "application/vnd.oasis.opendocument.presentation",

            // Images
            "png" => "image/png",
            "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "bmp" => "image/bmp",
            "webp" => "image/webp",
            "svg" => "image/svg+xml",
            "ico" => "image/x-icon",
            "tiff" or "tif" => "image/tiff",
            "avif" => "image/avif",
            "heic" or "heif" => "image/heif",

            // Audio
            "mp3" => "audio/mpeg",
            "wav" => "audio/wav",
            "ogg" or "oga" => "audio/ogg",
            "flac" => "audio/flac",
            "aac" => "audio/aac",
            "m4a" => "audio/x-m4a",
            "wma" => "audio/x-ms-wma",
            "opus" => "audio/opus",
            "mid" or "midi" => "audio/midi",

            // Video
            "mp4" or "m4v" => "video/mp4",
            "mkv" => "video/x-matroska",
            "webm" => "video/webm",
            "avi" => "video/x-msvideo",
            "mov" => "video/quicktime",
            "wmv" => "video/x-ms-wmv",
            "flv" => "video/x-flv",
            "ogv" => "video/ogg",

            // Fonts
            "ttf" => "font/ttf",
            "otf" => "font/otf",
            "woff" => "font/woff",
            "woff2" => "font/woff2",

            // 3D and models
            "stl" => "model/stl",
            "obj" => "model/obj",
            "gltf" => "model/gltf+json",
            "glb" => "model/gltf-binary",
            "3mf" => "model/3mf",

            _ => $"application/x-{clean}"
        };
    }

    private static void ValidateOptions(LinuxAppBundleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ApplicationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ProductName);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Description);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.License);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.RepositoryUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DeveloperId);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Authors);
        ValidateList(options.Categories, nameof(options.Categories));
    }

    private static string GetExecutableName(LinuxAppBundleOptions options)
    {
        var executableName = options.ExecutableName ?? options.ProductName;
        ArgumentException.ThrowIfNullOrWhiteSpace(executableName);
        return executableName;
    }

    private static void ValidateList(IEnumerable<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("List items cannot be null, empty, or white space.", parameterName);
        }
    }

    private static string FormatDesktopList(IEnumerable<string> values, string parameterName)
    {
        ValidateList(values, parameterName);
        var list = values as IReadOnlyCollection<string> ?? values.ToList();
        return list.Count == 0
            ? string.Empty
            : $"{string.Join(';', list.Select(EscapeDesktopListItem))};";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRunRegex();

    private static string NormalizeSummary(string value)
    {
        var summary = WhitespaceRunRegex().Replace(value, " ").Trim();
        return (summary.Length <= 78 ? summary : summary[..78].TrimEnd()).TrimEnd('.');
    }

    private static string AppendCustomBlock(string content, string? customBlock)
    {
        return string.IsNullOrWhiteSpace(customBlock) ? content : $"{content}\n{customBlock}";
    }

    private static string EscapeDesktopValue(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    private static string EscapeDesktopListItem(string value)
    {
        return EscapeDesktopValue(value).Replace(";", "\\;", StringComparison.Ordinal);
    }

    // The desktop-entry Exec field uses the same double-quoted escaping rules as Bash.
    private static string EscapeDesktopExecDoubleQuoted(string value)
    {
        return value.EscapeBashDoubleQuoted();
    }
}
