# DriftGuard — analisi tecnica e bugfix (patch 0.1.1)

**Ambito:** repository consegnato come archivio `tests.zip`, sorgenti `.NET 10`, senza dipendenze di rete a runtime. Questa review è una valutazione statica del progetto, non una certificazione di sicurezza o un audit di produzione. I problemi marcati come corretti hanno patch nel codice e test aggiunti; l'esecuzione dei test resta da verificare con il .NET SDK.

## Scopo del prodotto

DriftGuard confronta una configurazione di riferimento (`baseline`) con una proposta (`current`), estrae il **drift semantico** su file JSON e YAML e produce report leggibili o JSON. Facoltativamente applica regole di policy su percorso, operazione, severità e nuovo valore. Permette ai team .NET/DevOps di bloccare un deployment rischioso in CI prima del rilascio; non è uno scanner completo dei segreti, un sistema di merge o un motore di compliance su configurazioni immutate.

## Architettura attuale

```text
src/DriftGuard.Cli/
  Program.cs          Comandi compare / check / explain / init / version, exit code
  Arguments.cs        Parser argomenti e opzioni
  ReportWriter.cs     Renderer console e contratto report JSON
  PolicyTemplates.cs  Policy di esempio
  BuildInfo.cs        Versione tool e versione schema report
       | usa
       v
src/DriftGuard.Core/
  ConfigParser.cs     Parsing JSON/YAML, tipi e limiti
  InputSafety.cs      Lettura bounded e JSON con chiavi univoche
  DiffEngine.cs       Differenze strutturali, ordinamento deterministico
  JsonPath.cs         Generazione e matching deterministico dei percorsi
  PolicyLoader.cs     Caricamento e validazione policy
  PolicyEngine.cs     Matching delle regole, anche nei sottoalberi
  ValueRedactor.cs    Mascheramento best effort
  ChangeModels.cs     Tipi di cambiamento e finding
  PolicyModels.cs     Contratto policy
       ^
       |
tests/DriftGuard.Core.Tests/  Test unitari, incluse regressioni
scenarios/                 17 fixture baseline/current
scripts/                   Runner scenari per Bash e PowerShell
.github/workflows/ci.yml   Pipeline build + unit test + scenari
```

Flusso `check`: argomenti → lettura con limiti → parsing → diff → validazione policy → valutazione delle regole → redazione report → exit code (`0`, `1`, `2`). `compare` non blocca per la sola presenza di differenze; `explain` filtra e spiega i cambiamenti.

## Problemi identificati e interventi

| Priorità | Problema precedente | Impatto | Patch |
|---|---|---|---|
| **P0** | Aggiungere `{ "logging": { "debug": true } }` come oggetto intero generava soltanto un evento su `$.logging`, così la regola su `$.logging.debug` non interveniva | Bypass della gate di rilascio | `PolicyEngine`: valutazione anche su discendenti di oggetti/array aggiunti, rimossi e sostituiti per type change, conservando compatto il diff |
| **P0** | Rimuovere tutto `features` poteva evitare la regola su `$.features.billingEnabled` | Bypass della gate di rilascio | Stessa espansione semantica per le rimozioni |
| **P1** | Glob `$.**.password` non intercettava `$.password` al primo livello | Falsi negativi | `JsonPath`: segmentazione e `**` = zero o più segmenti, senza regex/backtracking |
| **P1** | Il pattern `$.config.*` non intercettava correttamente chiavi con punto in bracket notation | Falsi negativi | Matching per segmenti e corretta gestione di `['nome.con.punto']` |
| **P1** | Escape incompleto del backslash nei nomi proprietà | Percorsi ambigui o collisioni nei report | `JsonPath.AppendProperty` centralizzato e test su apostrofi/backslash |
| **P1** | Severità numeriche come `"999"` erano accettate da `Enum.TryParse` | Risultati non previsti e report incoerenti | `PolicyLoader`: whitelist esplicita `info`, `warning`, `error` |
| **P1** | I JSON con chiavi duplicate non venivano rifiutati preventivamente | Interpretazione ambigua dei parametri | `InputSafety`: scansione preventiva di tutti gli oggetti JSON, incluse le policy |
| **P1** | File-size verificato prima della lettura, ma senza un limite rigoroso durante la lettura | File modificabile tra check e read | `InputSafety.ReadBoundedFile`: stream limitato anche durante la lettura |
| **P1** | Alias YAML espandibili in molti nodi | Consumo eccessivo di memoria/CPU | Budget di 100.000 nodi e 10 MiB di testo scalare espanso |
| **P1** | Opzioni valide in altri comandi (`--force` su `check`, per esempio) venivano ignorate | Errori di configurazione invisibili | Validazione opzioni per comando, `init --help`, errore su argomenti extra a `version` |
| **P1** | `compare --fail-on error` senza policy non poteva trovare finding e passava sempre | Gate illusorio | Ora viene rifiutato con exit code `2` se non è specificata una policy |
| **P2** | Percorsi e descrizioni non sanificati nel testo terminale | Possibile alterazione log tramite caratteri di controllo | Escape di controlli, direzionalità e separatori Unicode su output testuale |
| **P2** | `explain` sul figlio di un oggetto aggiunto o sostituito poteva dire “nessun cambiamento” | UX inesatta | Include anche il cambiamento sull'antenato pertinente e valida la sintassi del path |
| **P2** | Il workflow GitHub invocava uno script Bash direttamente, ma l'archivio non garantiva il bit eseguibile | Possibile fallimento di CI per permessi | Il workflow usa esplicitamente `bash ./scripts/run-scenarios.sh` |
| **P2** | Nessuna informazione esplicita sulla versione del contratto report o sull'applicazione della policy | Integrazioni web fragili | Aggiunti `schemaVersion: 1` e `policyApplied` al report JSON; versione CLI portata a 0.1.1 |

