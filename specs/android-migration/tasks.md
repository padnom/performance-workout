# Tasks

- [x] #1 T-01 Port offline catalog and variants from the React definitions.
- [x] #2 T-02 Implement and test workout persistence, resumption and gym separation.
- [x] #3 T-03 Replace placeholder screens with functional training/catalog/history/progress pages.
- [x] #4 T-04 Harden and test backup migration and persistence integration.
- [x] #5 T-05 Compile Android, verify rendered components and produce the installable APK.
- [x] #6 T-06 Review the final seams, record checks and delivery evidence.
- [ ] #7 T-07 Run physical-phone/WebView checks from `dotnet/README-ANDROID.md` (phone not connected).
- [x] #8 T-08 Restore the original logo in the Android launcher, splash screen and header after repository relocation.
- [x] #9 T-09 Restore automatic rest, duration selection, extension and persisted deadlines, including backup compatibility.
- [x] #10 T-10 Add recent session history during workouts and gym-specific exercise records.
- [x] #11 T-11 Isolate timer rendering, cache catalog lists and validate the optimized Release APK.
- [x] #12 T-12 Make total elapsed workout time clearly visible immediately after confirmation and on reopening, without displaying the start time.
- [x] #13 T-13 Remove RIR from workout entry, preserve existing data and produce the updated optimized APK.
- [x] #14 T-14 Restore the active week with ordered/duplicate sessions, persisted edits and explicit session start.
- [x] #15 T-15 Implement the compact week editor and classic/targeted category/variant library with inline details and integrated plus buttons.
- [x] #16 T-16 Restore separate Home navigation/dashboard, history metrics and week cards, with active-workout resume and actual route refresh.
- [x] #17 T-17 Complete session replace/edit/remove/create parity and atomic per-gym changes.
- [x] #18 T-18 Add optional native Android rest notifications and lifecycle/back handling.
- [x] #19 T-19 Split route pages, add sticky workout controls, collapsed cards and four tabs plus More.
- [x] #20 T-20 Add local diagnostics, version 1.1.0/code 2 and verified Release automation.
- [x] #21 T-21 Review and test the new seams; deliver signed Release APK with in-place emulator update proof.
- [x] #22 T-22 Restore deployed Progress overview, program/muscle bars and exercise chart; test calculations/rendering and deliver Release 1.1.1/code 3.
- [x] #23 T-23 Pin compact workout/rest controls at the top, replace cumulative tonnage with useful set metrics and verify Android scrolling/keyboard.
- [x] #24 T-24 Archive the web reference, clean generated files, refresh Android documentation and commit/push the verified current branch.

| Acceptance | Implementation | Proof |
|---|---|---|
| AC-01 | T-01 | catalog tests and embedded JSON |
| AC-02/03/04 | T-02/03 | workflow unit tests and UI behavior checks |
| AC-05 | T-03 | rendered-component tests and manual device checklist |
| AC-06 | T-04 | migration and disk persistence round-trip tests |
| AC-07 | T-05 | Android build, APK signature/package inspection |
| AC-08 | T-09 | state/UI/migration tests; emulator automatic rest, extension and reopening |
| AC-09 | T-10 | populated history and gym-record component tests; emulator modal access |
| AC-10 | T-11 | parent-render and cache invalidation tests; Release AOT package and emulator navigation |
| AC-11 | T-12 | immediate confirmed-start, reopened-duration and isolated-render component tests; Android display check |
| AC-12 | T-13 | hidden-RIR and preserved-value component test; four-column Android display check |
| AC-13 | T-14 | week state/migration/disk tests and UI addition/order/removal/start/failure tests; Android week display and reopening |
| AC-14 | T-15 | WeeklyPlanPageTests family/category/reset, actual variant exercises, independent plus, next-variant wrap, custom program and failed-write cases; TrainingPageTests confirmation; Android compact editing and detail screenshots |
| AC-15 | T-16 | DashboardPageTests metrics/window boundaries/goal, navigation content refresh, week duplicates/confirmation, free session, resume/draft preservation, active-week editing, failed start, finish and latest gym; Android Home/Programs/Resume screenshots |
| AC-16 | T-17 | WorkoutSessionEditingTests and WorkoutEditingUiTests: replacement/duplicates/gym drafts/resize/create/cancel/failed writes; emulator editor |
| AC-17 | T-18 | RestNotificationTests and NotificationSettingsTests: opt-in/refusal/re-arm/extension/stale callbacks/claim/background ownership; native Android receipts in progress.md |
| AC-18 | T-19 | Separate route components, navigation/Back/free-start/collapse component tests; native modal Back and free-start search verified |
| AC-19 | T-17/20 | WorkoutTransforms, WorkoutHistoryIndex and one repository lock; diagnostics bounded/payload exclusion/export opt-in tests |
| AC-20 | T-20/21 | Release package/certificate/ABI/AOT/version verifier and emulator launch; CI distribution secrets explicitly pending |
| AC-21 | T-22 | WorkoutProgressTests, ProgressPageTests and native Android chart/navigation/update receipts in Progress |
| AC-22 | T-23 | WorkoutParityTests.Workout_ToolbarPrecedesCardsAndRetainsRestActions, DashboardPageTests seven-day set metrics, SessionHistoryTests no volume; native sticky/keyboard measurements |
