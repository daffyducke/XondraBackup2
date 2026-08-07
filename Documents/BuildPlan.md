# Xondra Engine — Phased TDD Build Plan

## Context

`XondraBackup2` currently holds no source code — it's `CLAUDE.md`, an
empty `src/` folder, `Documents/` (analysis of a legacy prototype that
lives elsewhere), and `Setup/` (working DDL/seed SQL for the two SQLite
databases). The goal is to build a first working version of Xondra — a
content-addressable, deduplicating Windows backup engine — from scratch,
strictly test-first, following the stack and conventions `CLAUDE.md`
already commits to: .NET 8/C#, AlphaFS/AlphaVSS for open-file/VSS support,
Microsoft.Data.Sqlite with parameterized queries, an engine with zero UI
dependency, xUnit + FluentAssertions.

`Documents/CodeReview-Summary.md` describes the *intended functional
behavior* of the legacy prototype (`Common.cs`) — used here as a guide to
what the engine should do, not code to port. Per `CLAUDE.md`, its UI
details are ignored, its SQL-string-concatenation pattern is the one flaw
being deliberately fixed (not carried forward), and the AES key/IV
derivation scheme it flagged as weak is instead an explicit, fixed
requirement to preserve exactly.

## Solution / folder structure

```
Xondra.slnx
src/
  Xondra.Engine/                 (class library, net10.0, no UI deps)
    Hashing/     Sha512Hasher.cs, HmacSha512Deriver.cs
    Crypto/      AesKeyDerivation.cs, BlobCodec.cs
    Storage/     BlobStore.cs                (hash-sharded dir layout)
    Data/
      SqliteSchemaInitializer.cs
      Resources/  (linked from ../../../Setup/*.sql, not duplicated)
      Catalog/    BackupRepository.cs, BackupSetRepository.cs,
                  FileRepository.cs, LocalDriveRepository.cs,
                  LocalDirectoryRepository.cs, LocalFilenameRepository.cs,
                  BackupSetEmptyDirRepository.cs, ErrorRepository.cs
      Config/     JobSettingsRepository.cs
    Scanning/    IFileSystem.cs, WindowsFileSystem.cs (AlphaFS-backed),
                 DirectoryScanner.cs, ScannedFile.cs, ScannedDirectory.cs
    Vss/         IVssSnapshotProvider.cs, AlphaVssSnapshotProvider.cs,
                 NullVssSnapshotProvider.cs
    Backup/      BackupConfig.cs, IncrementalPlanner.cs,
                 FileBackupWorker.cs, BackupRunner.cs
    Verify/      VerifyMode.cs, FileVerifier.cs
    Restore/     RestoreService.cs
  Xondra.Cli/                    (thin console harness)
    Program.cs
Testing/
  Xondra.Engine.Tests/            (xUnit, fast, fully headless — default run)
    Hashing/  Crypto/  Storage/  Data/  Scanning/  Backup/  Verify/  Restore/
    TestSupport/  TempDirectory.cs, SqliteTestDatabase.cs,
                  FakeVssSnapshotProvider.cs, FakeFileSystem.cs
  Xondra.Engine.IntegrationTests/ (real AlphaVSS, needs admin — run manually, not default)
  TestResults/                    (build output/coverage/logs)
Setup/            (existing — unchanged, single source of truth for DDL/seed SQL)
Documents/         (existing)
CLAUDE.md          (existing)
```

`src/` already exists (empty); `Testing/` needs to be created. DDL files
are embedded into `Xondra.Engine` via a linked `<EmbeddedResource>` so
there is exactly one copy of the schema, not one duplicated into the
library.

## Phases

Each phase is built strictly test-first: write the test, run it and
confirm it fails for the right reason, implement, run it and confirm it
passes.

