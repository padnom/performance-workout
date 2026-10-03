# Android migration

## Resume

- Branch: `main` (direct delivery explicitly requested by the user).
- Base commit: `697eb59`
- Repository: `/mnt/d/Repos/performance-workout/` (moved by the user).
- Status: Release 1.1.1/code 3 delivered; session parity, discreet optional vibration, offline charts and top-pinned controls implemented. Repository cleanup and structured Performance.slnx verified with 448 .NET tests and 11 release-script checks. Android is the main project; the web reference is archived in legacy/web. Direct delivery on main is explicitly authorized by the user; GitHub push is pending because the supplied PAT was denied write access (403).
- Next action: install `outputs/performance-android-1.1.1.apk` over the existing app and verify the toolbar on the physical phone; the supplied phone screenshot still shows the old bottom toolbar. T-07 remains open. Configure the existing signing certificate in CI secrets before a distributed CI release. Import a complete web backup to recover an existing PWA week.
- Updated: 2026-10-03

The existing MAUI project had navigation and backup import/export, but training pages were placeholders.
The React PWA remains the behavioral reference under `legacy/web`, and the existing backup format remains supported.
The user confirmed a C# development profile and that the application will no longer be hosted; MAUI Blazor Hybrid remains the selected framework.

[Proposal](proposal.md) · [Design](design.md) · [Tasks](tasks.md) · [Progress](progress.md)
