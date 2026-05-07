#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
args=("$@")
filtered=()
for arg in "${args[@]}"; do
  if [[ "$arg" == "--short" ]]; then
    filtered+=(-j short --filter '*')
    continue
  fi
  filtered+=("$arg")
done

dotnet run -c Release --project "$repo_root/tests/nem.Mimir.Benchmarks/nem.Mimir.Benchmarks.csproj" -- "${filtered[@]}"
