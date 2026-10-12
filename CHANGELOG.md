# Changelog

## 0.2.0 — CLI contracts, minimal reports and DevOps integration

- Implement `validate-policy` with JSON/text responses and structured JSON errors (exit 2).
- Introduce `--values none` and `--source-names none` for privacy-minimized JSON CI reports, keeping successful report schema v1 unchanged.
- Export SARIF 2.1.0 findings with deterministic fingerprints and explicit file-level line-1 annotations.
- Add a local GitHub composite action with optional report artifact and SARIF uploads; uploads are disabled by default.
- Extend GitHub CI to Windows + Ubuntu, CLI process tests and a report-v1 golden fixture.
- Make `init` generate policy templates in UTF-8 with BOM and CRLF.
- Add implementation guidance, dedicated error/validation JSON schemas and versioned contract notes.
- Runtime validation on .NET 10 still required: SDK unavailable in this authoring environment.

## 0.1.1 — input hardening, policy correctness and web preparation

- Evaluate descendants of added, removed and type-changed subtrees for policy rules without inflating the public `changes` list.
- Replace regex path globs with segment-aware, bounded matching supporting zero-level `**`, bracket keys, and escaped quotes/backslashes.
- Reject duplicate JSON properties, numeric pseudo-severity values and invalid policy path syntax.
- Limit file reads, expanded YAML nodes/scalar data and policy evaluation work.
- Validate per-command CLI options and require a policy when using a blocking `--fail-on` threshold.
- Improve `explain` for ancestor-level changes and escape controls in console text.
- Add `schemaVersion: 1` and `policyApplied` to JSON reports.
- Add core regression tests and five new scenario fixtures (17 total), plus docs for a potential SaaS and plan model.
- Invoke scenario runner via `bash` in CI, independent of script executable file mode.
- Note: tests could not be run in the review environment; a .NET 10 CI run remains necessary.

## 0.1.0 — initial MVP

- Semantic JSON/YAML config comparison.
- Added, removed, changed and type-changed operations.
- Path-glob policy evaluation with severity and typed value matching.
- Terminal and JSON reports with CI exit codes.
- Best-effort redaction of sensitive paths and common token-like values.
- Sample production-oriented policy, tests, CI workflow and 12 scenario fixtures.
