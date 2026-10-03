# Personal program builder and favorites

Requested extension: create a program in the offline app and mark programs as personal favorites. Work directly on main; preserve the existing certificate and all personal data. Target release 1.2.1/code 5.

## Acceptance and decisions

- AC-30 / D-30: A local editor creates a named program, gym and multiple named sessions. Each session chooses existing exercises, with adjustable sets, repetitions and rest; sessions and exercises can be reordered/removed. Reject invalid bounds, duplicate exercises within a session and incompatible gyms. Never replace the active week or workout automatically.
- AC-31 / D-31: Closing/canceling/Android Back publishes nothing. Creation allocates independent program/session IDs and uses the existing mutation lock and single atomic save. A failed write retains the editor draft and leaves all published state unchanged.
- AC-32 / D-32: Built-in, imported and created programs can be starred. Favorites appear together for quick access, persist in personal settings and full backups, and are absent from program-sharing JSON. Optional favoriting during creation uses the same save. Malformed/stale favorite settings are tolerated.
- AC-33 / D-33: The created program appears in My programs, can be previewed, started, added to the week and exported/imported using existing flows. Keep exercise search local and gym-aware. No displayed RIR or personal weights in the builder.
- AC-34 / D-34: Unit/component and real-file round trips cover changes; review precedes the authorized local main commit. Verify signed AOT/trimmed Release and an emulator update from 1.2.0. Physical-phone results and GitHub delivery remain separate pending checks.

## Brownfield invariants

Domain records and schemaVersion 1 stay compatible. Program/session draft IDs are untrusted and regenerated. Settings key `training-favorite-programs` stores personal program IDs; program packages continue to contain definitions only. History, active drafts, catalog identities and week duplicates remain intact. No second persistence or locking mechanism is added.

## Dispatch and tasks

Standard mode. T-31 Application authoring/favorite operations and unit tests; T-32 App editor, star controls and component tests run concurrently in disjoint files. Root owns T-33 real-file/full-backup/privacy proof, docs and release. A fresh reviewer performs the .NET/micro review after integration. The dev-implement skill explicitly requests this bounded delegation; no Artelia board/work-item tooling applies to this personal repository.

| Acceptance | Task | Proof |
|---|---|---|
| AC-30/31 | T-31/32 | Authoring validation/save-failure tests; component editor/cancel/Back tests |
| AC-32 | T-31/32/33 | Favorite mutation and UI tests; native backup/file reopen and package privacy |
| AC-33 | T-32/33 | Library visibility, week/start/share component and integration proof |
| AC-34 | T-33 | Fresh suites, version/certificate/package verifier and emulator upgrade |

## Progress

- T-31 and T-32 complete. 363 Application, 137 App/component, 103 Infrastructure unit and six real-file integration cases pass: 609 total, no failures/skips. Eleven release-script checks pass.
- The actual named review-dotnet and review-micro-dotnet passes are complete, with no Critical/Important findings remaining. Fixed incompatible per-plan gym under overall Toutes, overlong rest, validation complexity and the concurrency proof; UI tests caught literal Razor IDs and skipped child refresh. Eight forged-null cases and a genuinely blocked first writer protect the final seams.
- Verification-before-completion: DoD PASS, AC-30 through AC-34 traced below. Android build passed with the same eight existing IL2026 warnings in personal persistence/migration; new favorite serialization uses generated metadata and adds no warning.
- Release 1.2.1/code 5: 30,923,371 bytes, SHA-256 `9e152135bba444b29e7e7ad317a93c0535598e34ba7b2c43f214ac99e19796f4`. Existing certificate, arm64-v8a/x86_64, embedded UI and AOT payload verified. APK is local under ignored outputs.
- API 36 emulator updated in place from 1.2.0, rendered Home without a startup crash, and retained the private file byte-for-byte: SHA-256 before/after `c6d0807319817266efd03ef64e216ba4dc950174d4b14429b8469eee519907f0`. Small-screen editor and sticky actions inspected; physical-phone behavior remains unverified.
- Native Release creation also passed: entered a synthetic program name, selected an exercise, checked Favorite and saved. The 4×10/90 s plan and favorite are persisted; original active workout, history and week remain unchanged. After force-stop/relaunch, the Favorites card and starred state reappear. The screenshot is a test-emulator artifact, not a physical-phone capture.
- Nice-to-have retained: old setting `Basic Fit` defaults this editor to One Air; Basic-Fit can be explicitly selected. Existing package limit 1 MiB intentionally rejects exceptionally large exports while keeping local data.
- Direct main local delivery remains authorized. No GitHub push or CI success is claimed; previous remote write denial and distribution-secret configuration remain pending.

## Final acceptance proof

| Acceptance | Passing test or actual QA | Status |
|---|---|---|
| AC-30 | WorkoutProgramAuthoringTests invalid/boundary/gym/clone cases; ProgramAuthoringUiTests.CreateProgram_MultipleSessionsAndPrescriptions_SaveOneAtomicFavoriteProgram and removal/search cases | OK |
| AC-31 | ProgramMutation_WhenSaveFails; pending-save and blocked-writer tests; CreateProgram_CancelOrAndroidBack_DiscardsDraftWithoutSaving and failed-save retry | OK |
| AC-32 | ToggleProgramFavoriteAsync persist/reopen; malformed/stale settings; Favorites_BuiltInToggle; ProgramAuthoringPersistenceTests.CreateProgramAsync_ReopenAndBackup_PreservesFavoritesWeekAndPersonalDraft | OK |
| AC-33 | Favorites_CustomProgramRemoval_StaysInMyProgramsWithPreviewWeekAndStart; immediate sharing export; CreateProgramAsync_ExportAndImportCopy_ExcludesFavoritesAndStartsWithPlannedSets | OK |
| AC-34 | Fresh suites, release verifier, version increment, actual smoke install-r/render and identical private data hashes | OK |

The Home free-workout selected-gym/one-confirmation/automatic-search regression passes; this feature remains on Home and is disabled while another workout exists.
