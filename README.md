# DriftGuard

**Semantic configuration diff and deployment-policy checks for .NET teams.** DriftGuard compares two JSON/YAML configuration snapshots, explains their behavioral differences, applies a small policy rule set, and returns CI-friendly exit codes.

> Status: early open-source MVP (`0.1.1` engineering hardening patch). The code and scenarios are a strong starting point, not a substitute for testing with your own deployment configuration before production enforcement.

## Why a CLI first?

DriftGuard is intentionally CLI-first. Configuration checks belong in local workflows and CI, and JSON output is a useful integration contract. A web UI would add hosting, authentication, and deployment concerns before the semantics are settled. The terminal report is the initial UX; build a UI only after users demonstrate a need for cross-run history, review collaboration, or dashboards.

## Features in this repository

- Semantic JSON/YAML comparison: ignores whitespace and object property order; preserves array order.
- Change kinds: `added`, `removed`, `changed`, and `type-changed`.
- JSONPath-like paths such as `$.database.sslMode` and `$.services.worker.replicas`.
- Policy rules by path glob, operation, severity, and optional typed `newValueEquals` condition.
- Human-readable terminal output and machine-readable JSON output.
- Exit codes suitable for CI: `0` pass, `1` policy threshold failed, `2` usage/input error.
- Best-effort automatic redaction for sensitive path names and common token-like values.
- Input limits: config files up to 5 MiB, policy files up to 1 MiB, and parser depth limits.
- Unit tests, CI workflow, production-oriented sample policy, and 17 reproducible scenarios.
- Fail-closed duplicate JSON keys, bounded YAML expansion and policy evaluation, and strict CLI options.
- Versioned JSON report fields (`schemaVersion`, `policyApplied`) as a starting point for web integrations.

## Requirements

