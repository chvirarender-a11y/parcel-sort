#!/usr/bin/env bash
# Regenerates Assets/Resources/Levels/level_XXX.json with Mono. Run from the project root.
set -euo pipefail
out=$(mktemp -d)
mcs -nologo -out:"$out/levelgen.exe" Assets/Scripts/Core/*.cs tools/levelgen/Program.cs
mono "$out/levelgen.exe" Assets/Resources/Levels
