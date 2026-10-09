#!/usr/bin/env bash
set -euo pipefail

if (( $# != 2 )); then
    printf '%s\n' 'Usage: test-desktop-arch-package.sh <older.pkg.tar.zst> <newer.pkg.tar.zst>' >&2
    exit 64
fi
for tool in pacman bsdtar bwrap jq desktop-file-validate vercmp sha256sum Xvfb strace timeout; do
    command -v "$tool" >/dev/null || { printf 'Required verification tool missing: %s\n' "$tool" >&2; exit 69; }
done
if [[ ${XDG_SESSION_TYPE:-} != wayland || ! ${DISPLAY:-} =~ ^:[0-9]+(\.[0-9]+)?$ ]]; then
    printf '%s\n' 'Run native launch verification in a Wayland session with XWayland enabled.' >&2
    exit 69
fi
pgrep -x Xwayland >/dev/null
older=$(realpath -e -- "$1")
newer=$(realpath -e -- "$2")
name=islamu-configurator-desktop
older_identity=$(pacman -Qp -- "$older")
newer_identity=$(pacman -Qp -- "$newer")
[[ $older_identity == "$name "* && $newer_identity == "$name "* ]]
[[ $(vercmp "${older_identity#* }" "${newer_identity#* }") == -1 ]]

export TMPDIR=${TMPDIR:-"$HOME/.cache/agent-tmp"}
mkdir -p -- "$TMPDIR"
work=$(mktemp -d "$TMPDIR/desktop-arch-check.XXXXXXXX")
x11_pid=
cleanup() {
    if [[ -n $x11_pid ]]; then
        kill "$x11_pid" 2>/dev/null || true
        wait "$x11_pid" 2>/dev/null || true
    fi
    rm -rf -- "$work"
}
trap cleanup EXIT
mkdir -p "$work/root/var/lib/pacman" "$work/cache" "$work/hooks" "$work/extracted"
printf '%s\n' '[options]' 'Architecture = x86_64' 'SigLevel = Never' > "$work/pacman.conf"

# The host tree is read-only; root privileges and every pacman mutation stay isolated.
isolated_pacman() {
    bwrap --ro-bind / / --bind "$work" "$work" --unshare-user --uid 0 --gid 0 \
        --unshare-net --die-with-parent -- \
        /usr/bin/pacman --root "$work/root" --dbpath "$work/root/var/lib/pacman" \
        --config "$work/pacman.conf" --cachedir "$work/cache" --hookdir "$work/hooks" \
        --logfile "$work/pacman.log" --noconfirm "$@"
}

for archive in "$older" "$newer"; do
    metadata=$(bsdtar -xOf "$archive" .PKGINFO)
    [[ $metadata == *$'arch = x86_64\n'* ]]
    [[ $metadata == *$'depend = xorg-xwayland\n'* ]]
    [[ $metadata != *'depend = dotnet'* ]]
    if bsdtar -tf "$archive" | grep -E '(^|/)(home|etc)/|^\.INSTALL$|\.dll$|\.pdb$' >/dev/null; then
        printf '%s\n' 'Package contains unexpected user/configuration/script/loose assembly files.' >&2
        exit 65
    fi
done
bsdtar -xf "$newer" -C "$work/extracted"
payload="$work/extracted/usr/lib/$name"
(
    cd -- "$payload"
    sha256sum --check --quiet "$newer.payload.sha256"
    [[ $(find . -type f | wc -l) == $(wc -l < "$newer.payload.sha256") ]]
)
test -x "$payload/$name"
test -s "$payload/LICENSE"
test -s "$payload/THIRD-PARTY-NOTICES.md"
for directory in contracts schemas dependency-notices; do test -d "$payload/$directory"; done
jq -e '.surface == "desktop" and .runtimeIdentifiers == ["linux-x64"]
    and .selfContained and .publishSingleFile and .includeNativeLibrariesForSelfExtract' \
    "$payload/contracts/package.json" >/dev/null
desktop-file-validate "$work/extracted/usr/share/applications/$name.desktop"
test -s "$work/extracted/usr/share/icons/hicolor/scalable/apps/$name.svg"
test -s "$work/extracted/usr/share/licenses/$name/LICENSE"
[[ $(readlink "$work/extracted/usr/bin/$name") == "../lib/$name/$name" ]]
printf '%s\n' 'PASS: native metadata, XWayland dependency, standalone closure and launcher integration'

mkdir -p "$work/root/home/operator/output"
printf '%s\n' 'synthetic operator-owned configuration' > "$work/root/home/operator/output/prepared.json"
chmod 600 "$work/root/home/operator/output/prepared.json"
original=$(sha256sum "$work/root/home/operator/output/prepared.json")
# Dependency resolution is checked against the real host; this isolated root stores no OS packages.
mapfile -t dependencies < <(bsdtar -xOf "$newer" .PKGINFO | sed -n 's/^depend = //p')
pacman -T "${dependencies[@]}"
isolated_pacman -U --noscriptlet --nodeps --nodeps "$older"
[[ $(isolated_pacman -Q "$name") == "$older_identity" ]]
printf '%s\n' 'PASS: pacman installed and registered the older development package'
isolated_pacman -U --noscriptlet --nodeps --nodeps "$newer"
[[ $(isolated_pacman -Q "$name") == "$newer_identity" ]]
isolated_pacman -Qk "$name"
diff -r "$payload" "$work/root/usr/lib/$name"
test -x "$work/root/usr/bin/$name"
[[ $(sha256sum "$work/root/home/operator/output/prepared.json") == "$original" ]]
[[ $(stat -c %a "$work/root/home/operator/output/prepared.json") == 600 ]]
printf '%s\n' 'PASS: pacman upgrade retained every payload byte and operator-owned file'

launch_check() {
    local label=$1 display=$2 session=$3 exit_code=0 display_number
    display_number=${display#:}
    display_number=${display_number%%.*}
    mkdir -p "$work/runtime" "$work/launch-home"
    bwrap --ro-bind / / --bind "$work" "$work" --tmpfs /usr/share/dotnet \
        --proc /proc --dev /dev \
        --unshare-user --unshare-net --die-with-parent -- \
        /usr/bin/env -i PATH=/usr/bin:/bin LANG=C.UTF-8 HOME="$work/launch-home" \
        TMPDIR="$work/runtime" DOTNET_ROOT=/unavailable \
        DOTNET_BUNDLE_EXTRACT_BASE_DIR="$work/runtime/bundle" DISPLAY="$display" \
        XAUTHORITY="${XAUTHORITY:-}" XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-}" \
        WAYLAND_DISPLAY="${WAYLAND_DISPLAY:-}" XDG_SESSION_TYPE="$session" \
        LIBGL_ALWAYS_SOFTWARE=1 \
        /usr/bin/timeout --signal=TERM --kill-after=2s 8s \
        /usr/bin/strace -f -e trace=connect -o "$work/$label.connect" \
        "$work/root/usr/bin/$name" > "$work/$label.log" 2>&1 || exit_code=$?
    if [[ $exit_code != 124 ]]; then
        printf 'FAIL: installed %s launch exited %s instead of reaching its smoke-test timeout.\n' "$label" "$exit_code" >&2
        return 1
    fi
    if ! grep -E "connect\(.*sun_path=\"/tmp/\.X11-unix/X$display_number\".*= 0" \
        "$work/$label.connect" >/dev/null; then
        printf 'FAIL: installed %s launch did not establish its display connection.\n' "$label" >&2
        return 1
    fi
    printf 'PASS: installed %s launch connected to its display and remained running without SDK/network\n' "$label"
}
launch_check Wayland-XWayland "$DISPLAY" wayland
mkfifo "$work/x11-ready"
Xvfb -displayfd 3 -screen 0 1100x850x24 -nolisten tcp 3> "$work/x11-ready" > "$work/xvfb.log" 2>&1 &
x11_pid=$!
IFS= read -r -t 10 x11_display < "$work/x11-ready"
[[ $x11_display =~ ^[0-9]+$ ]]
launch_check X11 ":$x11_display" x11
kill "$x11_pid"
wait "$x11_pid" 2>/dev/null || true
x11_pid=

isolated_pacman -R --noscriptlet "$name"
test ! -e "$work/root/usr/lib/$name"
test ! -e "$work/root/usr/bin/$name"
test ! -e "$work/root/usr/share/applications/$name.desktop"
test ! -e "$work/root/usr/share/icons/hicolor/scalable/apps/$name.svg"
test ! -e "$work/root/usr/share/licenses/$name"
[[ $(sha256sum "$work/root/home/operator/output/prepared.json") == "$original" ]]
[[ $(stat -c %a "$work/root/home/operator/output/prepared.json") == 600 ]]
if isolated_pacman -Q "$name" >/dev/null 2>&1; then exit 65; fi
printf '%s\n' 'PASS: pacman removal deleted package-owned files and preserved private operator output'
