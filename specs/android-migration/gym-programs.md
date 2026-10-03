# Gym programs and sharing — approved implementation

Release: 1.2.0, Android code 4. Work directly on main. Preserve the existing certificate, personal data, old catalog IDs and all backup formats.

## Acceptance

- AC-23: One Air and Basic-Fit each provide Push, Pull, Legs and Upper in that order. One legs day; balanced shoulders. Main exercises use editable 4×10; compound rest 150 s, RDL 180 s, isolation 90 s.
- AC-24: One Air uses the confirmed machine inventory. Basic-Fit uses generic standard equipment without claiming a mapped machine brand. Starting a recommended session resolves to the selected gym. Changing settings never changes an active workout, and manual weeks are never automatically replaced.
- AC-25: Two optional abdominal exercises, two sets of 10–15 repetitions and 60 s rest each. One Air: Life Fitness Abdominal and reverse crunch; Basic-Fit: cable crunch and reverse crunch. Manual addition is atomic, excludes pending sets from totals, preserves other drafts/rest and cannot be duplicated after reopening.
- AC-26: Recommended cards appear before the folded old library. Applying a four-session week requires explicit confirmation. Existing plus, reorder and remove actions remain available.
- AC-27: Separate JSON program sharing selects the week or one/multiple programs. Envelope kind `performance-programs`, schemaVersion 1. Include required exercise definitions, gyms, machines, plans and optional exercises, excluding personal logs, weights, active workout, settings and diagnostics.
- AC-28: Import validates and previews before adding copies. Allocate new program/session IDs; reuse an exercise only when its ID and semantic definition match. Otherwise create a separate definition and remap references. Preserve duplicates/order, current week, active workout and history. Invalid files, cancellation and failed writes publish no changes.
- AC-29: Fresh unit/component/integration and release-script tests, reviewed seams, signed AOT/trimmed Release package and version/signature/architecture checks. Emulator upgrade proof is distinguished from unavailable physical-phone checks. GitHub delivery remains blocked by PAT write denial until rights are corrected.

## Design

Use the existing persisted aggregate and mutation lock. Domain adds nullable program Gym and an empty-by-default optional exercise list on sessions. State becomes partial to isolate gym operations and program import without adding another save mechanism. Package codec/use cases live in Application/Infrastructure; native picker/sharing and confirmation live in the App.

One Air core IDs: Push `lf-chest-press, fly, shoulder, lateral, lf-triceps`; Pull `pulldown, lowrow, rear, lf-biceps`; Legs `squat, rdl, lf-seated-leg-curl, legext, calf`; Upper `incline, lf-row, lf-lateral-raise, rear, overhead-a`. Retain every legacy definition and program. No automatic history/load aliasing between machines or gyms.

Basic-Fit core: chest press, pec deck, shoulder press, dumbbell lateral raise, rope pushdown; lat pulldown, cable row, reverse pec deck, cable curl; leg press, barbell RDL, leg curl, leg extension, calf raise on leg press; incline dumbbell press, cable row, dumbbell lateral raise, reverse pec deck, overhead cable triceps extension.

## Tasks and proof

- [x] T-26 Gym catalog/state transformations — catalog and state unit tests, including failed persistence and gym isolation.
- [x] T-27 Optional abdominal block — state/component tests for manual activation, repeat/reopen and unchanged rest/drafts.
- [x] T-28 Private program package/import — round-trip, semantic conflicts, duplicate order, malformed file and failed-write tests.
- [x] T-29 Recommended/sharing UI and native files — component tests for selections, confirmations and cancellation; Android rendering and picker opened on the Release APK.
- [x] T-30 Review and release — all four .NET suites, Python checks, version/certificate/ABI/AOT verification and emulator update with byte-identical data.

Mode: standard, brownfield compatibility. Root integrates and releases; bounded core, package and UI agents own disjoint files. Review precedes the authorized local commit.

## Verification receipt

530 .NET tests passed: 299 Application, 124 App/component, 103 Infrastructure unit and 4 real-file integration. 11 release-script tests passed. Review-dotnet and review-micro-dotnet passed after correcting native backup overlap, imported-program gym selection and canonical gym aliases. No remaining Critical/Important issues. The unavailable dedicated security/simplify skills were not claimed as invoked; the actual micro review assessed simplicity and directly checked package privacy, safe links and bounded deserialization.

| Acceptance | Passing proof |
|---|---|
| AC-23/24 | GymTrainingCatalogTests; GymTrainingStateTests role mapping, alias, legacy identity, unchanged active state and gym-specific charges |
| AC-25 | GymTrainingStateTests manual addition/reopen/remove/failed-write; TrainingProgramsUiTests optional counts; JsonPerformanceDataRepositoryTest real-file reopen |
| AC-26 | TrainingProgramsUiTests library folding, card plus, confirmation/Back and active-draft preservation |
| AC-27/28 | JsonProgramPackageCodecTests; WorkoutProgramSharingTests; TrainingProgramsUiTests export/privacy/preview/cancel/failed-save/imported Basic-Fit start |
| AC-29 | Full fresh suites; Release version/certificate/ABI/AOT verifier; API 36 emulator upgrade from 1.1.1 with identical private-data SHA-256 and rendered dashboard/Programs |

Release: 1.2.0/code 4, 30,510,801 bytes. SHA-256 `483865bc3cb111327344a073f564f1eee234cdcb60c3cbe86916c58aa183769a`. Existing signing certificate matches the repository public fingerprint. Eight IL2026 warnings come from existing reflection-based personal persistence/migration APIs, outside the new source-generated program codec. Current Release startup and reading of the old synthetic personal data were verified; full trimming changes remain a separate concern.

Physical-phone testing and remote CI/distribution remain unverified. GitHub write access is still pending from the previous 403; no remote delivery is claimed.
