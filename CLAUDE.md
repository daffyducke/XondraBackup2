# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project overview

Xondra is a content-addressable, deduplicating backup engine for Windows.
A backup job walks a source directory, hashes each file (SHA-512), and
stores each unique content blob exactly once — compressed then encrypted —
regardless of how many paths/times that content appears under. Two SQLite
databases drive it:

- `Xondra.cfg` — job/attribute configuration (which directories to back up,
  VSS on/off, backup type, etc.), read at the start of a run.
- `Xondra.dat` — the backup catalog written in the target directory: backup
  run history, the file/directory inventory, and the dedup `File` table.

Key behaviors of the existing engine (see [Architecture](#architecture)):

- **Open/locked files**: backups can run against a Windows VSS (Volume
  Shadow Copy Service) snapshot of the source volume, so files held open by
  other processes are still copied consistently.
- **Incremental backups**: uses the Windows Archive attribute bit —
  unchanged files are re-linked to the new backup run instead of re-copied,
  and the bit is cleared after each successful copy.
- **Dedup**: identical file content is stored once and referenced by any
  number of backup runs/paths; already-stored content is detected by hash
  and skipped rather than re-compressed/re-encrypted.
- **Verification**: every stored file can be round-tripped (decrypt +
  decompress + re-hash) and checked against its original hash, independent
  of whether the copy step reported success.
- **Restore**: reconstructs original paths, timestamps, and attributes from
  the catalog, and re-verifies each restored file's hash before leaving it
  in place.

A prior code-reading pass produced a written analysis of the actual C#
implementation (`Common.cs`) and flagged its rough edges (SQL built by
string concatenation rather than parameterized, an , UI (`MessageBox`) calls embedded in otherwise
headless backup logic). See `Documents/CodeReview-Summary.md` for the full
detail — treat those as known issues to design around, not to repeat.  Use `Common.cs` as a guide to the desired funcionality, do not copy it as is.

The AES key/IV is derived from the plaintext's own hash is a requirement.

## Architecture

Architecture, schema, and data-flow documentation lives in `Documents/` and
is a suggestion — read it there rather than duplicating it here:

- [`Documents/CodeReview-Summary.md`](Documents/CodeReview-Summary.md) —
  walkthrough of the backup/verify/restore engine: solution layout, backup
  flow, the compress-then-encrypt storage format, verification, restore,
  and the data access pattern.  Ignore the information about the UI.
- [`Documents/Xondra.dat.ERD.md`](Documents/Xondra.dat.ERD.md) — entity
  relationship diagram and notes for the backup catalog database
  (`Backup`, `BackupSet`, `File`, dedup design, lookup tables).
- [`Documents/Xondra.cfg.ERD.md`](Documents/Xondra.cfg.ERD.md) — entity
  relationship diagram and notes for the job/attribute configuration
  database, including the `Settings_Json` / `JobAttributes` views.

The corresponding DDL/data scripts are in `Setup/` (`Xondra.dat.DDL.sql`,
`Xondra.cfg.DDL.sql`, `Xondra.cfg.Data.sql`) if the exact schema is needed
alongside the ERDs.

## Recommended tech stack

Target platform is Windows 10 and newer, and the engine must be able to
copy files that are open/locked by another process. Recommendation:

- **.NET (current LTS), C#** — matches the existing codebase's language and
  keeps the door open to reusing/porting logic from `Common.cs`. Target
  whatever LTS SDK is actually installed on the build machine (`net10.0`
  as of this build — see `Documents/BuildPlan.md` Phase 0).
- **AlphaFS / AlphaVSS** for Volume Shadow Copy access (required to copy
  open files) and long-path filesystem support — the same role the legacy
  `Alphaleonis.Win32.Filesystem` dependency served. This is the one piece
  with no good pure-BCL substitute: .NET has no built-in VSS API.
- **Microsoft.Data.Sqlite** for both SQLite databases, with all queries
  parameterized — fixes the string-concatenation SQL flagged in the code
  review rather than carrying it forward.
- **Engine as a plain class library with no UI dependency** — a direct fix
  for the `MessageBox.Show` calls embedded in the legacy engine. The
  backup/verify/restore logic should be callable and fully testable
  headless; any UI (WPF, or none at all for a service/CLI) sits on top and
  only handles presentation.
- **xUnit** for unit tests, **FluentAssertions** for readable assertions.

## Development workflow: test-driven development

All new functionality is built test-first:

1. Write a unit test for the behavior being added.
2. Run it and confirm it fails (for the right reason — not a compile error).
3. Write the minimal code to make it pass.
4. Run the test again and confirm it passes.

Unit tests live in an xUnit test project alongside the engine (e.g.
`Testing/Xondra.Engine.Tests/`). Test artifacts (build output, coverage
reports, run logs) are kept under a top-level `Testing/` directory, kept
separate from application source.
Explain new unit tests before running them the first time.

Prompt to commit changes to github for each phase and all unit tests are passing.

## Code style

Prefer easy-to-read code over clever code. Keep comments minimal — only
add one where the *why* isn't obvious from the code itself (a non-obvious
constraint, a workaround, a subtlety a reader would otherwise trip on).
Don't add comments that just restate what a line does.
