using StageKit.Primitives;
using StageKit.Runtime;

namespace StageKit.Fallout;

/// <summary>
/// Creates the Bash GitHub Releases installation script emitted by <see cref="StageKitBuild.GenerateInstallScript"/>.
/// </summary>
internal static class InstallScript
{
    private static readonly IReadOnlyDictionary<ApplicationPackagingType, string> PackageTypeNames =
        new Dictionary<ApplicationPackagingType, string>
        {
            [ApplicationPackagingType.Portable] = "portable",
            [ApplicationPackagingType.DotNetSingleFile] = "dotnet-single-file",
            [ApplicationPackagingType.LinuxAppImage] = "linux-appimage",
            [ApplicationPackagingType.LinuxFlatpak] = "linux-flatpak",
            [ApplicationPackagingType.LinuxSnap] = "linux-snap",
            [ApplicationPackagingType.LinuxDeb] = "linux-deb",
            [ApplicationPackagingType.LinuxRpm] = "linux-rpm",
            [ApplicationPackagingType.LinuxArchPackage] = "linux-arch",
            [ApplicationPackagingType.MacOSAppBundle] = "macos-app-bundle",
            [ApplicationPackagingType.MacOSDmg] = "macos-dmg",
            [ApplicationPackagingType.MacOSPkg] = "macos-pkg"
        };

    internal static bool SupportsAny(IEnumerable<ApplicationPackagingType> packagingTypes)
    {
        ArgumentNullException.ThrowIfNull(packagingTypes);
        return packagingTypes.Any(PackageTypeNames.ContainsKey);
    }

    internal static string Create(
        string repositoryUrl,
        string applicationName,
        string executableName,
        IReadOnlyCollection<ApplicationPackagingType> selectedPackagingTypes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(executableName);
        ArgumentNullException.ThrowIfNull(selectedPackagingTypes);

        applicationName = FileUtilities.ValidatePathLeafName(applicationName, nameof(applicationName));
        executableName = FileUtilities.ValidatePathLeafName(executableName, nameof(executableName));
        var repository = GetGitHubRepository(repositoryUrl);
        var selected = selectedPackagingTypes.ToHashSet();
        var packageTypes = ApplicationPackagingInfo.KnownPackagingTypes.Keys
            .Where(selected.Contains)
            .Where(PackageTypeNames.ContainsKey)
            .Select(packagingType => PackageTypeNames[packagingType])
            .ToArray();
        if (packageTypes.Length == 0)
        {
            throw new InvalidOperationException(
                "None of the selected Fallout packaging types can be installed by the generated Bash script.");
        }

        var packageTypeLines = string.Join('\n', packageTypes.Select(type => $"  '{type}'"));
        var applicationSlug = LinuxPackage.GetPackageName(applicationName);
        return Template
            .Replace("{{REPOSITORY}}", EscapeSingleQuoted(repository), StringComparison.Ordinal)
            .Replace("{{APPLICATION_NAME}}", EscapeSingleQuoted(applicationName), StringComparison.Ordinal)
            .Replace("{{APPLICATION_SLUG}}", EscapeSingleQuoted(applicationSlug), StringComparison.Ordinal)
            .Replace("{{EXECUTABLE_NAME}}", EscapeSingleQuoted(executableName), StringComparison.Ordinal)
            .Replace("{{PACKAGE_TYPES}}", packageTypeLines, StringComparison.Ordinal)
            .ReplaceLineEndings("\n") + '\n';
    }

    internal static string GetGitHubRepository(string repositoryUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryUrl);
        var value = repositoryUrl.Trim().TrimEnd('/');
        if (value.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
            value = $"https://github.com/{value["git@github.com:".Length..]}";

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"RepositoryUrl '{repositoryUrl}' is not a supported GitHub repository URL.");
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
        {
            throw new InvalidOperationException(
                $"RepositoryUrl '{repositoryUrl}' must identify a GitHub owner and repository.");
        }

