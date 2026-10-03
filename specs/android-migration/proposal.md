# Objective

Resume the migration to a local Android .NET MAUI Blazor Hybrid application usable without hosting.

- AC-01: the built-in React exercise/program/variant catalog is available offline with stable IDs.
- AC-02: training starts explicitly and cannot silently replace an unfinished workout.
- AC-03: each entered set is saved locally and an unfinished workout survives reopening.
- AC-04: completing a workout preserves prior history and isolates loads by gym.
- AC-05: exercise catalog, custom exercises, history and progress screens show actual data.
- AC-06: web JSON backups remain importable; Android data can be exported and reimported.
- AC-07: an installable signed diagnostic APK is produced; phone validation is recorded separately.
- AC-08: entering repetitions starts the exercise rest automatically; duration, +30 seconds, stop and the persisted deadline remain available after reopening.
- AC-09: recent session history is accessible without ending the active workout; exercise records remain separated by gym.
- AC-10: timer ticks avoid rendering the whole workout, repeated catalog reads avoid rebuilding lists, and an optimized APK is produced; actual phone navigation latency is validated separately.
- AC-11: confirmed workout start immediately shows a clearly labelled elapsed total; reopening resumes the original duration. Do not display the start time, as requested by the user.
- AC-12: workout entry contains weight and repetitions without a RIR column; editing these fields preserves previously stored RIR values.
- AC-13: show the ordered active week, support repeated sessions, adding/reordering/removing occurrences and explicit start; preserve/import/export the week locally without replacing a workout or history.
- AC-14: show a compact week editor with same-row up/down/remove controls; browse classic/targeted families, categories and A/B/C/D variants with their actual exercise details. Keep a plus inside each card, explicit start confirmation and access to custom programs.
- AC-15: add a distinct Home navigation button and dashboard showing completed sessions over seven rolling days, a four-session goal, volume over thirty rolling days, free training and ordered week cards. Keep Programs as the editor/library. Navigation must actually update content and preserve an active workout, its draft and elapsed start; expose Resume and return home after completion.

- AC-16: replace, edit, remove and create exercises during a workout with the web fields, duplicate protection, stable position, gym-specific load history and atomic draft changes. Cancelling an editor changes nothing; old RIR data stays stored but hidden.
- AC-17: optional rest feedback is off by default. Enabling requests Android permissions explicitly; a silent notification and one 200 ms vibration work independently of Blazor in the background. Extend reprograms; stop/remove/replace/finish/cancel/disable cancel the reminder. Current persisted token suppresses stale callbacks and duplicate feedback.
- AC-18: distinct Home/Programs/Workout pages preserve routes. Four direct tabs plus Plus, compact persistent workout controls, manual card collapse, one-confirmation free start opening search and modal-first Android Back preserve the active draft across interruption.
- AC-19: one save/lock mechanism remains authoritative; pure workout/week transforms, centralized history index and notification coordination are independent of pages. Diagnostics export is voluntary and contains operation durations and exception types, never session payloads.
- AC-20: deliver signed Release 1.1.0/code 2 with AOT/trimming, both ABIs, compatible current certificate and in-place emulator update. Automate tests, monotonic version checks, package/signature checks and actual emulator launch; distribution secrets remain an explicit setup requirement. Physical-phone checks are recorded separately.

No remote backend, accounts or multi-device synchronization are needed for this local application.

- AC-21: restore the deployed Progress screen: 28-day completed-session/entered-set/average-duration summary, program-family bars, seven primary muscle-area bars and missing/light-area insight. Exercise progress uses full chronological gym-specific history, maximum load, start/change/record and accessible dated values; zero/one-point and flat curves work offline. Retain existing record search and hidden legacy RIR. Deliver the compatible updated APK with an incremented version.
- AC-22: following user feedback, put the elapsed-workout and rest controls in a compact top toolbar that stays visible during scrolling and keyboard use. Retain Finish, set count, rest extension and stop. Remove cumulative weight/tonnage from Home, Progress and History; show weekly completed sets instead. Do not display cumulative time in the gym; preserve the current-workout chronometer, per-workout duration and average duration.
