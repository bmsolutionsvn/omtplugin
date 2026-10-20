#!/usr/bin/env bash
# Builds OMT-HX and the plugin on Linux x64, then leaves everything OBS loads in the publish folder.
# OMT-HX's FFmpeg comes from the distribution (libavcodec-dev, libavutil-dev, libswscale-dev, libswresample-dev).
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
omthx=${OMTHX_ROOT:-$(cd "$root/.." && pwd)/OMT-HX}
if [[ ! -f $omthx/CMakeLists.txt ]]; then
  echo "OMT-HX not found at $omthx. Clone it beside this repository, or set OMTHX_ROOT." >&2
  exit 1
fi
bash "$omthx/scripts/build.sh" --shared
dotnet publish "$root/omtplugin.csproj" -r linux-x64 -c Release
out="$root/bin/Release/net8.0/linux-x64/publish"
cp "$omthx/build/linux-release/libomthx.so" "$omthx/build/linux-release/libomt.so" \
   "$omthx/build/linux-release/libvmx.so" "$out/"
echo "Plugin and OMT-HX libraries are in $out"