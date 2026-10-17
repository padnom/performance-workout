# Session sharing and single-session builder

Requested extension: export and import one session (structure only), create a single session ahead of time, and share
easily through WhatsApp. Work directly on main; preserve the existing certificate and all personal data. Target release
1.3.0/code 6. Route: Brownfield.

## Completed

- Branch: `main`
- Status: implemented, reviewed and verified on the host; release 1.3.0/code 6 set. Android compilation of MainActivity and device QA remain pending (no Android SDK on this machine).
- Next action: on the SDK machine, build the signed Release APK 1.3.0, run `verify-release-apk.py`, install over 1.2.1 and run the WhatsApp cold/warm QA from README-ANDROID.
- Updated: 2026-10-17

## Acceptance and decisions

- AC-40 / D-40, D-41, D-49: A history entry (free workout included) exports a `performance-session` v1 JSON through the share sheet: ordered exercises, completed set count, repetition target, rest and gym. No weight, completed reps or RIR.
- AC-41 / D-42, D-43: A program session opened in SessionStart (preview or confirmation) exports the session resolved for the selected gym, optional exercises included. No export for the empty free workout.
- AC-42 / D-50: "Partager en texte" shares the name and one readable line per exercise, without loads.
- AC-43 / D-44, D-45: Importing a valid session file and confirming appends it to the personal program `my-sessions` / « Mes séances » (created on demand). Identical definitions are reused, unknown ones become personal exercises. History, week, active workout and favorites are unchanged; Cancel/Back changes nothing.
- AC-44 / D-40, D-51: Invalid, oversized (> 1 MiB), unknown-field, wrong version or wrong kind files are rejected with a message and no change. A program file chosen through "Importer une séance" is rejected clearly.
- AC-45 / D-46, D-47: "＋ Créer une séance" opens the editor in single-session mode with the program builder validations and saves into « Mes séances ». Cancel/Back publishes nothing; a failed write keeps the draft.
- AC-46 / D-44, D-48: A « Mes séances » session can be previewed, started (default gym = gym of its plans) and added to the week; the program can be starred.
- AC-47 / D-51, D-52: On Android, opening or sharing a Performance `.json` from WhatsApp or the file manager opens the import preview (session or programs by kind), cold and warm. Any other JSON shows a message and changes nothing.
- AC-48 / D-45, D-53: Persisted format, backups, `performance-programs` format and program import behaviour are unchanged; existing suites stay green.

Settled questions: intent filters accept `application/json` and `application/octet-stream` (VIEW and SEND); history export uses the completed set count; release 1.3.0/code 6.

## Brownfield invariants

Domain records and `PerformanceData` schemaVersion 1 stay unchanged; « Mes séances » is an ordinary `WorkoutProgram` capped at 100 sessions so it stays exportable as a program. IDs coming from a file are regenerated (`imported-session-…`, `imported-exercise-…`, `custom-session-…`). Every operation is one `MutateAsync`; a failure publishes nothing. Version 1.2.1 rejects a session file through its kind check.

## Tasks

- [x] #1 T-41 SessionPackage contract, validation, export from log/plan, text summary, shared-file reader
- [x] #2 T-42 Bounded text reader, incoming share service, session/text file sharing
- [x] #3 T-43 ImportSessionAsync, CreateSessionAsync, « Mes séances »
- [x] #4 T-44 JsonSessionPackageCodec
- [x] #5 T-45 Export buttons in history and SessionStart, default gym
- [x] #6 T-46 Session import preview, incoming file routing, single-session editor
- [~] #7 T-47 Android intent filters and DI
- [~] #8 T-48 Review, mutations, release 1.3.0 verification

