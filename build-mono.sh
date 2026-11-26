#!/bin/bash
# Builds GameData/ProjectPluto/Plugins/ProjectPluto.dll with Mono's mcs.
# Usage: ./build-mono.sh "/path/to/KSP Managed folder"
# macOS: <KSP folder>/KSP.app/Contents/Resources/Data/Managed
set -e
shopt -s nullglob
cd "$(dirname "$0")"
M="$1"
if [ -z "$M" ] || [ ! -f "$M/Assembly-CSharp.dll" ]; then
    echo 'Usage: ./build-mono.sh "/path/to/KSP Managed folder" (must contain Assembly-CSharp.dll)' >&2
    exit 1
fi
# Compile against KSP's own .NET libraries, not Mono's: newer overloads (such as
# string.Split(char, options)) do not exist in KSP's runtime and would fail when the game loads.
refs=("-nostdlib" "-r:$M/mscorlib.dll" "-r:$M/System.dll" "-r:$M/System.Core.dll"
      "-r:$M/Assembly-CSharp.dll" "-r:$M/Assembly-CSharp-firstpass.dll")
for f in "$M"/UnityEngine*.dll; do refs+=("-r:$f"); done
mkdir -p GameData/ProjectPluto/Plugins
mcs -optimize+ -target:library -debug- -nowarn:1701 \
    -out:GameData/ProjectPluto/Plugins/ProjectPluto.dll "${refs[@]}" \
    Source/*.cs Source/Core/*.cs Source/Effect/*.cs
ls -l GameData/ProjectPluto/Plugins/ProjectPluto.dll
