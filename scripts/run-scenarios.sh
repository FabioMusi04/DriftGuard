#!/usr/bin/env bash
set -u
cd "$(dirname "$0")/.."

run_case() {
  expected="$1"
  label="$2"
  shift 2
  printf '\n===== %s (expected exit %s) =====\n' "$label" "$expected"
  "$@"
  actual=$?
  if [ "$actual" -ne "$expected" ]; then
    printf 'FAIL: %s returned %s, expected %s\n' "$label" "$actual" "$expected" >&2
    exit 1
  fi
  printf 'PASS: exit code matched (%s)\n' "$actual"
}

CLI=(dotnet run --configuration Release --no-build --project src/DriftGuard.Cli --)
POLICY=policies/production.json
run_case 0 '01 no semantic drift' "${CLI[@]}" compare scenarios/01-no-op/baseline.json scenarios/01-no-op/current.json
run_case 1 '02 debug enabled' "${CLI[@]}" check scenarios/02-debug-enabled/baseline.json scenarios/02-debug-enabled/current.json --policy "$POLICY"
run_case 1 '03 database TLS disabled' "${CLI[@]}" check scenarios/03-tls-disabled/baseline.json scenarios/03-tls-disabled/current.json --policy "$POLICY"
run_case 1 '04 secret change blocks at warning threshold' "${CLI[@]}" check scenarios/04-secret-rotation/baseline.json scenarios/04-secret-rotation/current.json --policy "$POLICY" --fail-on warning
run_case 1 '05 critical feature removed' "${CLI[@]}" check scenarios/05-feature-removed/baseline.json scenarios/05-feature-removed/current.json --policy "$POLICY"
run_case 1 '06 type drift blocks at warning threshold' "${CLI[@]}" check scenarios/06-type-drift/baseline.json scenarios/06-type-drift/current.json --policy "$POLICY" --fail-on warning
run_case 1 '07 YAML debug enabled' "${CLI[@]}" check scenarios/07-yaml-debug/baseline.yaml scenarios/07-yaml-debug/current.yaml --policy "$POLICY"
run_case 0 '08 safe additive change' "${CLI[@]}" check scenarios/08-safe-addition/baseline.json scenarios/08-safe-addition/current.json --policy "$POLICY"
run_case 1 '09 single replica blocks at warning threshold' "${CLI[@]}" check scenarios/09-single-replica/baseline.json scenarios/09-single-replica/current.json --policy "$POLICY" --fail-on warning
run_case 2 '10 malformed JSON' "${CLI[@]}" compare scenarios/10-invalid-json/baseline.json scenarios/10-invalid-json/current.json
run_case 1 '11 public ingress enabled' "${CLI[@]}" check scenarios/11-public-ingress/baseline.json scenarios/11-public-ingress/current.json --policy "$POLICY"
run_case 0 '12 array change is report-only' "${CLI[@]}" compare scenarios/12-array-change/baseline.json scenarios/12-array-change/current.json

printf '\nAll scenario exit-code assertions passed. Review each report above for the expected finding and redaction behavior.\n'
