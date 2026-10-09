#!/usr/bin/env bash
set -euo pipefail

if (( $# > 1 )); then
    printf '%s\n' 'Usage: package-desktop-arch.sh [0.0.0.devYYYYMMDDhhmmss]' >&2
    exit 64
fi
version=${1:-0.0.0.dev$(date -u +%Y%m%d%H%M%S)}
if [[ ! $version =~ ^0\.0\.0\.dev[0-9]{14}$ ]]; then
    printf '%s\n' 'Only timestamped local development versions are accepted.' >&2
    exit 64
fi
if [[ $(uname -s) != Linux || $(uname -m) != x86_64 || $EUID == 0 ]]; then
    printf '%s\n' 'Build as an ordinary user on an x86_64 Arch Linux host.' >&2
    exit 69
fi
for tool in dotnet makepkg pacman tar sha256sum jq desktop-file-validate; do
    command -v "$tool" >/dev/null || { printf 'Required build tool missing: %s\n' "$tool" >&2; exit 69; }
done

root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)
output="$root/artifacts/configurator/packages/arch"
package="$output/islamu-configurator-desktop-$version-1-x86_64.pkg.tar.zst"
if [[ -e $package || -e $package.sha256 ]]; then
    printf '%s\n' 'This development package version already exists; use a new timestamp.' >&2
    exit 73
fi
mkdir -p -- "$output"
work=$(mktemp -d "$output/.arch-build.XXXXXXXX")
trap 'rm -rf -- "$work"' EXIT
export TMPDIR=${TMPDIR:-"$HOME/.cache/agent-tmp"}
mkdir -p -- "$TMPDIR"

dotnet msbuild "$root/eng/configurator/Configurator.Release.proj" \
    -t:PublishConfigurator -p:ConfiguratorSurface=desktop -p:ConfiguratorRid=linux-x64 \
    -p:Configuration=Release -p:ConfiguratorOutputRoot="$work/published" \
    -m:1 -nr:false -p:UseSharedCompilation=false

payload="$work/published/desktop/linux-x64"
for entry in islamu-configurator-desktop LICENSE THIRD-PARTY-NOTICES.md contracts/package.json; do
    test -s "$payload/$entry" || { printf '%s\n' 'Published Desktop payload is incomplete.' >&2; exit 65; }
done
for directory in contracts schemas dependency-notices; do
    test -d "$payload/$directory" || { printf '%s\n' 'Published Desktop metadata is incomplete.' >&2; exit 65; }
done
jq -e '.surface == "desktop" and .runtimeIdentifiers == ["linux-x64"]
    and .executableName == "islamu-configurator-desktop" and .selfContained
    and .publishSingleFile and (.publishTrimmed | not)
    and .includeNativeLibrariesForSelfExtract and .useAppHost' \
    "$payload/contracts/package.json" >/dev/null
if [[ -n $(find "$payload" -type l -print -quit) ]]; then
    printf '%s\n' 'Published payload must not contain symbolic links.' >&2
    exit 65
fi
desktop-file-validate "$root/eng/configurator/packaging/linux/islamu-configurator-desktop.desktop"

mkdir -p -- "$work/archive/payload" "$work/archive/integration" "$work/packages"
cp -a -- "$payload/." "$work/archive/payload/"
find "$work/archive/payload" -type d -exec chmod 755 {} +
find "$work/archive/payload" -type f -exec chmod 644 {} +
chmod 755 "$work/archive/payload/islamu-configurator-desktop"
install -m644 "$root/eng/configurator/packaging/linux/islamu-configurator-desktop.desktop" "$work/archive/integration/"
install -m644 "$root/src/Configurator.Browser/wwwroot/icons/configurator-512.svg" \
    "$work/archive/integration/islamu-configurator-desktop.svg"
tar -cf "$work/payload.tar" -C "$work/archive" payload integration
payload_hash=$(sha256sum "$work/payload.tar")
payload_hash=${payload_hash%% *}
sed -e "s/@VERSION@/$version/g" -e "s/@PAYLOAD_SHA256@/$payload_hash/g" \
    "$root/eng/configurator/packaging/linux/arch/PKGBUILD.in" > "$work/PKGBUILD"
printf '%s\n' 'source /etc/makepkg.conf' 'COMPRESSZST=(zstd -c -T2 -8 -)' > "$work/makepkg.conf"
(
    cd -- "$work"
    export PKGDEST="$work/packages" SRCDEST="$work" BUILDDIR="$work/build"
    export PACKAGER='ISLAMU local development <development@islamu.invalid>' CARCH=x86_64 PKGEXT=.pkg.tar.zst
    makepkg --config "$work/makepkg.conf" --noconfirm --nosign
    makepkg --config "$work/makepkg.conf" --printsrcinfo > "$work/packages/.SRCINFO"
)
archive="$work/packages/$(basename -- "$package")"
test -s "$archive"
pacman -Qp -- "$archive"
(
    cd -- "$payload"
    find . -type f -print0 | sort -z | xargs -0 sha256sum > "$work/payload.sha256"
)
# Publish only a successfully assembled archive; never install or use release credentials.
ln -- "$archive" "$package"
cp -- "$work/PKGBUILD" "$output/$(basename -- "$package").PKGBUILD"
cp -- "$work/packages/.SRCINFO" "$output/$(basename -- "$package").SRCINFO"
cp -- "$work/payload.sha256" "$output/$(basename -- "$package").payload.sha256"
(
    cd -- "$output"
    sha256sum "$(basename -- "$package")" > "$(basename -- "$package").sha256"
)
printf '\nArch development package: %s\n' "$package"
