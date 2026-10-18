#!/usr/bin/env bash
set -euo pipefail
sandbox_root="${HOME}/DAREU"; dotnet_root="${HOME}/.dotnet"; sdk_archive="${HOME}/dotnet-sdk-10.0.402-linux-x64.tar.gz"
if [[ ! -f "$sdk_archive" ]]; then echo "Missing $sdk_archive" >&2; exit 2; fi
mkdir -p "$dotnet_root"; tar -zxf "$sdk_archive" -C "$dotnet_root"; export DOTNET_ROOT="$dotnet_root"; export PATH="$dotnet_root:$PATH"; export DOTNET_CLI_TELEMETRY_OPTOUT=1
cd "$sandbox_root"; dotnet --info; dotnet restore tests/DareuAe6.Core.Tests/DareuAe6.Core.Tests.csproj; dotnet test tests/DareuAe6.Core.Tests/DareuAe6.Core.Tests.csproj --no-restore -c Release
