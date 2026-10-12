# Proposta architettura web/SaaS per DriftGuard

**Stato: progettazione, nessuna API/web/DB presente nel repository attuale.** L'obiettivo è mantenere DriftGuard utile in CI senza rete e aggiungere un servizio online per collaborazione, cronologia e monetizzazione.

## Architettura logica

```text
                    Developer / CI pipeline
                            |
         +------------------+-------------------+
         |                                      |
  DriftGuard.Cli (MIT)                   Frontend Web
         |                                      |
  DriftGuard.Core (MIT)                  OIDC login
         |                                      |
  report JSON v1 (redatto)                       |
         |                                      |
         +------------ HTTPS --------------------+
                          |
                   DriftGuard.Api
       ASP.NET Core / REST, OIDC + RBAC, rate limits
                          |
          +---------------+-------------------+
          |               |                   |
  Ingestion Service  Entitlement Service   Billing Integration
  schema, dedupe,    Free/Basic/Pro        webhook firmati,
  tenant scope       capability + quota    riconciliazione
          |               |                   |
          +---------- PostgreSQL --------------+
                          |
                   Background Worker
             retention, trend, notifiche
                          |
        Object Storage (solo report redatti opzionali)
```

## Progetti futuri proposti

| Progetto | Responsabilità | Dipendenze ammesse |
|---|---|---|
| `DriftGuard.Core` | diff, parser, path e policy engine | Nessuna dipendenza SaaS |
| `DriftGuard.Cli` | interfaccia terminale e CI offline | Core |
| `DriftGuard.Contracts` | DTO e schema JSON condivisi | Minime/nessuna infrastruttura |
| `DriftGuard.Api` | endpoint versionati, auth e autorizzazione | Contracts + application services |
| `DriftGuard.Application` | casi d'uso, entitlement e orchestrazione | Core, Contracts, astrazioni |
| `DriftGuard.Infrastructure` | EF Core, PostgreSQL, provider billing, notifiche | Application |
| `DriftGuard.Worker` | retention, aggregazioni, eventi | Application + Infrastructure |
| `DriftGuard.Web` | dashboard responsiva (Blazor o Next.js) | API HTTP/DTO, **non** il database |

Il frontend è una decisione di prodotto: **Blazor** riduce il cambio di stack per un team C#, **Next.js/React** offre un ecosistema ampio per dashboard SaaS. Evitare due frontend in parallelo prima della validazione utente.

## Modello dati iniziale

```text
Workspace(id, slug, ownerUserId, createdAtUtc)
User(id, oidcSubject, email, ...)
Membership(workspaceId, userId, role)
Project(id, workspaceId, name, ...)
Environment(id, projectId, slug, ...)
PolicyVersion(id, workspaceId, projectId, version, hash, body, ...)
Run(id, workspaceId, projectId, environmentId, idempotencyKey,
    schemaVersion, sourceRef, createdAtUtc, result, reportHash, ...)
Finding(id, runId, ruleId, path, severity, changeKind, ...)
Subscription(id, workspaceId, planCode, status, periodStartUtc, periodEndUtc, ...)
UsageBucket(workspaceId, periodStartUtc, metric, used)
AuditEvent(id, workspaceId, actorId, action, createdAtUtc, metadataRedacted)
BillingWebhookEvent(providerEventId, receivedAtUtc, processedAtUtc, status)
```

Su ogni query tenant-owned, applicare filtro obbligatorio su `workspaceId`, con controlli applicativi **e** isolamento a livello dati quando appropriato. Non accettare `workspaceId` come prova di autorizzazione. Le chiavi API sono hashate a riposo e collegate a scope e workspace.

## Contratto di ingestione e API v1 (bozza)

| Endpoint | Ruolo |
|---|---|
| `POST /api/v1/projects/{projectId}/runs` | Carica un report DriftGuard JSON v1 già redatto, con idempotency key |
| `GET /api/v1/projects` | Progetti visibili all'utente/workspace |
| `GET /api/v1/projects/{projectId}/runs` | Timeline paginata dei run |
| `GET /api/v1/runs/{runId}` | Sommario e finding del run autorizzato |
| `GET /api/v1/me/entitlements` | Capability e quote del workspace corrente |
| `GET /api/v1/workspaces/{id}/usage` | Quota residua con metrica e periodo |
| `POST /api/v1/billing/checkout` | Avvio checkout lato server per un owner |
| `POST /api/v1/billing/webhooks` | Webhook autenticato, idempotente |

Al primo avvio, la CLI può caricare **solo l'output redatto** (`--format json`) e un identificatore run idempotente. La pipeline può continuare a eseguire policy e stabilire exit code anche se l'upload fallisce; l'eventuale errore di sync deve essere osservabile separatamente.

## Sicurezza obbligatoria prima del lancio web

1. **Data minimization:** nessun file baseline/current raw nel cloud per impostazione predefinita. La redazione attuale è euristica: richiedere un payload minimizzato senza valori o una strategia di redazione forte prima dell'ingestione cloud.
2. **Isolamento multi-tenant:** autorizzazione server-side su ogni risorsa e indice/unique key scoped per workspace, test di accesso incrociato.
3. **Identità:** OIDC, rotazione sessioni/token, API key con scope e scadenza, rate limits per workspace, controlli CSRF dove necessari.
4. **Billing:** verificare firma webhook, gestire replay ed eventi fuori ordine, mai fidarsi del redirect client per attivare entitlements.
5. **Upload:** limite payload, schema allowlist, verifica versione, scansione/filtri per stringhe pericolose nella UI, protezione contro injection e log forging.
6. **Storage:** cifratura in transito e a riposo, key management, retention effettiva, esportazione e cancellazione dati tenant.
7. **Operation:** backup e disaster recovery testati, observability senza dati sensibili, dashboard di errore tenant-safe.

## Primo flusso UX consigliato

1. Login e creazione workspace Free.
2. Creazione progetto e copia snippet CLI per CI.
3. Caricamento del primo report redatto e vista "Deployment safe / Needs review".
4. Timeline diff tra run, dettaglio finding e policy coinvolta.
5. Team invitation e notifiche (Basic); workflow di approvazione e audit export (Pro).

La promessa commerciale non è un diff visivo generico: è **ridurre il rischio dei cambiamenti di configurazione** e mantenere un audit consultabile delle decisioni in CI/CD.
