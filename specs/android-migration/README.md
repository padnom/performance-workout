# Android migration

## Resume

- Branch: `main` (direct delivery explicitly requested by the user).
- Base commit: `697eb59`
- Repository: `/mnt/d/Repos/performance-workout/` (moved by the user).
- Status: Release 1.2.0/code 4 built and verified: gym-specific four-session programs, optional abdominal exercises and private program sharing implemented. 530 .NET tests and 11 release-script checks pass. Review passed after three compatibility/gym fixes. The Release APK updates 1.1.1 on the API 36 emulator while preserving the private data file byte-for-byte. Android remains the main project; legacy/web is preserved. Direct main delivery is authorized; GitHub push remains pending from PAT write denial (403).
- Next action: install `outputs/performance-android-1.2.0.apk` over the existing app and verify programs, optional abs and native sharing on the physical phone. T-07 remains open. Configure the existing signing certificate in CI secrets before a distributed CI release; correct GitHub write rights for main push.
- Updated: 2026-10-03

The existing MAUI project had navigation and backup import/export, but training pages were placeholders.
The React PWA remains the behavioral reference under `legacy/web`, and the existing backup format remains supported.
The user confirmed a C# development profile and that the application will no longer be hosted; MAUI Blazor Hybrid remains the selected framework.

[Proposal](proposal.md) · [Design](design.md) · [Tasks](tasks.md) · [Progress](progress.md)

[Approved gym/program-sharing plan and verification receipt](gym-programs.md)
