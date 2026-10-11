# Scenario lab

Run the scripts from the repository root after `dotnet build`. Each case compares `baseline` with `current`; fake values only.

| # | Scenario | Command behavior | Expected result |
|---:|---|---|---|
| 01 | Formatting/key order only | `compare` | No semantic drift; exit 0 |
| 02 | Debug turned on | `check` + production policy | Error finding; exit 1 |
| 03 | Database TLS disabled | `check` + production policy | Error finding; exit 1 |
| 04 | Password rotation | `check --fail-on warning` | Warning; values redacted; exit 1 |
| 05 | Critical feature flag removed | `check` + production policy | Error finding; exit 1 |
| 06 | Port changes string → number | `check --fail-on warning` | Type-change warning; exit 1 |
| 07 | YAML debug turned on | `check` + production policy | Same policy behavior as JSON; exit 1 |
| 08 | Safe feature flag addition | `check` + production policy | No matching finding; exit 0 |
| 09 | Service scales down to one replica | `check --fail-on warning` | Warning; exit 1 |
| 10 | Invalid JSON | `compare` | Parse error; exit 2, no stack trace |
| 11 | Public ingress enabled | `check` + production policy | Error finding; exit 1 |
| 12 | Array element removed | `compare` | Index-based removal report; exit 0 |

## Manual verification checklist

- [ ] Run `dotnet test DriftGuard.sln --configuration Release`.
- [ ] Run `scripts/run-scenarios.sh` (or `scripts/run-scenarios.ps1`).
- [ ] For scenario 04, verify neither `EXAMPLE_OLD_PASSWORD_NEVER_USE` nor `EXAMPLE_NEW_PASSWORD_NEVER_USE` appears in output.
- [ ] For scenario 01, verify object key order and whitespace do not generate changes.
- [ ] For scenario 06, compare default error threshold with `--fail-on warning`.
- [ ] Run one JSON report and validate it with a JSON parser.
- [ ] Try a config larger than the configured input limit and confirm it fails before parsing.
- [ ] Add a local rule and confirm duplicate IDs and invalid severities are rejected.

## Suggested fuzz cases for contributors

- Deeply nested objects near the depth limit.
- Empty objects and arrays; nested null values.
- Numeric values such as `1`, `1.0`, `1e0`, and quoted `"1"`.
- Unicode property names and property names containing dots/brackets.
- Glob patterns with `*`, `**`, and escaped regex characters.
- Secret-looking values inside added/removed objects and arrays.
- YAML aliases, duplicate keys, unusual tags, multi-document files and malformed indentation.