        var repositoryName = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? segments[1][..^4]
            : segments[1];
        if (string.IsNullOrWhiteSpace(segments[0]) || string.IsNullOrWhiteSpace(repositoryName))
        {
            throw new InvalidOperationException(
                $"RepositoryUrl '{repositoryUrl}' must identify a GitHub owner and repository.");
        }

        return $"{segments[0]}/{repositoryName}";
    }

    private static string EscapeSingleQuoted(string value)
    {
        return value.Replace("'", "'\"'\"'", StringComparison.Ordinal);
    }

    private const string Template = """
                                            #!/usr/bin/env bash
                                            set -euo pipefail

                                            REPOSITORY='{{REPOSITORY}}'
                                            APPLICATION_NAME='{{APPLICATION_NAME}}'
                                            APPLICATION_SLUG='{{APPLICATION_SLUG}}'
                                            EXECUTABLE_NAME='{{EXECUTABLE_NAME}}'
                                            PACKAGE_TYPES=(
                                            {{PACKAGE_TYPES}}
                                            )

                                            show_header() {
                                              local script_name="${0##*/}"
                                              printf '\n============================================================\n'
                                              printf ' %s installer\n' "$APPLICATION_NAME"
                                              printf '============================================================\n'
                                              printf 'Usage:\n'
                                              printf '  %s [install] [latest|VERSION]\n' "$script_name"
                                              printf '  %s --version VERSION\n' "$script_name"
                                              printf '  %s --list\n' "$script_name"
                                              printf '  %s --list-changelog [LIMIT]\n' "$script_name"
                                              printf '  %s help\n' "$script_name"
                                              printf 'Commands:\n'
                                              printf '  install [VERSION]   Install or downgrade to the latest or selected version.\n'
                                              printf '  list                Show the available published release versions.\n'
                                              printf '  list-changelog [LIMIT]  Show release changelogs (default: 20 versions).\n'
                                              printf '  help                Show detailed help and examples.\n'
                                              printf 'Options:\n'
                                              printf '  -v, --version VERSION  Pick a release version, including an older version.\n'
                                              printf '  -l, --list             Show the available published release versions.\n'
                                              printf '  --list-changelog [LIMIT]  Show release changelogs (default: 20 versions).\n'
                                              printf '  -h, --help             Show detailed help.\n'
                                              printf '============================================================\n\n'
                                            }

                                            show_help() {
                                              show_header
                                              printf 'The installer selects the best compatible asset published for this system.\n'
                                              printf 'Selecting an older release tag allows an installed application to be downgraded.\n\n'
                                              printf 'Examples:\n'
                                              printf '  %s\n' "${0##*/}"
                                              printf '  %s install v1.2.3\n' "${0##*/}"
                                              printf '  %s --version 1.2.3\n' "${0##*/}"
                                              printf '  %s --list\n' "${0##*/}"
                                              printf '  %s --list-changelog\n' "${0##*/}"
                                              printf '  %s --list-changelog 5\n' "${0##*/}"
                                              printf '  %s help\n' "${0##*/}"
                                            }

                                            fail() {
                                              printf 'Error: %s\n' "$*" >&2
                                              printf 'Run %s help for usage.\n' "${0##*/}" >&2
                                              exit 1
                                            }

                                            command_exists() {
                                              command -v "$1" >/dev/null 2>&1
                                            }

                                            get_url() {
                                              local url="$1"
                                              case "$DOWNLOAD_TOOL" in
                                                curl)
                                                  curl -fsSL -H 'Accept: application/vnd.github+json' \
                                                    -H 'X-GitHub-Api-Version: 2022-11-28' "$url"
                                                  ;;
                                                wget)
                                                  wget -qO- --header='Accept: application/vnd.github+json' \
                                                    --header='X-GitHub-Api-Version: 2022-11-28' "$url"
                                                  ;;
                                              esac
                                            }

                                            download_file() {
                                              local url="$1"
                                              local output_file="$2"
                                              case "$DOWNLOAD_TOOL" in
                                                curl) curl -fL --retry 3 --output "$output_file" "$url" ;;
                                                wget) wget -q -t 3 -O "$output_file" "$url" ;;
                                              esac
                                            }

                                            run_elevated() {
                                              if [ "$(id -u)" -eq 0 ]; then
                                                "$@"
                                              elif command_exists sudo; then
                                                sudo "$@"
                                              else
                                                fail "Installing ${APPLICATION_NAME} requires root privileges or sudo."
                                              fi
                                            }

                                            normalize_architecture() {
                                              case "$(uname -m)" in
                                                x86_64|amd64) printf 'x64\n' ;;
                                                arm64|aarch64) printf 'arm64\n' ;;
                                                *) fail "Unsupported architecture: $(uname -m)" ;;
                                              esac
                                            }

                                            detect_linux_native_package() {
                                              local distribution_ids=''
                                              if [ -r /etc/os-release ]; then
                                                distribution_ids="$(awk -F= '/^(ID|ID_LIKE)=/ { gsub(/\"/, "", $2); printf "%s ", tolower($2) }' /etc/os-release)"
                                              fi

                                              case "$distribution_ids" in
                                                *debian*|*ubuntu*) printf 'linux-deb\n'; return ;;
                                                *fedora*|*rhel*|*centos*|*suse*) printf 'linux-rpm\n'; return ;;
                                                *arch*|*manjaro*) printf 'linux-arch\n'; return ;;
                                              esac

                                              if command_exists apt-get || command_exists apt; then
                                                printf 'linux-deb\n'
                                              elif command_exists dnf5 || command_exists dnf || command_exists yum || command_exists zypper; then
                                                printf 'linux-rpm\n'
                                              elif command_exists pacman; then
                                                printf 'linux-arch\n'
                                              elif command_exists dpkg; then
                                                printf 'linux-deb\n'
                                              elif command_exists rpm; then
                                                printf 'linux-rpm\n'
                                              fi
                                            }

                                            package_is_compatible() {
                                              case "$1" in
                                                portable) [ "$PLATFORM" = 'linux' ] && command_exists unzip ;;
                                                dotnet-single-file|linux-appimage) [ "$PLATFORM" = 'linux' ] ;;
                                                linux-flatpak) [ "$PLATFORM" = 'linux' ] && command_exists flatpak ;;
                                                linux-snap) [ "$PLATFORM" = 'linux' ] && command_exists snap ;;
                                                linux-deb) [ "$LINUX_NATIVE_PACKAGE" = 'linux-deb' ] && command_exists dpkg ;;
                                                linux-rpm) [ "$LINUX_NATIVE_PACKAGE" = 'linux-rpm' ] && command_exists rpm ;;
                                                linux-arch) [ "$LINUX_NATIVE_PACKAGE" = 'linux-arch' ] && command_exists pacman ;;
                                                macos-app-bundle|macos-dmg|macos-pkg) [ "$PLATFORM" = 'macos' ] ;;
                                                *) return 1 ;;
                                              esac
                                            }

                                            list_versions() {
                                              local releases_json page_versions versions version release_count page
                                              versions=''
                                              page=1
                                              while true; do
                                                if ! releases_json="$(get_url \
                                                  "https://api.github.com/repos/${REPOSITORY}/releases?per_page=100&page=${page}")"; then
                                                  fail "Unable to retrieve available versions from ${REPOSITORY}."
                                                fi

                                                page_versions="$(printf '%s\n' "$releases_json" |
                                                  grep -o '"tag_name"[[:space:]]*:[[:space:]]*"[^"]*"' |
                                                  sed 's/^"tag_name"[[:space:]]*:[[:space:]]*"//; s/"$//')"
                                                [ -n "$page_versions" ] || break
                                                if [ -n "$versions" ]; then
                                                  versions="${versions}"$'\n'
                                                fi
                                                versions="${versions}${page_versions}"
                                                release_count="$(printf '%s\n' "$page_versions" | wc -l | tr -d ' ')"
                                                [ "$release_count" -eq 100 ] || break
                                                page=$((page + 1))
                                              done

                                              [ -n "$versions" ] || fail "No published release versions were found for ${REPOSITORY}."
                                              printf 'Available versions for %s:\n' "$APPLICATION_NAME"
                                              while IFS= read -r version; do
                                                printf '  %s\n' "$version"
                                              done <<< "$versions"
                                            }

                                            print_release_changelogs() {
                                              local limit="$1"
                                              if command_exists jq; then
                                                jq -r --argjson limit "$limit" '.[0:$limit][] |
                                                  select(.tag_name != null and .tag_name != "") |
                                                  "\n# \(.tag_name | sub("^[vV]"; ""))\n\n\(if (.body // "") == "" then "No changelog provided." else .body end)"'
                                              elif command_exists python3; then
                                                python3 -c '
                                            import json
                                            import sys

                                            for release in json.load(sys.stdin)[:int(sys.argv[1])]:
                                                tag = release.get("tag_name")
                                                if not tag:
                                                    continue
                                                version = tag[1:] if tag[:1].lower() == "v" else tag
                                                body = release.get("body") or "No changelog provided."
                                                print(f"\n# {version}\n\n{body}")
                                            ' "$limit"
                                              else
                                                fail 'jq or python3 is required to display release changelogs.'
                                              fi
                                            }

                                            list_changelogs() {
                                              local releases_json release_count page page_size remaining shown_count
                                              page=1
                                              shown_count=0
                                              page_size="$CHANGELOG_LIMIT"
                                              if [ "$page_size" -gt 100 ]; then
                                                page_size=100
                                              fi
                                              printf 'Published changelog for %s:\n' "$APPLICATION_NAME"
                                              while [ "$shown_count" -lt "$CHANGELOG_LIMIT" ]; do
                                                if ! releases_json="$(get_url \
                                                  "https://api.github.com/repos/${REPOSITORY}/releases?per_page=${page_size}&page=${page}")"; then
                                                  fail "Unable to retrieve release changelogs from ${REPOSITORY}."
                                                fi

                                                release_count="$(printf '%s\n' "$releases_json" |
                                                  grep -c '"tag_name"[[:space:]]*:' || true)"
                                                [ "$release_count" -gt 0 ] || break
                                                remaining=$((CHANGELOG_LIMIT - shown_count))
                                                printf '%s\n' "$releases_json" | print_release_changelogs "$remaining"
                                                if [ "$release_count" -ge "$remaining" ]; then
                                                  shown_count="$CHANGELOG_LIMIT"
                                                else
                                                  shown_count=$((shown_count + release_count))
                                                fi
                                                [ "$shown_count" -lt "$CHANGELOG_LIMIT" ] || break
                                                [ "$release_count" -eq "$page_size" ] || break
                                                page=$((page + 1))
                                              done

                                              [ "$shown_count" -gt 0 ] ||
                                                fail "No published release changelogs were found for ${REPOSITORY}."
                                            }

                                            parse_arguments() {
                                              ACTION='install'
                                              VERSION='latest'
                                              CHANGELOG_LIMIT='20'
                                              case "$#" in
                                                0) ;;
                                                1)
                                                  case "$1" in
                                                    help|-h|--help) show_help; exit 0 ;;
                                                    list|-l|--list) ACTION='list' ;;
                                                    list-changelog|--list-changelog) ACTION='list-changelog' ;;
                                                    --list-changelog=*) ACTION='list-changelog'; CHANGELOG_LIMIT="${1#*=}" ;;
                                                    install) ;;
                                                    -v|--version) fail "$1 requires a VERSION." ;;
                                                    --version=*) VERSION="${1#*=}" ;;
                                                    *) VERSION="$1" ;;
                                                  esac
                                                  ;;
                                                2)
                                                  case "$1" in
                                                    install|-v|--version) VERSION="$2" ;;
                                                    list-changelog|--list-changelog) ACTION='list-changelog'; CHANGELOG_LIMIT="$2" ;;
                                                    *) fail "Unknown command or option: $1" ;;
                                                  esac
                                                  ;;
                                                *) fail 'Too many arguments.' ;;
                                              esac

                                              [ -n "$VERSION" ] || fail 'VERSION cannot be empty.'
                                              if [ "$ACTION" = 'list-changelog' ]; then
                                                [[ "$CHANGELOG_LIMIT" =~ ^[1-9][0-9]*$ ]] &&
                                                  [ "${#CHANGELOG_LIMIT}" -le 9 ] ||
                                                  fail 'LIMIT must be a positive integer with at most 9 digits.'
                                              fi
                                            }

                                            package_extension() {
                                              case "$1" in
                                                portable|macos-app-bundle) printf '.zip\n' ;;
                                                dotnet-single-file) printf '.bin\n' ;;
                                                linux-appimage) printf '.AppImage\n' ;;
                                                linux-flatpak) printf '.flatpak\n' ;;
                                                linux-snap) printf '.snap\n' ;;
                                                linux-deb) printf '.deb\n' ;;
                                                linux-rpm) printf '.rpm\n' ;;
                                                linux-arch) printf '.pkg.tar.zst\n' ;;
                                                macos-dmg) printf '.dmg\n' ;;
                                                macos-pkg) printf '.pkg\n' ;;
                                                *) return 1 ;;
                                              esac
                                            }

                                            find_asset_url() {
                                              local package_type="$1"
                                              local extension rid url name
                                              extension="$(package_extension "$package_type")"

                                              if [ "$PLATFORM" = 'macos' ]; then
                                                for rid in 'osx-multiarch' "osx-${ARCHITECTURE}"; do
                                                  while IFS= read -r url; do
                                                    [ -n "$url" ] || continue
                                                    name="${url##*/}"
                                                    if [ "$package_type" = 'dotnet-single-file' ]; then
                                                      case "$name" in
                                                        *.zip|*.AppImage|*.flatpak|*.snap|*.deb|*.rpm|*.pkg.tar.zst|*.dmg|*.pkg) continue ;;
                                                      esac
                                                    fi
                                                    case "$name" in
                                                      *"${rid}"*"${extension}") printf '%s\n' "$url"; return 0 ;;
                                                    esac
                                                  done <<< "$ASSET_URLS"
                                                done
                                              else
                                                rid="linux-${ARCHITECTURE}"
                                                while IFS= read -r url; do
                                                  [ -n "$url" ] || continue
                                                  name="${url##*/}"
                                                  if [ "$package_type" = 'dotnet-single-file' ]; then
                                                    case "$name" in
                                                      *.zip|*.AppImage|*.flatpak|*.snap|*.deb|*.rpm|*.pkg.tar.zst|*.dmg|*.pkg) continue ;;
                                                    esac
                                                  fi
                                                  case "$name" in
                                                    *"${rid}"*"${extension}") printf '%s\n' "$url"; return 0 ;;
                                                  esac
                                                done <<< "$ASSET_URLS"
                                              fi

                                              return 1
                                            }

                                            select_package() {
                                              local package_type candidate
                                              for package_type in "${PACKAGE_TYPES[@]}"; do
                                                package_is_compatible "$package_type" || continue
                                                if candidate="$(find_asset_url "$package_type")"; then
                                                  SELECTED_PACKAGE_TYPE="$package_type"
                                                  SELECTED_ASSET_URL="$candidate"
                                                  return 0
                                                fi
                                              done

                                              return 1
                                            }

                                            install_portable() {
                                              command_exists unzip || fail 'unzip is required to install the portable archive.'
                                              local destination="${XDG_DATA_HOME:-$HOME/.local/share}/${APPLICATION_SLUG}"
                                              local executable_path
                                              rm -rf "${destination}.new"
                                              mkdir -p "${destination}.new" "$HOME/.local/bin"
                                              unzip -q "$ASSET_FILE" -d "${destination}.new"
                                              executable_path="$(find "${destination}.new" -type f -name "$EXECUTABLE_NAME" -print -quit)"
                                              [ -n "$executable_path" ] || fail "The archive does not contain ${EXECUTABLE_NAME}."
                                              chmod +x "$executable_path"
                                              rm -rf "$destination"
                                              mv "${destination}.new" "$destination"
                                              executable_path="$(find "$destination" -type f -name "$EXECUTABLE_NAME" -print -quit)"
                                              ln -sfn "$executable_path" "$HOME/.local/bin/$EXECUTABLE_NAME"
                                            }

                                            install_single_file() {
                                              mkdir -p "$HOME/.local/bin"
                                              install -m 0755 "$ASSET_FILE" "$HOME/.local/bin/$EXECUTABLE_NAME"
                                            }

                                            install_appimage() {
                                              mkdir -p "$HOME/Applications"
                                              install -m 0755 "$ASSET_FILE" "$HOME/Applications/${APPLICATION_SLUG}.AppImage"
                                            }

                                            install_flatpak() {
                                              flatpak install --user --noninteractive --or-update "$ASSET_FILE"
                                            }

                                            install_snap() {
                                              run_elevated snap install --dangerous "$ASSET_FILE"
                                            }

                                            install_deb() {
                                              if command_exists apt-get; then
                                                run_elevated apt-get install --yes --allow-downgrades "$ASSET_FILE"
                                              else
                                                run_elevated dpkg --install --force-downgrade "$ASSET_FILE"
                                              fi
                                            }

                                            install_rpm() {
                                              run_elevated rpm --upgrade --replacepkgs --oldpackage "$ASSET_FILE"
                                            }

                                            install_arch() {
                                              run_elevated pacman -U --noconfirm "$ASSET_FILE"
                                            }

                                            copy_macos_application() {
                                              local app_path="$1"
                                              [ -d "$app_path" ] || fail "The package does not contain ${APPLICATION_NAME}.app."
                                              if [ -w /Applications ]; then
                                                ditto "$app_path" "/Applications/${APPLICATION_NAME}.app"
                                                xattr -dr com.apple.quarantine "/Applications/${APPLICATION_NAME}.app" 2>/dev/null || true
                                              else
                                                run_elevated ditto "$app_path" "/Applications/${APPLICATION_NAME}.app"
                                                run_elevated xattr -dr com.apple.quarantine "/Applications/${APPLICATION_NAME}.app" 2>/dev/null || true
                                              fi
                                            }

                                            install_macos_app_bundle() {
                                              local extract_directory="$TEMP_DIRECTORY/app"
                                              mkdir -p "$extract_directory"
                                              ditto -x -k "$ASSET_FILE" "$extract_directory"
                                              copy_macos_application "$(find "$extract_directory" -maxdepth 2 -type d -name "${APPLICATION_NAME}.app" -print -quit)"
                                            }

                                            install_macos_dmg() {
                                              MOUNT_DIRECTORY="$TEMP_DIRECTORY/dmg"
                                              mkdir -p "$MOUNT_DIRECTORY"
                                              hdiutil attach "$ASSET_FILE" -nobrowse -readonly -mountpoint "$MOUNT_DIRECTORY" >/dev/null
                                              copy_macos_application "$(find "$MOUNT_DIRECTORY" -maxdepth 2 -type d -name "${APPLICATION_NAME}.app" -print -quit)"
                                              hdiutil detach "$MOUNT_DIRECTORY" -quiet
                                              MOUNT_DIRECTORY=''
                                            }

                                            install_macos_pkg() {
                                              run_elevated installer -pkg "$ASSET_FILE" -target /
                                            }

                                            install_selected_package() {
                                              case "$SELECTED_PACKAGE_TYPE" in
                                                portable) install_portable ;;
                                                dotnet-single-file) install_single_file ;;
                                                linux-appimage) install_appimage ;;
                                                linux-flatpak) install_flatpak ;;
                                                linux-snap) install_snap ;;
                                                linux-deb) install_deb ;;
                                                linux-rpm) install_rpm ;;
                                                linux-arch) install_arch ;;
                                                macos-app-bundle) install_macos_app_bundle ;;
                                                macos-dmg) install_macos_dmg ;;
                                                macos-pkg) install_macos_pkg ;;
                                                *) fail "Unsupported package type: $SELECTED_PACKAGE_TYPE" ;;
                                              esac
                                            }

                                            parse_arguments "$@"
                                            show_header
                                            if command_exists curl; then
                                              DOWNLOAD_TOOL='curl'
                                            elif command_exists wget; then
                                              DOWNLOAD_TOOL='wget'
                                            else
                                              fail 'curl or wget is required to download GitHub release assets.'
                                            fi
                                            if [ "$ACTION" = 'list' ]; then
                                              list_versions
                                              exit 0
                                            fi
                                            if [ "$ACTION" = 'list-changelog' ]; then
                                              list_changelogs
                                              exit 0
                                            fi

                                            case "$(uname -s)" in
                                              Linux) PLATFORM='linux' ;;
                                              Darwin) PLATFORM='macos' ;;
                                              *) fail "Unsupported operating system: $(uname -s)" ;;
                                            esac
                                            ARCHITECTURE="$(normalize_architecture)"
                                            LINUX_NATIVE_PACKAGE=''
                                            if [ "$PLATFORM" = 'linux' ]; then
                                              LINUX_NATIVE_PACKAGE="$(detect_linux_native_package)"
                                            fi

                                            if [ "$VERSION" = 'latest' ]; then
                                              RELEASE_API_URL="https://api.github.com/repos/${REPOSITORY}/releases/latest"
                                            else
                                              if [[ ! "$VERSION" =~ ^v?[0-9]+(\.[0-9]+){1,3}([-+][0-9A-Za-z.-]+)?$ ]]; then
                                                fail "Invalid version '${VERSION}'. Use latest or a release tag such as v1.2.3."
                                              fi
                                              RELEASE_API_URL="https://api.github.com/repos/${REPOSITORY}/releases/tags/${VERSION}"
                                            fi

                                            if ! RELEASE_JSON="$(get_url "$RELEASE_API_URL")"; then
                                              if [ "$VERSION" != 'latest' ] && [[ "$VERSION" != v* ]]; then
                                                RELEASE_API_URL="https://api.github.com/repos/${REPOSITORY}/releases/tags/v${VERSION}"
                                                RELEASE_JSON="$(get_url "$RELEASE_API_URL")" ||
                                                  fail "Unable to resolve release '${VERSION}' from ${REPOSITORY}."
                                              else
                                                fail "Unable to resolve release '${VERSION}' from ${REPOSITORY}."
                                              fi
                                            fi
                                            ASSET_URLS="$(printf '%s\n' "$RELEASE_JSON" |
                                              sed -n 's/^[[:space:]]*"browser_download_url":[[:space:]]*"\([^"]*\)".*/\1/p')"
                                            [ -n "$ASSET_URLS" ] || fail "The release does not contain downloadable assets."

                                            SELECTED_PACKAGE_TYPE=''
                                            SELECTED_ASSET_URL=''
                                            select_package ||
                                              fail "No selected package is available for ${PLATFORM}-${ARCHITECTURE} with the installed package tools."

                                            TEMP_DIRECTORY="$(mktemp -d)"
                                            MOUNT_DIRECTORY=''
                                            cleanup() {
                                              if [ -n "$MOUNT_DIRECTORY" ]; then
                                                hdiutil detach "$MOUNT_DIRECTORY" -quiet 2>/dev/null || true
                                              fi
                                              rm -rf "$TEMP_DIRECTORY"
                                            }
                                            trap cleanup EXIT
                                            ASSET_NAME="${SELECTED_ASSET_URL##*/}"
                                            ASSET_FILE="$TEMP_DIRECTORY/$ASSET_NAME"

                                            printf 'Downloading %s (%s)...\n' "$APPLICATION_NAME" "$SELECTED_PACKAGE_TYPE"
                                            download_file "$SELECTED_ASSET_URL" "$ASSET_FILE"
                                            install_selected_package
                                            printf '%s was installed successfully.\n' "$APPLICATION_NAME"
                                            """;
}
