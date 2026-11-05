#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
sdk_root="${ZOMBIO_DOTNET_DIR:-/workspace/tooling/dotnet}"
export DOTNET_CLI_HOME="${ZOMBIO_DOTNET_CLI_HOME:-/workspace/tooling/dotnet-home}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
sdk_version=8.0.427
if [[ ! -x "$sdk_root/dotnet" ]] || [[ "$("$sdk_root/dotnet" --version)" != "$sdk_version" ]]; then
  [[ "$(uname -s)" == Linux && "$(uname -m)" == x86_64 ]] || { echo 'Use an installed .NET 8 SDK on this platform.' >&2; exit 1; }
  mkdir -p "$sdk_root"
  sdk_archive="$(mktemp /tmp/zombio-sdk.XXXXXX.tar.gz)"
  trap 'rm -f "$sdk_archive"' EXIT
  curl --fail --show-error --location --retry 2 "https://builds.dotnet.microsoft.com/dotnet/Sdk/$sdk_version/dotnet-sdk-$sdk_version-linux-x64.tar.gz" --output "$sdk_archive"
  printf '%s  %s\n' '85725692f465f5024b9b42a159a29d9f73e8c57ad9e8bfe1e7028a505d18031d68931e8aa3f9de44145b08170dde897c8a78e1aa18d4219698b6891af7095d39' "$sdk_archive" | sha512sum --check --status
  tar -xzf "$sdk_archive" -C "$sdk_root"
fi
cd "$repo_root"
"$sdk_root/dotnet" run --project Tools/CoreChecks/CoreChecks.csproj --configuration Release
