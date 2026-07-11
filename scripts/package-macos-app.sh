#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "${script_dir}/.." && pwd)"
project_path="${repo_root}/src/FileCrypter.Desktop/FileCrypter.Desktop.csproj"
artifacts_root="${repo_root}/artifacts/macos"
license_path="${repo_root}/LICENSE"
notice_path="${repo_root}/NOTICE"
third_party_notices_path="${repo_root}/THIRD-PARTY-NOTICES.md"

if [[ ! -f "${project_path}" ]]; then
    echo "Could not find FileCrypter.Desktop.csproj at ${project_path}" >&2
    exit 1
fi

if [[ ! -f "${license_path}" ]]; then
    echo "Could not find project license at ${license_path}" >&2
    exit 1
fi

if [[ ! -f "${notice_path}" ]]; then
    echo "Could not find project notice at ${notice_path}" >&2
    exit 1
fi

if [[ ! -f "${third_party_notices_path}" ]]; then
    echo "Could not find third-party notices at ${third_party_notices_path}" >&2
    exit 1
fi

get_msbuild_property() {
    local property_name="$1"

    dotnet msbuild "${project_path}" -nologo "-getProperty:${property_name}"
}

copy_native_package_notices() {
    local package_id="$1"
    local package_directory_name="$2"
    local output_prefix="$3"
    local escaped_package_id="${package_id//./\.}"
    local package_version
    local package_directory

    package_version="$(
        grep -m 1 -F "\"${package_id}/" "${publish_output}/${bundle_executable}.deps.json" \
            | sed -E "s@.*\"${escaped_package_id}/([^\"]+)\".*@\1@"
    )"

    if [[ -z "${package_version}" ]]; then
        echo "Could not resolve ${package_id} from the published dependency manifest." >&2
        exit 1
    fi

    package_directory="${global_packages_root}/${package_directory_name}/${package_version}"

    if [[ ! -f "${package_directory}/LICENSE.txt" ]]; then
        echo "Could not find ${package_id} license in ${package_directory}" >&2
        exit 1
    fi

    if [[ ! -f "${package_directory}/THIRD-PARTY-NOTICES.txt" ]]; then
        echo "Could not find ${package_id} third-party notices in ${package_directory}" >&2
        exit 1
    fi

    cp "${package_directory}/LICENSE.txt" "${resources_dir}/${output_prefix}-LICENSE.txt"
    cp "${package_directory}/THIRD-PARTY-NOTICES.txt" \
        "${resources_dir}/${output_prefix}-THIRD-PARTY-NOTICES.txt"
}

architecture="$(uname -m)"

case "${architecture}" in
    arm64)
        runtime_identifier="osx-arm64"
        ;;
    x86_64)
        runtime_identifier="osx-x64"
        ;;
    *)
        echo "Unsupported macOS architecture: ${architecture}" >&2
        exit 1
        ;;
esac

bundle_name="$(get_msbuild_property MacBundleName)"
bundle_display_name="$(get_msbuild_property MacBundleDisplayName)"
bundle_identifier="$(get_msbuild_property MacBundleIdentifier)"
minimum_system_version="$(get_msbuild_property MacMinimumSystemVersion)"
bundle_version="$(get_msbuild_property Version)"
bundle_executable="$(get_msbuild_property AssemblyName)"
global_packages_root="${NUGET_PACKAGES:-$(
    dotnet nuget locals global-packages --list | sed -E 's/^global-packages:[[:space:]]*//'
)}"

mkdir -p "${artifacts_root}"

staging_root="$(mktemp -d "${artifacts_root}/.staging.XXXXXX")"
publish_output="${staging_root}/publish"
bundle_dir="${staging_root}/${bundle_name}.app"
contents_dir="${bundle_dir}/Contents"
macos_dir="${contents_dir}/MacOS"
resources_dir="${contents_dir}/Resources"
final_bundle_dir="${artifacts_root}/${bundle_name}.app"

cleanup() {
    rm -rf "${staging_root}"
}

trap cleanup EXIT

echo "Restoring ${project_path} in locked mode..."
dotnet restore "${project_path}" --locked-mode

echo "Publishing ${project_path} for ${runtime_identifier}..."
dotnet publish "${project_path}" \
    -c Release \
    -r "${runtime_identifier}" \
    --no-restore \
    --self-contained false \
    -p:UseAppHost=true \
    -o "${publish_output}"

if [[ ! -x "${publish_output}/${bundle_executable}" ]]; then
    echo "Expected published executable was not created at ${publish_output}/${bundle_executable}" >&2
    exit 1
fi

mkdir -p "${macos_dir}" "${resources_dir}"
cp -R "${publish_output}/." "${macos_dir}/"
cp "${license_path}" "${resources_dir}/LICENSE"
cp "${notice_path}" "${resources_dir}/NOTICE"
cp "${third_party_notices_path}" "${resources_dir}/THIRD-PARTY-NOTICES.md"
copy_native_package_notices \
    "SkiaSharp.NativeAssets.macOS" \
    "skiasharp.nativeassets.macos" \
    "SkiaSharp"
copy_native_package_notices \
    "HarfBuzzSharp.NativeAssets.macOS" \
    "harfbuzzsharp.nativeassets.macos" \
    "HarfBuzzSharp"
chmod +x "${macos_dir}/${bundle_executable}"

cat > "${contents_dir}/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
  <dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleDisplayName</key>
    <string>${bundle_display_name}</string>
    <key>CFBundleExecutable</key>
    <string>${bundle_executable}</string>
    <key>CFBundleIdentifier</key>
    <string>${bundle_identifier}</string>
    <!-- A custom .icns can be added in a later packaging pass. -->
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>${bundle_name}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>${bundle_version}</string>
    <key>CFBundleVersion</key>
    <string>${bundle_version}</string>
    <key>LSMinimumSystemVersion</key>
    <string>${minimum_system_version}</string>
    <key>NSHighResolutionCapable</key>
    <true/>
  </dict>
</plist>
EOF

echo "Applying ad-hoc code signature to ${bundle_name}.app..."
codesign --force --deep --sign - "${bundle_dir}"

if [[ -e "${final_bundle_dir}" ]]; then
    backup_dir="${artifacts_root}/${bundle_name}.app.backup.$(date +%Y%m%d%H%M%S)"
    mv "${final_bundle_dir}" "${backup_dir}"
    echo "Moved existing bundle to ${backup_dir}"
fi

mv "${bundle_dir}" "${final_bundle_dir}"

echo
echo "Created macOS app bundle:"
echo "  ${final_bundle_dir}"
echo
echo "Launch with:"
echo "  open ${final_bundle_dir}"
echo
echo "Notes:"
echo "  - This is a framework-dependent macOS bundle for local use."
echo "  - .NET 10 must be installed on the machine that launches this app."
echo "  - Signing, notarization, and DMG packaging are intentionally deferred."
