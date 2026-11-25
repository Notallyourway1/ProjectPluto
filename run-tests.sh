#!/bin/bash
# Compiles the game-independent core and its tests, then runs them.
set -e
cd "$(dirname "$0")"
mcs -nologo -out:Tests.exe Source/Core/*.cs Tests/*.cs
mono Tests.exe
