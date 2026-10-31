#!/bin/sh
# Builds PS5Kitsune-<version>-x86_64.AppImage: the app and the CLI published self-contained into one folder. See the 8b spec, "Packaging".
# PS5K_APPIMAGE_OUT overrides the output folder (default artifacts/appimage): a case-insensitive filesystem such as /mnt/c in WSL merges PS5Kitsune and ps5kitsune, so build on ext4 there.
set -eu

version=${1:?usage: build-appimage.sh <version>}
repo=$(cd "$(dirname "$0")/../.." && pwd)
out=${PS5K_APPIMAGE_OUT:-"$repo/artifacts/appimage"}
appdir="$out/AppDir"
tools="$out/tools"

tool_url=https://github.com/AppImage/appimagetool/releases/download/1.9.1/appimagetool-x86_64.AppImage
tool_sha=ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0
runtime_url=https://github.com/AppImage/type2-runtime/releases/download/20251108/runtime-x86_64
runtime_sha=2fca8b443c92510f1483a883f60061ad09b46b978b2631c807cd873a47ec260d

fetch() { # url sha256 file
    if [ ! -f "$3" ] || ! echo "$2  $3" | sha256sum -c --status; then
        curl -fsSL --retry 3 -o "$3" "$1"
        echo "$2  $3" | sha256sum -c --quiet || { echo "Hash mismatch for $1" >&2; exit 1; }
    fi
}

rm -rf "$appdir" "$out/app" "$out/cli"
mkdir -p "$appdir/usr/bin" "$tools"

for project in App Cli; do
    dotnet publish "$repo/src/PS5Kitsune.$project" -c Release -r linux-x64 --self-contained \
        -p:Version="$version" -o "$out/$(echo "$project" | tr '[:upper:]' '[:lower:]')"
done

# The two outputs share the runtime and every common library: a file in both must be identical.
(cd "$out/app" && find . -type f) | while IFS= read -r file; do
    if [ -f "$out/cli/$file" ] && ! cmp -s "$out/app/$file" "$out/cli/$file"; then
        echo "The app and the CLI publish different $file" >&2
        exit 1
    fi
done || exit 1
cp -a "$out/app/." "$out/cli/." "$appdir/usr/bin/"

cp "$repo/packaging/linux/AppRun" "$appdir/AppRun"
chmod +x "$appdir/AppRun"
cp "$repo/packaging/linux/ps5kitsune.desktop" "$repo/packaging/linux/ps5kitsune.png" "$appdir/"

fetch "$tool_url" "$tool_sha" "$tools/appimagetool"
fetch "$runtime_url" "$runtime_sha" "$tools/runtime-x86_64"
chmod +x "$tools/appimagetool"

# No FUSE on CI runners or in WSL: appimagetool is itself an AppImage.
APPIMAGE_EXTRACT_AND_RUN=1 ARCH=x86_64 "$tools/appimagetool" --no-appstream \
    --runtime-file "$tools/runtime-x86_64" "$appdir" "$out/PS5Kitsune-$version-x86_64.AppImage"
echo "$out/PS5Kitsune-$version-x86_64.AppImage"