L'espansione delle policy è **solo interna al motore delle regole**: il formato delle `changes` rimane compatto per gli oggetti aggiunti/rimossi. Le `findings` possono tuttavia contenere percorsi discendenti non presenti come singoli elementi in `changes`, perché rappresentano proprietà contenute nel subtree modificato. Questo è intenzionale e va rispettato nel frontend.

## Protezioni di risorse e limitazioni residue

- `ConfigParser`: 5 MiB a file, profondità massima 128, JSON duplicate keys vietate; YAML espanso con limiti separati.
- `PolicyLoader`: 1 MiB, massimo 500 regole, path massimo 512 caratteri, descrizione massimo 2048 caratteri.
- `PolicyEngine`: massimo 20.000 percorsi valutabili (inclusi i discendenti sintetici), 2.000.000 confronti regola/percorso, 100.000 finding. Se ecceduti: **errore esplicito / exit 2**, mai `pass` silenzioso.
- **Da affrontare:** redazione dei segreti ancora euristica; valori non riconosciuti e nomi di proprietà/file potenzialmente sensibili possono arrivare nei report. Non caricare snapshot grezzi sul futuro SaaS.
- **Da affrontare:** il tool verifica *solo i cambiamenti*. Una configurazione già insicura ma invariata non genera finding. Per enforcement completo serve una modalità di scansione dello stato corrente.
- **Da affrontare:** YAML complesso, alias/tag insoliti e valori numerici devono essere coperti con fuzzing; il budget dopo il parsing non equivale a un sandbox della libreria YAML.
- **Da affrontare:** per grandi repository servono benchmark, test di carico, streaming e un design per gli output estesi.
- **Da affrontare:** test end-to-end reali su Linux, Windows e macOS, più test di proprietà contro i casi borderline del JSONPath-like.

## Verifica effettivamente eseguita in questo ambiente

- Lettura e analisi di README, minacce, policy, scenari, sorgenti e test.
- Controllo sintattico dei runner shell (`bash -n`), del JSON/XML dei file di supporto e della struttura degli artifact.
- **Non eseguiti:** `dotnet restore`, `dotnet build`, `dotnet test`, script `run-scenarios` e verifica runtime dei risultati: nel container non è installato `dotnet` e i repository per scaricarlo non sono raggiungibili.

Verifica obbligatoria prima del merge:

```bash
dotnet restore DriftGuard.sln
dotnet build DriftGuard.sln --configuration Release --no-restore
dotnet test DriftGuard.sln --configuration Release --no-build
bash ./scripts/run-scenarios.sh
```

Su Windows eseguire `scripts/run-scenarios.ps1`. Verificare che gli scenari 13, 14, 15 e 17 producano **exit 1** e il 16 **exit 2**; lo scenario 15 non deve stampare alcuna password dimostrativa. Validare anche un report `--format json` rispetto a `docs/contracts/report.v1.schema.json`.

## Compatibilità della patch

- Comandi principali ed exit code restano invariati; `0.1.1` corregge la semantica del motore di policy.
- Cambia intenzionalmente il matching del glob ricorsivo e il comportamento fail-closed su input non validi.
- Vengono aggiunti campi nel report JSON; consumatori rigidi che rifiutano proprietà non note devono essere adeguati.
- Le policy con glob molto generici (`$.**`) possono generare finding su più discendenti quando viene aggiunto un intero oggetto. Rivedere regole e soglie prima di attivare enforcement rigido.
- Il formato `version=1` delle policy non viene modificato.
