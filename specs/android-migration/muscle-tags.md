# Muscles come from tags only

User story: as an athlete editing my catalog, I want to describe an exercise with tags only, so I don't type the
same muscle twice and the free-text "Muscle principal" field stops drifting ("Epaile", "Isciaux", "Ischio"...).

## Status

- Branch: `ccr-f782f96a-gf0xn0`
- Status: implemented and verified on the host (unit and bUnit tests). Android device QA pending.
- Tracking: this spec only, no GitHub issue.
- Updated: 2026-11-21

## Acceptance criteria

- The exercise editors (during a workout and on the Exercices page) no longer show a "Muscle principal" field.
  Both use the same tag picker: predefined chips plus an editable comma-separated list.
- Saving requires at least one muscle tag; the message is "… choisis au moins un muscle dans les tags."
- The primary muscle is the first muscle tag. When an existing exercise is opened, its primary muscle is moved first,
  and a known stored muscle with no matching tag is added as a tag (e.g. "Haut pectoraux" becomes "Pectoraux").
- Everywhere a muscle is shown or searched (exercise card, pickers, program library, catalog) and the Progress
  "séries par muscle" chart use the primary muscle read from tags.
- Mistyped legacy values map to a known muscle: "Epaile" → Épaules, "Isciaux"/"Ischio" → Ischio-jambiers,
  "Mollet" → Mollets, "biceps" → Biceps.
- Compatibility: `muscleGroup` stays in backups, program and session files. Editors write it as the first muscle tag;
  imports and the React migration are unchanged. Older data whose stored muscle is still tagged keeps that muscle
  as primary, so existing progress counts do not move.

## Implementation

- `Performance.Domain/ExerciseMuscles.cs`: known muscles, alias matching, `Primary`, `EditableTags`, `FromTags`.
- `Components/TagPicker.razor`: shared tag picker; `ExerciseEditor.razor` and `Pages/Exercises.razor` use it.
- `WorkoutProgress.AreaFor` maps the primary muscle to an area (Grand dorsal/Trapèzes → Dos, Arrière d'épaule →
  Épaules are now counted).

## Verification

- Application: 474/474, App (bUnit): 210/210, Infrastructure: 118/118.
- Checked against a real backup (25 custom exercises): every primary muscle resolves to a known muscle.
