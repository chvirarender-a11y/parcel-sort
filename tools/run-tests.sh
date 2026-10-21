#!/usr/bin/env bash
# Runs the core rule tests without Unity, using Mono (apt install mono-mcs mono-runtime).
# Run from the project root. Inside Unity, use Window > General > Test Runner instead.
set -euo pipefail
out=$(mktemp -d)
mcs -nologo -out:"$out/tests.exe" \
  Assets/Scripts/Core/*.cs Assets/Tests/EditMode/*.cs tools/nunit-shim/*.cs
mono "$out/tests.exe"
