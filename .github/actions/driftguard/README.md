# Local GitHub Action: DriftGuard

The composite action in this folder builds the **checked-out**, versioned CLI source using the .NET 10 SDK. It does **not** download an arbitrary CLI binary and works on Windows and Ubuntu runners. The caller must first run `actions/checkout@v4`.

```yaml
name: Configuration policy gate
on: [pull_request, push]
permissions:
  contents: read
  security-events: write # ONLY needed if upload-sarif is enabled
jobs:
  drift:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: ./.github/actions/driftguard
        with:
          baseline: configs/baseline.json
          current: configs/current.json
          policy: policies/production.json
          fail-on: error
          upload-artifact: 'true'
          upload-sarif: 'true'
```

Default `upload-artifact` and `upload-sarif` are **false**. The JSON artifact always uses `--format json --values none --source-names none`; the report has no old/new values or local source file paths. **Metadata is still sensitive**: keys/JSONPaths, policy IDs and human descriptions remain, while privacy-minimized JSON errors use generic messages. Apply repository access controls and review policies before enabling artifact upload.

SARIF necessarily contains the current file path (relative to the repository, where possible), JSONPath and policy description. The CLI cannot resolve exact changed source lines yet; SARIF findings point to **line 1**. GitHub PR inline annotations may not appear unless line 1 is part of the PR diff. Scan findings can still appear in code scanning where enabled. Public repositories and Code Security entitlements are governed by GitHub settings.

The action preserves CLI status semantics: 0 = success, 1 = blocking findings, 2 = invalid arguments or input. The JSON artifact is uploaded before the action fails the job. No cloud API, workspace token, billing or paid license is required.
