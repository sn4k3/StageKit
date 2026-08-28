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
        ValidateOptions(options);
        var executableName = GetExecutableName(options);
        var iconName = options.IconName ?? options.ProductName;

        ArgumentException.ThrowIfNullOrWhiteSpace(iconName);

        var desktopEntry = $$"""
                             [Desktop Entry]
                             Type=Application
                             Name={{EscapeDesktopValue(options.ProductName)}}
                             Comment={{EscapeDesktopValue(options.Summary)}}
                             Categories={{FormatDesktopList(options.Categories, nameof(options.Categories))}}
                             Keywords={{FormatDesktopList(options.Keywords, nameof(options.Keywords))}}
                             Icon={{EscapeDesktopValue(iconName)}}
                             Exec="{{EscapeDesktopExecDoubleQuoted(executableName)}}"
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
            new XElement("developer_name", options.Authors));

        if (!string.IsNullOrWhiteSpace(options.UpdateContact))
        {
            component.Add(new XElement("update_contact", options.UpdateContact));
        }

        component.Add(new XElement("provides", new XElement("binary", GetExecutableName(options))));

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
        var finishArguments = string.Join(
            Environment.NewLine,
            options.FlatpakFinishArguments.Select(argument => $"  - {argument.SingleQuoteYaml()}"));

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
                       - {{"mv ./app-sources /app/bin/app-sources".SingleQuoteYaml()}}
                       - {{$"chmod +x {executablePath.QuoteShell()}".SingleQuoteYaml()}}
                       - {{$"ln -s {executablePath.QuoteShell()} {$"/app/bin/{executableName}".QuoteShell()}".SingleQuoteYaml()}}
                     sources:
                       - type: dir
                         path: {{sourceDirectory.SingleQuoteYaml()}}
                         dest: app-sources
                 """.ReplaceLineEndings("\n");
    }

    private static XElement? CreateScreenshots(IReadOnlyList<string> screenshotUrls)
    {
        ArgumentNullException.ThrowIfNull(screenshotUrls);
        if (screenshotUrls.Count == 0)
        {
            return null;
        }

        var screenshots = new XElement("screenshots");
        for (var index = 0; index < screenshotUrls.Count; index++)
        {
            var screenshotUrl = screenshotUrls[index];
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
        }

        return screenshots;
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
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Authors);
        ValidateList(options.Categories, nameof(options.Categories));
    }

    private static string GetExecutableName(LinuxAppBundleOptions options)
    {
        var executableName = options.ExecutableName ?? options.ProductName;
        ArgumentException.ThrowIfNullOrWhiteSpace(executableName);
        return executableName;
    }

    private static void ValidateList(IReadOnlyList<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("List items cannot be null, empty, or white space.", parameterName);
        }
    }

    private static string FormatDesktopList(IReadOnlyList<string> values, string parameterName)
    {
        ValidateList(values, parameterName);
        return values.Count == 0
            ? string.Empty
            : $"{string.Join(';', values.Select(EscapeDesktopListItem))};";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRunRegex();

    private static string NormalizeSummary(string value)
    {
        var summary = WhitespaceRunRegex().Replace(value, " ").Trim();
        return summary.Length <= 78 ? summary : summary[..78].TrimEnd();
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