# Incremento implementativo P1 — CLI/CI, versione 0.2.0

**Ambito**: primi deliverable della roadmap dopo la patch di correttezza 0.1.1. Questo aggiornamento contiene codice, test e integrazione GitHub Actions, **non** il backend SaaS o il billing.

## Funzionalita' implementate

| Deliverable | Implementazione | Come verificarlo |
|---|---|---|
| CLI: validazione delle policy | `validate-policy <file> [--format json]` valida sintassi, ID, glob, operazione e limiti | Exit 0 per `policies/production.json`, 2 per policy non valida |
| JSON minimizzato | `--values none` rimuove completamente `baselineValue` e `currentValue` (non sono semplici placeholder) | Schema JSON v1 e golden fixture |
| Omissione nomi dei file | `--source-names none` sostituisce baseline/current con `[omitted]` | Test del processo CLI |
| Errori JSON | `--format json` emette oggetto `kind: error`, schema versione 1, exit 2 | Test di errori d'uso e parsing |
| SARIF 2.1.0 | `--format sarif` con regole, risultati, livello, URI file, fingerprint deterministici | Due esportazioni consecutive producono output identico |
| GitHub Action locale | `.github/actions/driftguard/action.yml`, test/build CLI pin nella checkout | CI: smoke action (senza upload) |
| CI multipiattaforma | Matrix Ubuntu + Windows, test Core + CLI + scenari PowerShell | Workflow GitHub Actions |
| Contratti versionati | Schema report v1 invariato, nuovi schemi `error`/`policy-validation` e fixture golden | Test + schema validation |
| Template Windows | `init` scrive UTF-8 BOM + CRLF | Aprire il file creato e verificarne i byte |

## Utilizzo (PowerShell)

```powershell
# Verifica la policy prima di adottarla in CI.
dotnet run --project src/DriftGuard.Cli -- validate-policy policies/production.json --format json

# Report JSON senza valori delle configurazioni e senza riferimenti ai file locali.
dotnet run --project src/DriftGuard.Cli -- check scenarios/02-debug-enabled/baseline.json scenarios/02-debug-enabled/current.json --policy policies/production.json --format json --values none --source-names none

# SARIF per GitHub Code Scanning: preserva il riferimento al file del repository.
dotnet run --project src/DriftGuard.Cli -- check scenarios/02-debug-enabled/baseline.json scenarios/02-debug-enabled/current.json --policy policies/production.json --format sarif

# Build completa / test su Windows.
dotnet restore .\DriftGuard.sln
dotnet build .\DriftGuard.sln -c Release
dotnet test .\DriftGuard.sln -c Release --no-build
.\scripts\run-scenarios.ps1
.\scripts\check-windows-encoding.ps1
```

**Exit code:** `0` successo (o nessuna violazione bloccante), `1` policy fallita, `2` input/uso non valido. In modalita' JSON gli errori escono su stdout come documento JSON, non come testo libero su stderr. Con i flag privacy `none`, i messaggi di errore sono generici per non esportare percorsi locali o snippet di parsing.

## Sicurezza, compatibilita' e limiti

- `--values none` garantisce di non esportare valori raw vecchi/nuovi nelle liste dei cambiamenti; il motore usa comunque i valori **localmente** per applicare le policy. Attenzione: nomi dei campi nei JSONPath, regole e descrizioni rimangono leggibili e possono essere sensibili. Nessun report e' automaticamente pubblico/sicuro per l'upload solo grazie a questi flag.
- `--source-names none` omette i nomi dei file **solo nel report**; la CLI deve comunque leggerli per analizzare. SARIF non consente omissione dei nomi sorgente, perche' serve per collegare i finding ai file.
- SARIF associa ogni finding al file *alla riga 1*: e' una localizzazione approssimata. La maggior parte delle annotazioni precise nelle pull request richiede in seguito una mappa dai path JSON/YAML alle righe reali.
- Le integrazioni GitHub (`upload-artifact`, `upload-sarif`) sono **opt-in**. Le organizzazioni devono gestire permessi `security-events: write`, repository privacy, policy IDs e descrizioni. L'azione non trasferisce snapshot raw.
- Il contratto di report JSON resta `schemaVersion=1` e non aggiunge membri che romperebbero validatori esistenti con `additionalProperties: false`. `error` e `policy-validation` hanno schemi separati.
- Non e' stata integrata una credenziale o una licenza cloud; la CLI rimane locale e gratuita secondo MIT. Nessun endpoint web o database e' ancora attivo.

## Verifiche richieste prima di merge

L'ambiente di lavoro in questa sessione **non contiene dotnet** e il download dell'SDK non e' possibile per assenza di rete. I file sono stati sottoposti a controlli statici (YAML, XML, JSON schema/fixture, integrita' ZIP, BOM/CRLF), non a compilazione .NET reale. La pipeline Ubuntu/Windows deve completare `dotnet restore`, `build`, `test`, 17 scenari e smoke GitHub Action per considerare la modifica pronta alla pubblicazione.

## Prossimo incremento consigliato: P2

1. Estrarre DTO/validazione del report in un progetto `DriftGuard.Contracts` (senza cambiare il Core).
2. Progettare e implementare API ASP.NET Core con OIDC e isolamento per workspace. Il backend deve imporre quote, autorizzazione e limiti sul payload.
3. Aggiungere ingestione idempotente di report senza valori, PostgreSQL e audit trail; soltanto dopo esporre timeline/dettaglio in dashboard web.
4. Implementare `PlanCatalog`/`Entitlements` server-side Free/Basic/Pro *prima* del checkout billing, garantendo che la CLI offline rimanga utilizzabile senza account.
