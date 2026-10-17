# Android migration

## Resume

- Branch: `main` (direct delivery explicitly requested by the user).
- Base commit: `697eb59`
- Repository: `/mnt/d/Repos/performance-workout/` (moved by the user).
- Status: Release 1.2.1/code 5 built and verified: local multi-session program creation and personal favorites, 609 .NET and 11 script checks passed. Review-dotnet/micro and DoD passed; the existing signing certificate, both architectures and AOT payload are verified. In-place emulator update from 1.2.0 preserves the private file byte-for-byte. Existing 1.2.0 remains available as rollback. Direct main local delivery is authorized; GitHub push remains pending from PAT write denial (403).
- Next action: install `outputs/performance-android-1.2.1.apk` over the existing phone app and check builder, favorites, keyboard and free-workout access. Physical-phone T-07 and remote T-24 remain open; configure the existing certificate secrets for CI distribution.
- Updated: 2026-10-03

The existing MAUI project had navigation and backup import/export, but training pages were placeholders.
The React PWA remains the behavioral reference under `legacy/web`, and the existing backup format remains supported.
The user confirmed a C# development profile and that the application will no longer be hosted; MAUI Blazor Hybrid remains the selected framework.

[Proposal](proposal.md) · [Design](design.md) · [Tasks](tasks.md) · [Progress](progress.md)

[Approved gym/program-sharing plan and verification receipt](gym-programs.md)

[Personal program builder and favorites](program-builder.md)

[Session sharing and single-session builder](session-sharing.md)