- .NET 10 SDK (the repository targets `net10.0`). .NET 10 is an LTS release supported through November 2028; see [Microsoft's release lifecycle](https://learn.microsoft.com/dotnet/core/releases-and-support).
- Network access during first restore for the pinned NuGet dependency `YamlDotNet`.

## Quick start

From the repository root:

```bash
dotnet restore DriftGuard.sln
dotnet build DriftGuard.sln --configuration Release
dotnet test DriftGuard.sln --configuration Release
```

Compare two files:

```bash
dotnet run --project src/DriftGuard.Cli -- compare \
  scenarios/01-no-op/baseline.json \
  scenarios/01-no-op/current.json
```

Run a policy check (recommended in CI):

```bash
dotnet run --project src/DriftGuard.Cli -- check \
  scenarios/02-debug-enabled/baseline.json \
  scenarios/02-debug-enabled/current.json \
  --policy policies/production.json
```

This scenario intentionally fails with exit code `1`, since `logging.debug` was enabled. Check the exit code in scripts rather than parsing terminal text.

Emit JSON for CI or other tools:

```bash
dotnet run --project src/DriftGuard.Cli -- check \
  scenarios/06-type-drift/baseline.json \
  scenarios/06-type-drift/current.json \
  --policy policies/production.json \
  --format json \
  --fail-on warning
```

Explain a path (including any changed descendants):

```bash
dotnet run --project src/DriftGuard.Cli -- explain \
  scenarios/03-tls-disabled/baseline.json \
  scenarios/03-tls-disabled/current.json \
  '$.database' \
  --policy policies/production.json
```

Create a policy starter file:

```bash
dotnet run --project src/DriftGuard.Cli -- init --path driftguard.policy.json
```

`init` refuses to overwrite an existing file unless you pass `--force`. Review and customize the example values before using them in a real deployment.

## CI exit semantics

| Command / option | Behavior |
|---|---|
| `compare` | Reports changes; default `--fail-on none`, so change presence alone does not fail. |
| `check` | Requires `--policy`; defaults to `--fail-on error`. |
| `--fail-on none` | Never fails based on policy findings. Useful for report-only adoption. |
| `--fail-on warning` | Exit `1` on warning or error findings. |
| `--fail-on error` | Exit `1` only on error findings. |
| Invalid input / unknown option | Exit `2`; details go to stderr. |

`check` requiring an explicit policy is deliberate: an absent policy must not quietly look like a successful production gate. `compare --fail-on warning/error` **also requires** `--policy`; otherwise the CLI returns exit `2` rather than silently passing without rules. Options that do not belong to a given command are rejected.

## Policy format

See [`policies/production.json`](policies/production.json). Example:

```json
{
  "version": 1,
  "rules": [
    {
      "id": "debug-must-not-be-enabled",
      "description": "Do not enable debug mode in deployment.",
      "severity": "error",
      "operation": "any",
      "path": "$.logging.debug",
      "newValueEquals": true
    }
  ]
}
```

Supported rule properties:

- `id`: unique, stable ID (1–80 characters).
- `description`: human-readable reason.
- `severity`: `info`, `warning`, or `error`.
- `operation`: `any`, `added`, `removed`, `changed`, or `type-changed`.
- `path`: case-insensitive, segment-aware glob. `*` matches exactly one segment (including bracket-quoted keys), `**` matches zero or more complete path segments. Examples: `$.services.*.replicas`, `$.**.password` (matches both `$.password` and `$.api.password`). Dots, quotes and backslashes in literal key names use `['...']` bracket notation. Wildcards inside **unquoted** dot segments match characters within that segment.
- `newValueEquals` (optional): a JSON scalar (`string`, `number`, `boolean`, or `null`) compared with the current value. Use JSON types intentionally; `1` and `"1"` are not equivalent. Numeric lexical changes such as `1` vs `1.0` are conservatively reported as drift.

A rule evaluates observed differences only. For an entire object/array added, removed or replaced by a type change, the policy engine also evaluates **its descendants**, even though the diff report keeps the original parent-level change compact. Thus, adding `{ "logging": { "debug": true } }` cannot bypass the policy at `$.logging.debug`. Broad rules such as `$.**` may produce multiple findings for these changes. It is not a full static policy scan of unchanged settings. For example, the sample password rule reports a changed password path; it does not prove that every existing secret is stored safely.

## Scenario lab

All sample inputs are intentionally fake. Run all 17 test scenarios after building:

```bash
# Linux/macOS
dotnet test DriftGuard.sln --configuration Release
bash ./scripts/run-scenarios.sh

# Windows PowerShell
dotnet test DriftGuard.sln --configuration Release
./scripts/run-scenarios.ps1
```

See [`docs/SCENARIOS.md`](docs/SCENARIOS.md) for the case matrix and expected outcomes. Scenario scripts assert exit codes; review output for each test, especially redaction in scenario 04.

## Repository layout

```text
DriftGuard.sln
src/
  DriftGuard.Core/         Parsing, diff model, redaction, policy engine
  DriftGuard.Cli/          Command-line interface and report formatting
tests/
  DriftGuard.Core.Tests/   Unit tests
policies/
  production.json          Example deployment policy
scenarios/                 Reproducible baseline/current pairs
scripts/                   Scenario runners
.github/workflows/         CI build and test
```

## Deliberate limitations in v0.1

- JSON and YAML only; root values must be objects/mappings. Duplicate JSON property names are rejected.
- YAML scalar conversion supports common config types (quoted strings, booleans, null, integers and decimal/exponent numbers); this is not a complete YAML-to-JSON compatibility layer for every YAML 1.1/1.2 edge case.
- Arrays are compared by index. Reordering an array is meaningful drift.
- Paths are JSONPath-like, not a full implementation of the JSONPath standard. Property names with dots or special characters use bracket notation in reports; test policy matching against those names before enforcing it.
- Redaction is best effort and heuristic. Treat input and output as sensitive; do not publish reports indiscriminately or upload them to untrusted services. The future cloud ingestion feature requires a stricter values-free report mode.
- No automatic merge, remote secret retrieval, cloud API calls, or write-back to configuration files.
- CLI output uses the standard terminal; there is no web dashboard in v1.
- Policy evaluation fails with exit `2` for workloads exceeding its bounded candidate/finding/comparison limits. See the engineering review for details.
- JSON reports use `schemaVersion: 1` and `policyApplied` to distinguish successful policy checks from report-only comparisons. See [`docs/contracts/`](docs/contracts/README.md).

## Production-readiness checklist before enforcing in deployment

1. Add repository-specific policy rules and test them against known-good and known-bad snapshots.
2. Run in report-only mode first (`compare`, or `check --fail-on none`) and inspect false positives.
3. Add representative fixtures for every config format and provider you use.
4. Store secrets outside plaintext configuration wherever possible; redaction is not a secret-management system.
5. Pin the tool version in CI and review package updates.
6. Decide whether warnings should block a release, and document exceptions/ownership.
7. Treat policy descriptions and baseline/current paths as potentially sensitive in build logs.

## Engineering review, web roadmap, and Free / Basic / Pro plans

- [Detailed code review and bugfixes (Italian)](docs/ENGINEERING-REVIEW.it.md)
- [Prioritized engineering/product roadmap (Italian)](docs/ROADMAP.it.md)
- [Proposed web/SaaS architecture (Italian)](docs/architecture/WEB-SAAS.it.md)
- [Free, Basic, Pro licensing/entitlement model (Italian)](docs/product/PLANS.it.md)
- [Proposal JSON plan catalog](docs/product/plan-catalog.proposal.json)
- [Report v1 JSON schema](docs/contracts/report.v1.schema.json)

**Important:** the web backend, billing, paid plan enforcement and subscriptions are **not implemented**. The current code is MIT licensed; proposed plans apply only to future hosted services.

## Roadmap ideas (not implemented)

- GitHub Action wrapper and SARIF output.
- Policy validation command plus `--explain-rule <id>`.
- Recursive comparison of directory trees and secret-name inventory scan.
- Additional parsers and Kubernetes-specific semantic normalizers.
- Optional local web/TUI view for reviewing multiple environments; only after a stable JSON schema exists.
- Benchmarks, fuzzing, and property-based tests for parsers, glob matching and redaction.

## Contributing

Start with [`CONTRIBUTING.md`](CONTRIBUTING.md). Contributions should add tests and scenario fixtures for new matching or parsing behavior. Avoid adding broad dependencies to the core without a clear use case.

## License

MIT. See [`LICENSE`](LICENSE).
