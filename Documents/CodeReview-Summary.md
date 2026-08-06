# XondraBackup — C# Code Summary

This summarizes the actual backup/restore *logic* in the codebase. WinForms
UI code (Designer files, button wiring, dialogs) is intentionally skipped
except where it's the only place logic lives. Database schema details are in
[`Xondra.dat.ERD.md`](./Xondra.dat.ERD.md) and
[`Xondra.cfg.ERD.md`](./Xondra.cfg.ERD.md) — this doc explains the code that
reads and writes those schemas.

## Solution layout

| Project | Purpose | Status |
|---|---|---|
| `XondraBackup` | The actual backup engine + main app | Real logic lives here, in `Common.cs` |
| `Restore` | Standalone restore app | Empty stub — `Form1` has no code beyond `InitializeComponent()` |
| `ManageSettings` | Settings/job management UI | Empty stub — both forms are blank shells |

Within `XondraBackup`:
- **`Common.cs`** (~1600 lines) — the entire backup/verify/restore engine, as a
  plain `internal class Common` with no dependency on any Form. This is the
  code that matters.
- **`TEST_FORM.cs`** — the current main form (despite the name — set as the
  startup form in `Program.cs`). Thin: four button handlers that construct a
  `Common` + `BackupConfig` and call into `Common`.
- **`Form1 (1).cs`** (~1950 lines) — a superseded/legacy version of the same
  engine, with nearly identical methods (`BeginBackup`, `CompressThenEncrypt`,
  `DecryptThenDecompress`, etc.) implemented directly inside the form instead
  of the extracted `Common` class. It's not wired up in `Program.cs` and
  appears to be dead code left over from before the `Common`/`BackupConfig`
  refactor. Not summarized further since `Common.cs` is the live version.
- **`ConfigEditor.cs`** — empty stub form.

## Core data structures

- **`Common.BackupConfig`** — a mutable struct threaded through nearly every
  method (often `ref`), carrying the per-job configuration: source/target
  paths, `ComputerGUID`, VSS on/off, in-memory mode on/off, connection handles
  to the target SQLite DB and the config DB, running counters, etc. It's
  populated once per run by `FillBackupConfig` from the `Xondra.cfg` database
  and then mutated in place as the run progresses (`BackupID`, `BackupFileCount`).
- **`Common.VerifyMode`** enum — `CurrentBackup` / `AllNotVerified` / `All`,
  controlling which files `VerifyFiles` re-checks.

## Backup flow

Two entry points, `InMemoryBackup` and `OnDiskBackup`, both driven by a
`JobID` looked up against `Xondra.cfg`. They share the same steps but differ
in where the working SQLite database lives during the run:

1. Open `Xondra.cfg` (job/attribute config) and call `FillBackupConfig` to
   populate a `BackupConfig` for this job (source dir, target dir, VSS
   flag, in-memory flag, backup type, etc.).
2. Open/create the target database (`Xondra.dat` in the target directory) and
   insert a new `Backup` row (`StartBackup`), recording a JSON snapshot of the
   job's settings.
3. **`InMemoryBackup`** additionally opens a `:memory:` SQLite connection and
   copies the on-disk target DB into it (`BackupSQLDB`, which uses SQLite's
   native online backup API) so that all the cache/scan work below happens
   against RAM instead of disk, and periodically flushes back to disk (every
   `InMemoryBackupInterval` files, via `BackupFile`). **`OnDiskBackup`** just
   works directly against the on-disk DB.
4. Truncate `FileCache`/`DirCache`, then recursively walk `SourceDirectory`
   (`CacheFilesAndDirectories_Recursive`, using `Alphaleonis.Win32.Filesystem`
   for long-path support) inserting every file and directory found into those
   cache tables, tagged with the Windows Archive attribute bit, size,
   timestamps, and file attributes.
5. Normalize the caches: insert any new drive/directory/filename strings into
   `LocalDrive`/`LocalDirectory`/`LocalFilename` lookup tables, then back-fill
   the cache rows with the resulting IDs (`InsertNewLocalDrive` →
   `UpdateFileCacheWithLocalDriveID`, and the directory/filename equivalents).
6. If `BackupType == "ARCHIVEBIT"` (incremental mode), run
   `InsertIntoBackupSetWhereArchiveBitIsNotSet` — a single SQL statement that
   (a) copies forward `BackupSet` rows for any file whose Archive bit is
   *not* set (i.e. unchanged since the last backup, so it's just re-linked to
   the new `Backup` row rather than re-copied) and (b) deletes those rows from
   `FileCache` so the copy step below only processes files that actually
   changed.
7. **`PerformBackup`** branches on `BC.UseVSS`:
   - **VSS path** (`DoVssBackup`): initializes the Windows Volume Shadow Copy
     Service (via the `Alphaleonis` VSS wrapper), creates a snapshot of the
     source volume, exposes it as a network share, then reads files from the
     shadow copy so open/locked files can still be backed up consistently.
     Deletes the shadow copy when done.
   - **Non-VSS path** (`DoNonVssBackup`): reads files directly from the live
     volume.
   - Both funnel into **`BeginBackup`**, which iterates every row currently in
     `FileCache` and calls `BackupFile` per file, catching and logging
     `IOException`/`UnauthorizedAccessException`/general exceptions per-file
     (via `InsertErrorIntoDB`) so one bad file doesn't abort the whole run.
     Afterward it records empty directories (`InsertEmptyDirectories`) and
     calls `VerifyFiles` for the files just backed up, then marks the backup
     `"Done!"`.
