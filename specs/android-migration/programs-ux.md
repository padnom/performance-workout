# Programs page: pick a session and start it

User story: as an athlete, I open Programs to pick a session that suits me today and start it. I don't plan a week,
so the page should not make me scroll past weekly planning, gym programs and sharing blocks to find a session.

## Status

- Branch: `ccr-f782f96a-gf0xn0`
- Status: implemented and verified on the host (unit and bUnit tests, rendered markup checked at phone width).
  Android device QA pending.
- Mockup validated by the user: https://claude.ai/artifact/Kn9BB3NHaxchr2CdaGJVJ9
- Tracking: this spec only, no GitHub issue.
- Updated: 2026-11-21

## Acceptance criteria

- Programs shows one library: a search field, filters (Tous · ★ Favoris · Mes séances · current gym · Classiques ·
  Ciblées) and every startable session grouped by program, each group with its favorite star.
- Groups: personal programs (Mes séances, created and imported programs), the recommended program of the current gym,
  then the classic and targeted families (their variants only, as before).
- A session row shows its name, up to three primary muscles and its exercise count. Tapping the row opens the
  existing preview (exercises, gym, export, start); ▶ opens the start confirmation directly.
- Search matches the program name, session name and focus, and the names and tags of the session's exercises.
- The ⋯ button opens a bottom sheet: Créer un programme, Créer une séance, Importer des programmes, Importer une
  séance, Exporter / partager. Android back closes it.
- Exporter / partager is a bottom sheet listing programs; "Ma semaine" is no longer offered.
- The weekly plan is no longer shown anywhere (Home and Programs). Home keeps the 7-day metrics, Séance libre and a
  "Choisir une séance →" link.
- Compatibility: the stored week (`react-week-plan`) and the week services are unchanged, so backups, migration and
  existing data keep working; nothing is deleted.

## Implementation

- `Components/ProgramLibrary.razor`: flat grouped library with search and filters (`Groups`, `VariantsFor`).
- `Components/ProgramsMenu.razor`: the ⋯ sheet, registered with `AppNavigationService` for Android back.
- `Components/ProgramSharing.razor`: export becomes a sheet (`OpenExport`); `PickAsync` / `PickSessionAsync` are
  called from the menu and refresh the component themselves.
- `Pages/Programs.razor`: heading + menu + library; `Components/Dashboard.razor` / `Pages/Home.razor` drop the week.
- Removed from the UI: `WeeklyPlan.razor`, `GymProgram.razor`, `PersonalPrograms.razor` and their styles.

## Verification

- App (bUnit): 204/204, Application: 474/474, Infrastructure: 118/118 + 6/6 integration.
- New tests: family and gym filters, search, row preview, ▶ confirmation, menu back, export without the week, gym
  group following the current gym, stored week kept but hidden.
- A test run caught a real bug: imports started from the ⋯ menu did not show their preview until the sharing
  component refreshed itself.