- [x] **Phase 0 — Scaffolding.** `dotnet new sln`; `Xondra.Engine` class library
and `Testing/Xondra.Engine.Tests` xUnit project; wire references. NuGet:
`Microsoft.Data.Sqlite` (engine), `xunit`/`xunit.runner.visualstudio`/
`Microsoft.NET.Test.Sdk`/`FluentAssertions`/`coverlet.collector` (tests).
One trivial sanity test proves the harness is wired before real TDD starts.
Done: targets `net10.0`, not `net8.0` — only the .NET 10 SDK/runtime is
installed on this machine, and .NET 10 is itself the current LTS, so it's
a newer instance of the same "target the LTS" intent. Solution file is
`Xondra.slnx` (the SDK's current default format, not `.sln`). Pinned
`SQLitePCLRaw.bundle_e_sqlite3` to 3.0.5 directly to clear a known-high
transitive vulnerability warning on the version `Microsoft.Data.Sqlite`
pulls in by default. Sanity test: `EngineInfo.SchemaVersion == 1`.

- [x] **Phase 1 — Hashing primitives.** `Sha512Hasher` (file/stream → 128-char
hex), `HmacSha512Deriver`. Tests: known SHA-512 vectors, file/stream
hashes agree, HMAC is deterministic. Pure BCL crypto, no I/O beyond a
stream.
Done: `HmacSha512Deriver.Derive(key, content)` takes an explicit key rather
than deriving one from "part of the file hash" — `Common.cs` isn't in this
repo to confirm the exact byte-slicing convention against, so the key
convention is deferred to Phase 2/8 (`AesKeyDerivation`'s key bytes will be
passed in). Caught and fixed two mistyped SHA-512 test vectors (dropped
trailing hex digit) by cross-checking against .NET's own `SHA512`/
`HMACSHA512` output before trusting the "known vector" as ground truth.

- [x] **Phase 2 — AES key/IV derivation (fixed requirement).**
`AesKeyDerivation.Derive(hashHex)` → 32-byte key from the first 32
characters of the hash hex string (as ASCII bytes), 16-byte IV from the
first 16 characters of the *reversed* hash string. Tests assert exact
lengths/values against hand-computed expectations and that the IV really
comes from the reversed string. This reading is the only one that yields
correct AES-256/CBC lengths without an extra decode step — worth a quick
gut-check against your own memory of the original scheme before treating
Phase 2 as done, since `Common.cs` itself isn't in this repo to check
against directly.
Done: expected key/IV byte values in the tests were machine-computed
(PowerShell substring/reverse against a known SHA-512 hex string) rather
than hand-typed, after Phase 1 caught two hand-typed vectors being wrong.
A dedicated regression test (`Derive_iv_is_not_simply_the_first_16_...`)
guards against silently forgetting the reversal.

- [x] **Phase 3 — Compress-then-encrypt round trip.** `BlobCodec` streaming
GZip → AES CryptoStream and its reverse. Tests: round-trip byte equality
(empty/small/multi-buffer content), wrong-hash decrypt does not recover
original bytes, encrypted output ≠ plaintext. Pure `MemoryStream` in/out —
no filesystem or Windows dependency.
Done: all 5 tests passed on the first implementation attempt (`GZipStream`
→ `CryptoStream`, both wrapping the caller's streams with `leaveOpen: true`
so `BlobCodec` never closes streams it doesn't own). The wrong-hash test
catches any exception broadly, since either a bad GZip header or bad
PKCS7 padding is an equally valid proof the wrong key/IV can't decrypt.

- [x] **Phase 4 — Content-addressed blob store.** `BlobStore`: hash → 3-level
sharded path (`hash[0]/hash[1]/hash[2]/hash`), `Write`/`Read`/`Exists`.
Tests: correct shard path, byte-identical read-back, idempotent
double-write (backs the dedup skip-if-stored behavior tested in Phase 8).
Done: added the `TestSupport/TempDirectory` helper a phase earlier than
its first planned use, since all 4 tests here need a scratch directory.
`BlobStore` stays crypto-agnostic (opaque byte streams by hash) — no
knowledge of `BlobCodec`, matching the `Storage/` vs `Crypto/` split.

- [x] **Phase 5 — SQLite schema + repositories (the explicit fix-the-known-issue
phase).** `SqliteSchemaInitializer` runs the embedded DDL for first-run
bootstrap. Catalog repositories — `LocalDrive`/`LocalDirectory`/
`LocalFilename` (`GetOrInsert`), `FileRepository`, `BackupRepository`,
`BackupSetRepository` (including archive-bit copy-forward), plus
`BackupSetEmptyDirRepository`/`ErrorRepository` — every query
parameterized. `JobSettingsRepository` reads the `Settings_Json` view.
Tests include an explicit regression case: insert strings containing
`'`, `'; DROP TABLE File; --` etc. through the repositories and confirm
correct round-trip with no side effects — proof the string-concatenation
flaw is actually fixed, not just relocated. Temp-file SQLite DBs per test.
Done: `Xondra.dat.DDL.sql` and `Xondra.cfg.DDL.sql` are linked into
`Xondra.Engine` as `<EmbeddedResource>` with explicit `LogicalName`s
(not duplicated, per the folder plan); `Xondra.cfg.Data.sql` is
deliberately *not* embedded since it's real reverse-engineered seed data
(see the existing decision note below) — `JobSettingsRepository` tests
seed their own `Job`/`Attribute`/`Value` rows instead.
`SqliteSchemaInitializer` checks for a bootstrap table (`Backup` for the
catalog DB, `Job` for config) before running the DDL, so it's safe to call
on every startup. "Archive-bit copy-forward" landed as
`BackupSetRepository.CopyForward(backupSetId, newBackupId)` (clones an
existing row under a new `Backup`) plus `FindLatestForPath(driveId, dirId,
filenameId)` (locates the prior run's row for a given path) — the two
primitives Phase 7's `IncrementalPlanner` needs; the actual ARCHIVEBIT
branching logic is deliberately left for Phase 7. Test temp-file SQLite
connections need `Pooling=False` in the connection string — without it,
Sqlite's connection pool keeps the file handle open past `Dispose()` and
`TempDirectory`'s cleanup fails with a file-in-use error on Windows.
`Microsoft.Data.Sqlite`'s `SqliteConnection` has no `LastInsertRowId`
property (unlike `System.Data.SQLite`); every insert appends `SELECT
last_insert_rowid();` and reads it back via `ExecuteScalar()`, which
correctly skips the preceding `INSERT`'s empty result set. 63/63 tests
passing (22 prior + 41 new).

- [x] **Phase 6 — Directory scanning.** `IFileSystem` seam (enumerate, read
attributes/timestamps/size, get/clear Archive bit) with an AlphaFS-backed
`WindowsFileSystem` for long-path support; `DirectoryScanner` walks a
source root into in-memory `ScannedFile`/`ScannedDirectory` records
(no `FileCache`/`DirCache` staging table — see decision below). NuGet:
`AlphaFS`, introduced here. Tests: real temp directory tree with a
cleared-Archive-bit file and an empty subdirectory, asserted against
scanner output; a fake `IFileSystem` exercises error paths without real
ACLs. No VSS/admin required — Archive bit and attributes are plain NTFS
features.
Done: `AlphaFS 2.2.6` (the latest release; last published 2018) only ships
`.NETFramework` targets and restores under net10.0 via NuGet's compat
shim (`NU1701` warning on every build) — the same role `Alphaleonis.Win32.Filesystem`
served in the legacy engine, kept per `CLAUDE.md`'s explicit stack choice
rather than swapped for `System.IO`'s partial native long-path support.
`Alphaleonis.Win32.Filesystem.Directory`/`File`/`FileInfo` collide by name
with `System.IO`'s (pulled in by `ImplicitUsings`), so `WindowsFileSystem`
imports them under `AlphaDirectory`/`AlphaFile`/`AlphaFileInfo` aliases.
`ScannedDirectory` intentionally only ever represents *empty* directories
(no files, no subdirectories) — every other directory is already implied
by the `Directory` on the `ScannedFile`s inside it, matching what
`BackupSetEmptyDir` actually needs to store. `DirectoryScanner` catches
`UnauthorizedAccessException`/`IOException` per directory (enumeration)
and per file (stat) and continues the walk, collecting each into a
`ScanError` on the result rather than logging directly — the scanner has
no `Data` dependency, so persisting these through `ErrorRepository` is
Phase 10's job when it has a `Backup` row to attach them to. Verified the
happy-path test against the real filesystem through `WindowsFileSystem`
(not just the fake), so the AlphaFS compat-shim path is actually
exercised, not just assumed to work. 67/67 tests passing (63 prior + 4 new
— `DirectoryScannerTests` covers the real-tree walk, the
subdirectories-only-isn't-empty edge case, and both fake-filesystem error
paths in one file, not four).

- [x] **Phase 7 — Incremental (Archive-bit) planning.** `IncrementalPlanner`:
for `BackupType == "ARCHIVEBIT"`, files with the bit unset get their prior
`BackupSet` row copied forward (excluded from reprocessing); everything
else is processed. Tests seed a prior `Backup`/`BackupSet` row and assert
both branches.
Done: `IncrementalPlanner.Plan(backupType, newBackupId, files)` takes
`backupType` as a plain string rather than a `BackupConfig` object —
`BackupConfig` doesn't exist yet and is Phase 10's concern (job-settings
orchestration), so building it early just to pass one string through
would be a premature abstraction. Uses the Phase 5 primitives exactly as
planned: `FindLatestForPath` (resolving `Drive`/`Directory`/`Filename`
through the lookup repositories' `GetOrInsert`, since scan results carry
plain strings, not catalog IDs) to find the prior run's row, then
`CopyForward` to clone it under the new `Backup`. Lookup-table
`GetOrInsert` calls only happen for files actually eligible for
copy-forward (bit unset) — files with the bit set, or any file when
`BackupType != "ARCHIVEBIT"`, never touch `LocalDrive`/`LocalDirectory`/
`LocalFilename`, so a `"FULL"` run does zero incidental catalog writes.
A file with the bit unset but no prior `BackupSet` row (never backed up
before) correctly falls through to reprocessing — the "everything else is
processed" branch also covers that edge case, not just the changed-file
case. 71/71 tests passing (67 prior + 4 new).

- [x] **Phase 8 — Per-file backup worker.** `FileBackupWorker`: hash → HMAC →
dedup-lookup by `OriginalFileHash` → insert `File`/compress+encrypt into
`BlobStore` if new, else link-only → insert `BackupSet` → clear Archive
bit → catch and log per-file `IOException`/`UnauthorizedAccessException`/
general exceptions to `ErrorRepository` without aborting. Tests: new-content
path, duplicate-content path (two paths/one blob — the dedup proof),
failure path via a throwing fake `IFileSystem` that still lets the loop
continue.
Done: resolved the Phase 1 "HMAC key convention deferred to Phase 2/8"
note exactly as flagged — the HMAC key is `AesKeyDerivation.Derive(originalFileHash).Key`,
the same 32-byte key used for AES, not a separately managed secret.
`IFileSystem` gained `Stream OpenRead(string filePath)` (Phase 6 only
needed enumerate/stat/clear-archive-bit, not content access); `WindowsFileSystem`
delegates to `AlphaFile.OpenRead`, and `FakeFileSystem` gained
`AddFileContent`/`FailOpenRead` to back it. The "backup hash" that keys
the `BlobStore` blob is the SHA-512 of the *compressed+encrypted* bytes
(computed by buffering `BlobCodec.CompressThenEncrypt`'s output into a
`MemoryStream`, hashing it, then writing under that hash) — distinct from
`OriginalFileHash`, matching `CodeReview-Summary.md`'s "temp file is
renamed to its own SHA-512 hash" description; `BlobCodec.DecryptThenDecompress`
still needs the *original* hash (for the AES key/IV), not the backup hash,
which the new-content-path test round-trips end to end to prove. Dedup
lookup treats an existing `File` row with a still-`null` `BackupHash` as
"not actually stored yet" and re-runs the store step — a defensive branch
for a prior run that inserted the `File` row but crashed before
compressing/encrypting, so a future run doesn't perma-skip a blob that
was never written. `BackupFile(backupId, file)` processes exactly one
file and never throws (catches internally, logs to `ErrorRepository`,
returns normally) — the loop over multiple files lives in the caller
(`BackupRunner`, Phase 10), matching that phase's own "loop
`FileBackupWorker`" wording; the failure-path test builds its own
two-file loop to prove one bad file doesn't stop the next. 74/74 tests
passing (71 prior + 3 new).

- [ ] **Phase 9 — VSS seam.** `IVssSnapshotProvider` / `NullVssSnapshotProvider`
(passthrough for `UseVSS=false`) / `AlphaVssSnapshotProvider` (real).
NuGet: `AlphaVSS` (+ platform-native companion package — confirm current
package IDs at implementation time). Unit tests use a `FakeVssSnapshotProvider`
pointed at a temp directory to prove orchestration asks for/tears down a
snapshot correctly. A **separate** `Xondra.Engine.IntegrationTests`
project exercises the real `AlphaVssSnapshotProvider` against the actual
OS VSS service — needs admin elevation, run manually/on a dedicated leg,
not part of the default fast test run. This is the one piece that
structurally can't be unit-tested directly; the seam is what keeps
everything else testable.

- [ ] **Phase 10 — Backup orchestration (first end-to-end pipeline).**
`BackupRunner.Run(jobId)`: load settings → start `Backup` row → scan →
normalize lookups → incremental plan → resolve root via
`IVssSnapshotProvider` → loop `FileBackupWorker` → record empty
directories → mark done. On-disk mode only for v1. Test: full pipeline
over a real temp source/target tree + temp-file DBs + fake VSS provider,
asserting final `Backup` row/rows/blobs, and that a second run against an
unmodified tree takes the incremental path (no new blobs, rows copied
forward). Still fully headless.

- [ ] **Phase 11 — Verify.** `FileVerifier.VerifyFiles(mode)` for
`CurrentBackup`/`AllNotVerified`/`All`: decrypt+decompress to temp,
re-hash, compare to `OriginalFileHash`, write `LocalVerified`, clean up
temp file. Tests seed a good `File` row and a deliberately corrupted one,
assert both outcomes and that no temp files leak.

- [ ] **Phase 12 — Restore.** `RestoreService.RestoreFiles(backupId, targetRoot)`:
join scoped to one verified backup, recreate directories, decrypt/decompress
to original relative path, restore timestamps/attributes, re-verify and
delete-on-mismatch; `RestoreEmptyDirectories` for dirless directories.
Test: full backup→restore round trip (including duplicate-content files
and an empty directory) asserting byte-identical output and
timestamp/attribute preservation, plus a corrupted-blob case that deletes
rather than leaves a bad file.

- [ ] **Phase 13 — Thin CLI harness.** `Xondra.Cli`: `backup <jobId>`,
`verify <mode>`, `restore <backupId> <targetDir>` — glue only, no new
logic, not itself unit-tested. First thing that can exercise real
VSS/locked files/long paths by hand on an actual machine, ahead of any
GUI (which is out of scope for this plan).

- [ ] **Phase 14 (deferred) — In-memory mode.** `:memory:` staging connection +
SQLite's native online-backup API, flushed to disk every
`InMemoryBackupInterval` files per `InMemoryMode`. Only changes where the
connection points/when it flushes — Phases 5–12 repositories already work
against a plain `DbConnection`. Tests: rerun the Phase 5/10 suites
parameterized over on-disk vs in-memory-then-flushed, asserting identical
end state.

## Decisions made in this plan (flagging, not blocking)

- **No `FileCache`/`DirCache` SQLite staging tables in v1.** Scan results
  stay in-memory and feed the incremental planner/worker directly —
  simpler and more directly unit-testable. The DDL still defines these
  tables (harmless if unused); this is reversible later if crash-resumable
  scanning turns out to matter.
- **On-disk mode first, in-memory mode deferred** (Phase 14) — it's a
  connection-lifecycle optimization, not a functional-behavior change.
- **CLI harness, not a UI**, to keep the engine's headless/testable
  property real and give a concrete way to hand-test VSS before any GUI
  work begins.
- **Schema bootstrap only, no migration engine** — `SqliteSchemaInitializer`
  creates a fresh catalog DB; versioned migration of an existing `Xondra.dat`
  is out of scope for v1.
- **Root `Xondra.cfg`/`Xondra.dat` files**: these look like real
  reverse-engineered data (a real Windows username/path, a real
  `ComputerGUID`, per `Setup/Xondra.cfg.Data.sql`). They aren't used by
  the build and are worth moving/gitignoring before this becomes a shared
  or version-controlled repo — flagging, not acting on it here.

## Verification

Fast, default loop for every phase: `dotnet test Testing/Xondra.Engine.Tests`
— headless, no admin, runs in CI. Confirm red→green for each new test
before moving to the next phase (this is the TDD contract from `CLAUDE.md`,
not optional). After Phase 13, hand-verify the one thing unit tests can't
cover: run `Xondra.Cli backup` against a source directory containing a
file held open by another process, with `UseVSS=true`, on a real Windows
10+ machine, and confirm it's captured without error — this is the
concrete proof of the "copy open files" requirement the tech stack was
chosen for. Run `Xondra.Engine.IntegrationTests` (elevated) at least once
before considering VSS support done, since Phase 9's unit tests only prove
the orchestration seam, not the real VSS call.

### Critical files
- `CLAUDE.md` — stack, TDD workflow, fixed requirements
- `Documents/CodeReview-Summary.md` — functional behavior reference
- `Documents/Xondra.dat.ERD.md`, `Documents/Xondra.cfg.ERD.md` — schema reference
- `Setup/Xondra.dat.DDL.sql`, `Setup/Xondra.cfg.DDL.sql`, `Setup/Xondra.cfg.Data.sql` — schema/seed source of truth
