# DriftGuard — roadmap ingegneristica e di prodotto

Questa roadmap propone un passaggio progressivo da CLI open-source a piattaforma web **multi-tenant** con piani Free, Basic e Pro. Non descrive funzionalità SaaS già implementate. Le priorità sono ordinate per rischio e dipendenze tecniche; le condizioni di completamento sono verificabili.

## P0 — Affidabilità e CI del motore (patch 0.1.1, da verificare in CI)

- [x] Correggere i bypass delle policy su oggetti aggiunti/rimossi o sostituiti per cambio di tipo.
- [x] Correggere `**` e i segmenti JSONPath-like con nomi speciali.
- [x] Rendere rigorosi severità, duplicate JSON keys, limite dei file e risorse YAML.
- [x] Rifiutare opzioni non applicabili e soglie di blocco senza policy.
- [x] Aggiungere test unitari e 5 scenari di regressione.
- [ ] **Gate di merge:** build, unit test e 17 scenari eseguiti su .NET 10; esiti verificati su una CI reale.
- [ ] Affiancare fuzz/property-based test (parser, wildcard, redazione) e benchmark riproducibili.

## P1 — Contratti pubblici e integrazioni DevOps

1. Consolidare report JSON v1 con schema, fixture, golden tests e regole di versionamento. Aggiungere un'opzione valori-free che ometta completamente i contenuti raw dai report destinati al cloud.
2. Creare comando `validate-policy` ed errori strutturati (`--format json` anche per errori d'uso), senza mutare i significati degli exit code.
3. Pubblicare un GitHub Action wrapper che esegue la CLI versionata e carica report redatti come artifact opzionale.
4. Aggiungere SARIF e annotazioni dei pull request; definire strategia deterministica per gli identificativi dei finding.
5. Introdurre test d'integrazione CLI (process execution, stdout, stderr, exit code) e test multipiattaforma.

**Done:** una pipeline può produrre un report compatibile e testato con `schemaVersion=1`, senza caricare file di configurazione grezzi.

## P2 — Backend e primo prodotto web

1. Estrarre contratto report in `DriftGuard.Contracts` e mantenere `DriftGuard.Core` privo di dipendenze web.
2. Realizzare `DriftGuard.Api` in ASP.NET Core con autenticazione OIDC, workspace, progetti, ambienti e RBAC minimo.
3. Definire PostgreSQL per dati tenant, metadati e cronologia; job asincroni per ingestione e aggregazione.
4. Endpoint di upload **di report redatti** con idempotenza, limite payload, verifica schema/versione e chiave API a scope ridotto.
5. Dashboard: lista progetti, timeline drift, severità, dettaglio run e confronto tra ambienti.
6. Osservabilità: log strutturati senza valori segreti, metriche, tracing e audit di accesso.

**Done:** un utente autenticato visualizza solo i report del proprio workspace; nessuno snapshot di configurazione non redatto è conservato per impostazione predefinita.

## P3 — Licensing e billing Free / Basic / Pro

1. Realizzare `PlanCatalog`, `Entitlements`, `Subscription`, `UsageMeter` come dominio applicativo server-side; partire dal catalogo proposto in `docs/product/PLANS.it.md`.
2. Implementare conteggi transazionali per run/mese, progetti, membri e retention; usare timezone/periodo di billing coerenti.
3. Gestire trial, upgrade/downgrade, pagamento, errori, rinnovi, rimborsi e grace period, tramite un provider PCI-compliant.
4. Webhook con verifica firma, idempotenza, retry, audit e riconciliazione degli stati di pagamento.
5. Endpoint `GET /api/v1/me/entitlements` per UI e CLI con credenziali di servizio per CI; **enforcement nel backend**, non nella sola UI.
6. Permettere uso locale gratuito della CLI anche in assenza della rete e nei casi di guasto del billing cloud.

**Done:** test di integrazione confermano limitazione quote, isolamento tenant, upgrade/downgrade senza perdita dati e autorizzazione coerente su API e UI.

## P4 — Feature di differenziazione a pagamento

- **Basic:** GitHub/GitLab CI ingestion, gestione policy sul web, notifiche Slack/email, collaborazione team, cronologia più lunga.
- **Pro:** RBAC avanzato, audit immutabile/esportabile, API integrativa, SSO/OIDC organizzativo, policy versionate e approval workflow.
- Analisi multi-ambiente, baselines approvate e riepiloghi delle modifiche per release.
- Aggregazioni e trend: ricorrenza di warning, rischi per progetto, ambienti più instabili.
- Scanner dello stato corrente separato dal diff (policy su valori invariati) con semantica esplicita.

**Done:** gli entitlement sono verificati con test di autorizzazione negativi, non solo nascondendo elementi del frontend.

## P5 — Scalabilità, enterprise readiness e go-to-market

- Fuzzing e security review esterna del parser, redattore e API.
- SLO, backup, disaster recovery, controlli su tenant e retention automatica.
- Supporto Kubernetes/Terraform/Helm solo con un modello canonico e test contro casi reali.
- Documentazione SLA, privacy, GDPR, subprocessors, export/delete e politiche di conservazione.
- Metriche di prodotto: attivazione CLI → primo run CI → primo workspace → conversione Free/Basic/Pro.

## Regole architetturali da non compromettere

1. **Core riutilizzabile**: nessuna dipendenza da web, provider pagamento o identità nel motore diff.
2. **Prodotto sicuro by default**: Cloud riceve report redatti e metadati minimi, non credenziali e snapshot raw.
3. **CI indipendente dal SaaS**: una mancata risposta del cloud non deve invalidare un controllo locale già eseguito.
4. **Licenze server-side**: entitlements su API, job e risorse; il frontend fornisce solo visualizzazione.
5. **Versionamento esplicito**: schema report, API e policy hanno evoluzioni compatibili e testate.

Per schema dati e servizi vedere `docs/architecture/WEB-SAAS.it.md`; per quote e piani vedere `docs/product/PLANS.it.md`.