8. **`BackupFile`** (the per-file unit of work):
   - Hashes the source file with SHA-512 (`GetSHA512Hash`) and derives an
     HMAC-SHA512 (`GetHMACSHA512`, keyed off part of the file hash itself —
     see security note below).
   - Looks up the file by content hash in the `File` table; if new, inserts a
     row (dedup point — identical content backed up from different
     paths/times collapses to one `File` row, matching the ERD's dedup
     design).
   - Inserts a `BackupSet` row linking this `Backup` run to the
     drive/directory/filename/file IDs and file attributes/timestamps.
   - If the file's content hasn't been physically copied before
     (`File.BackupHash` still unset), calls `CompressThenEncrypt` to actually
     store it; otherwise just marks the `BackupSet` row as
     "previously backed up" (`-1`) and skips the copy — this is the content
     dedup in action.
   - Clears the Windows Archive bit on the source file (`ClearArchiveBit`) so
     the next incremental run can detect "unchanged since last backup" via
     that bit.
   - Increments file counters; in in-memory mode, periodically flushes the
     in-memory DB back to the on-disk target.

## Storage format: `CompressThenEncrypt` / `DecryptThenDecompress`

Each file is stored once, content-addressed by its backup hash, independent
of original path:
- Encrypt with AES (`AesCryptoServiceProvider`), where **both the key and IV
  are derived from the plaintext file's own SHA-512 hash** — key is the first
  32 bytes of the hash (as hex), IV is the first 16 bytes of the *reversed*
  hash string. This is convenient (no separate key management/storage needed
  to decrypt — you just need to already know the original file's hash from
  the DB) but means the encryption key is derived from public, low-entropy
  material relative to a cryptographic key, and reusing part of the same hash
  for both key and IV is unusual practice.
- Compress with `GZipStream`, chained through a `CryptoStream`, streamed
  directly from the source file to a temp file (compress-then-encrypt, single
  pass).
- The temp file is renamed to its own SHA-512 hash, and stored under a
  3-level subdirectory computed from the first three hex characters of that
  hash (`hash[0]/hash[1]/hash[2]/hash`) — a classic hash-sharding layout to
  avoid huge flat directories.
- `File.BackupHash` and `File.FilesizeCompressed` are updated in the DB to
  point at the stored blob.
- `DecryptThenDecompress` reverses this exactly: locate the blob by
  `BackupHash`, decrypt with AES using the *original* file's hash (not the
  backup hash) to regenerate the same key/IV, decompress, write to the
  requested destination path.

## Verification

`VerifyFiles(mode)` selects candidate `File` rows per `VerifyMode` (just this
backup's files / all unverified ever / literally all files), decrypts each
one to a throwaway temp path, recomputes its SHA-512, compares against the
stored `OriginalFileHash`, records the pass/fail into `File.LocalVerified`,
then deletes the temp file. This is a full round-trip integrity check of the
stored, encrypted blob against what was originally captured — it doesn't just
trust the copy succeeded.

## Restore

`RestoreFiles(BackupID)` queries `FilesToRestore` (a join across
`BackupSet`/`LocalDrive`/`LocalDirectory`/`LocalFilename`/`File` scoped to one
backup run, filtered to files that passed verification) and for each row:
recreates the target directory if needed, decrypts/decompresses the file to
its original relative path, restores the original creation time, last-write
time, and file attributes, then re-verifies the SHA-512 hash of the restored
file — if it doesn't match, the restored file is deleted rather than left
corrupted. `EmptyDirectoriesToRestore` separately recreates directories that
had no files but need to exist.

## Data access pattern

All database access is hand-written ADO.NET against `System.Data.SQLite`
(`SQLiteConnection`/`SQLiteCommand`/`SQLiteDataAdapter`) — there's no ORM.
Every query is built via **string concatenation**, with a `SanitizeSQL`
helper that escapes single quotes before interpolating string values into
SQL. This covers the common case but is not equivalent to parameterized
queries; it's the main correctness/security soft spot in the data layer, and
worth keeping in mind since some inputs (like the on-disk source file path
and directory names) come from the actual filesystem being backed up, not
just from user-entered settings.

Two SQLite databases are used per run:
- `Xondra.cfg` — read-only config/job/attribute store (see ERD).
- `Xondra.dat` (in the target/backup directory) — the backup catalog: `Backup`
  runs, `BackupSet` entries, the dedup `File` table, and the
  `FileCache`/`DirCache` scan staging tables (see ERD). `VacuumDB` is called
  at the end of every backup run to reclaim space.

## Notable rough edges observed while reading

- `Form1 (1).cs` is dead/duplicate code (see above) — safe candidate for
  deletion if confirmed unused, since `Common.cs` supersedes it entirely.
- SQL is built by string concatenation everywhere rather than parameterized
  queries (see above).
- AES key/IV derived from the plaintext's own hash rather than a
  separately-managed secret — anyone who can compute a file's SHA-512 (i.e.
  anyone with the original file) can derive the key without needing anything
  from the backup store itself; this is fine for content-integrity/dedup
  purposes but does not provide confidentiality against someone who already
  has the source file, and derives both key and IV from overlapping key
  material.
- Several call sites (`InMemoryBackup`, `OnDiskBackup`, `BeginBackup`) pop a
  `MessageBox.Show(...)` on completion — a UI dependency baked into
  otherwise-headless backup logic, which would need to be removed for this
  engine to run as a real background/service job.
