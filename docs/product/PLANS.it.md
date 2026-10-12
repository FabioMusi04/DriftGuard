# DriftGuard — proposta di piani Free, Basic e Pro

**Stato: proposta di prodotto, NON attivata nel codice.** DriftGuard CLI e Core sono attualmente rilasciati con licenza MIT. I piani qui sotto si riferiscono **esclusivamente al futuro servizio ospitato**, alle funzionalità collaborative e alle integrazioni gestite; non limitano retroattivamente l'uso della CLI esistente.

## Catalogo proposto

Le cifre sono **ipotesi di validazione** commerciale, non tariffe già pubblicate. Quote applicate **per workspace** e per periodo di fatturazione, non al codice open-source eseguito in locale.

| Funzionalità | Free | Basic | Pro |
|---|---|---|---|
| Prezzo indicativo | **€0** | **€19/workspace/mese** | **€79/workspace/mese** |
| Utenti workspace | 1 | 5 | 20 |
| Progetti cloud | 1 | 5 | 25 |
| Run caricati sul cloud al mese | 200 | 5.000 | 50.000 |
| Storico online | 7 giorni | 90 giorni | 365 giorni |
| CLI locale / JSON report | Sempre gratuita (MIT) | Inclusa | Inclusa |
| Dashboard web e cronologia | Base | Completa | Completa + analytics |
| GitHub/GitLab CI ingestion | Manuale / artifact | Automatica | Automatica + API |
| Policy custom nel workspace | Solo template locali | Sì | Sì + versionamento/approval |
| Notifiche Slack/email | No | Sì | Sì + regole avanzate |
| Ruoli e permessi | Owner | Owner + member | RBAC granulare |
| Audit log / export | No | Base | Avanzato |
| SSO organizzativo | No | No | Sì (dopo hardening) |
| Supporto | Community | Standard | Prioritario |

**Retention:** cancellazione tramite job schedulato e policy legale/privacy. **Overage:** bloccare solo l'ingestione cloud aggiuntiva, con messaggi chiari; non bloccare l'esecuzione locale in CI né cancellare risultati senza le regole di retention dichiarate.

## Confine fra licenza OSS e subscription SaaS

- `DriftGuard.Core` e `DriftGuard.Cli` rimangono utilizzabili con la licenza MIT del repository. Il contratto MIT non contiene meccanismi Free/Basic/Pro.
- Gli abbonamenti regolano il **diritto ad accedere alle funzioni web ospitate**. Prezzi, fatturazione e limiti devono stare nei Termini del Servizio del SaaS, in documenti separati dalla licenza software MIT.
- Non aggiungere un controllo centralizzato che disattivi il confronto locale quando una subscription scade o non è raggiungibile il server.
- Eventuali funzionalità desktop/CLI commerciali future devono avere una strategia di licensing autonoma, compatibile con i diritti degli autori e dei contributori open-source. Per licenze offline valutare token firmati con scadenza e chiave pubblica nel client; il controllo del backend resta comunque necessario per le risorse cloud.

## Modello logico degli entitlement

```text
PlanTier = Free | Basic | Pro

PlanDefinition
  code, displayName, catalogVersion
  limits: maxMembers, maxProjects, monthlyUploadedRuns, historyDays
  features: cloudDashboard, ciIngestion, teamNotifications,
            advancedRbac, auditExport, publicApi, sso, approvals

Subscription
  id, workspaceId, planCode, status, providerSubscriptionId,
  currentPeriodStartUtc, currentPeriodEndUtc, graceUntilUtc

WorkspaceEntitlement
  workspaceId, planCode, resolvedLimits, resolvedFeatures,
  effectiveAtUtc, expiresAtUtc

UsageBucket
  workspaceId, metric, billingPeriodUtc, consumed, updatedAtUtc
```

Il **catalogo è versionato**. I limiti effettivi possono includere add-on o eccezioni amministrative; non vanno incorporati in `if (plan == Pro)` all'interno dei controller. Usare un servizio `IEntitlementService` per valutare capability + quota e un `IUsageMeter` transazionale per consumare quote in modo atomico e idempotente.

## Stati dell'abbonamento e comportamento

- `free`: nessuna carta obbligatoria per iniziare.
- `trialing`: eventuale prova, chiaramente indicata e con transizione esplicita.
- `active`: entitlement del piano effettivo.
- `past_due`: grace period configurato; amministratore avvisato; nessuna interruzione delle verifiche locali.
- `canceled` / `expired`: dal termine del periodo, downgrade ai limiti Free, con accesso ai dati secondo retention contrattuale e possibilità di esportazione.

Upgrade immediato: quote ricalcolate idempotentemente; downgrade: preservare i dati per il tempo previsto ed evitare cancellazioni sincrone. Per un workspace oltre la quota del piano inferiore, mettere in sola lettura le risorse eccedenti fino a intervento dell'owner.

## Enforcement — solo server, testabile

```text
[API autenticata]
  -> risoluzione membership/workspace
  -> autorizzazione RBAC su workspace/progetto
  -> check entitlement per capability
  -> prenotazione/consumo quota atomica (con idempotency key)
  -> ingestione/report o risposta 403/429 con codice di errore stabile
  -> audit evento senza payload sensibile
```

Test negativi richiesti: tentativo di upload su un progetto di altro tenant; upgrade/downgrade concorrenti; retry dello stesso run; superamento soglia; reazione a webhook di billing duplicati e fuori ordine; impossibilità di aggirare le quote usando direttamente le API invece della dashboard.

## Indicatori economici da validare prima di fissare i prezzi

Costo storage per report, costo ingestione/run, dimensione media payload, numero reale di membri, conversione Free→Basic/Pro, retention utilizzata, supporto per workspace e necessità di SSO/retention. Rivedere i cap soprattutto con clienti che usano CI ad alta frequenza.
