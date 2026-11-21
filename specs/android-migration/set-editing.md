# Add or remove a set during a workout

User story (GitHub issue draft): as an athlete running a workout, I want to add or delete a set on an exercise, so
my log matches what I actually did without leaving the session.

## Status

- Branch: `main`
- Status: implemented and verified on the host (unit and bUnit tests). Android device QA pending.
- Tracking: this spec only, no GitHub issue.
- Updated: 2026-11-01

## Acceptance criteria

- Each exercise card of the active workout shows a "+ Ajouter une série" button under its sets.
- The added set is appended at the end, takes the weight of the last set, and leaves reps empty.
- Each set row has a delete button (×) that removes exactly that set; the following sets shift up.
- An exercise keeps at least 1 set and at most 20 (same bounds as the planned set count); the buttons are
  disabled at the limits and the service rejects with `LastSet` / `SetLimit`.
- The change applies only to the gym currently selected for the exercise; other gym drafts stay untouched.
- The draft is persisted immediately; the program definition is not modified.

## Implementation

- `WorkoutStateService.AddSetAsync` / `RemoveSetAsync` resize the active draft (`ResizeSets`).
- `WorkoutExerciseCard.razor`: add-set button, per-row delete button; `WorkoutUi.Error` maps the new tokens.

## Verification

- Application: 418/418, App (bUnit): 184/184, Infrastructure: 118/118 + 6/6 integration.
- Mutations seen red: copying the first set's weight instead of the last, and removing the last set instead of the
  targeted one.
