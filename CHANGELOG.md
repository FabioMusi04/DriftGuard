# Changelog

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