| Acceptance | Decision | Design element | Task | Proof | State |
|---|---|---|---|---|---|
| AC-40 | D-40, D-41, D-49 | `SessionExport.FromLog`, `ShareSessionAsync` | T-41, T-42, T-45 | SessionPackageTests FromLog cases, SessionExportUiTests history export | OK |
| AC-41 | D-42, D-43 | `SessionExport.FromPlan`, SessionStart button | T-41, T-45 | SessionPackageTests FromPlan cases, SessionExportUiTests preview/confirm/free | OK |
| AC-42 | D-49, D-50 | `SessionText.Format`, `ShareTextAsync` | T-41, T-42, T-45 | SessionPackageTests text cases, UI text share test, WhatsApp QA | OK |
| AC-43 | D-44, D-45 | `ImportSessionAsync`, ProgramSharing preview | T-43, T-46 | WorkoutSessionSharingTests import cases, SessionImportUiTests confirm/cancel | OK |
| AC-44 | D-40, D-51 | validation, `JsonSessionPackageCodec`, `ReadSharedFileUseCase` | T-41, T-44, T-46 | JsonSessionPackageCodecTests, SessionImportUiTests invalid | OK |
| AC-45 | D-46, D-47 | `CreateSessionAsync`, single-session `ProgramDraft` | T-43, T-46 | WorkoutSessionSharingTests create cases, SessionImportUiTests create session | OK |
| AC-46 | D-44, D-48 | « Mes séances » in PersonalPrograms, SessionStart default gym | T-43, T-45 | state start/week tests, UI default gym test, APK QA | OK |
| AC-47 | D-51, D-52 | `IncomingShareService`, MainActivity, MainLayout | T-42, T-46, T-47 | IncomingShareServiceTests, SessionImportUiTests incoming + MainLayout navigation; Android build and cold/warm QA pending | Partial |
| AC-48 | D-45, D-53 | unchanged persisted format, extracted mapping | T-43, T-48 | existing suites green, mapping mutation | OK |

## Progress
- [x] #1 T-41 and #4 T-44 — uncommitted
  decision: implemented sequentially by one implementer agent instead of parallel waves (shared working tree on main: concurrent builds would invalidate each other's runs). FromPlan keeps optional exercises optional (agent first flattened them; fixed with a red test). ReadSharedFileUseCase routing tests live in Infrastructure.UnitTests (Application tests do not reference Infrastructure).
  files: Application/SessionPackage.cs, Application/ProgramPackage.cs, Infrastructure/JsonSessionPackageCodec.cs, SessionPackageTests.cs, JsonSessionPackageCodecTests.cs
  proof: Application 386/386, Infrastructure 116/116. Whole-solution build needs AndroidSdkDirectory (XA5300); test projects build their own dependencies.
- [x] #2 T-42, #3 T-43, #5 T-45, #6 T-46; [~] #7 T-47 (code written, Android compilation pending); [~] #8 T-48 (review done, device QA pending)
  decision: four sequential Sonnet implementer agents, root review between batches. FromPlan keeps optional exercises optional. Default gym from plans only for « Mes séances » sessions. Incoming share never leaves the workout screen; history relaunches and non-content URIs ignored; shared files routed by kind so a broken session file keeps its specific error. SessionPackage split into SessionPackage/SharedFile/SessionPackageValidation/SessionExport/SessionText; SharedFile is a SharedSession/SharedPrograms union; SessionSharer shared by history and SessionStart.
  reviews: review-dotnet (5 Important fixed), review-micro-dotnet (split, union, duplication, diagnostics fixed; FormatException pattern and ProgramSharing size kept as existing conventions), security-review and simplify run inline (no Agent fan-out for reviews): no security finding; three reuse fixes; a flaky no-leak assertion (GUID could contain "100") made deterministic.
  proof: Application 410, Infrastructure 118, Integration 6, App 181 passed (RC=0); 11 release-script tests; version 1.2.1/5 -> 1.3.0/6 verified. Mutations red then restored: weight leak (4), import into new program (3), skipped creation validation (3), no definition reuse (6), name bound (1), text placeholder (1), file-name trim (1), default gym (2).
