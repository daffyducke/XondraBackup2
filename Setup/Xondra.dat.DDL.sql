-- ============================================================================
-- Xondra.dat — DDL
--
-- Backup run history and the file/directory inventory that supports it:
-- Backup runs, the BackupSet (and BackupSetEmptyDir) rows produced by each
-- run, a dedupe-friendly File table keyed by content hash, and normalized
-- lookup tables (LocalDrive, LocalDirectory, LocalFilename) that the caches
-- (FileCache, DirCache) and backup sets point into instead of repeating
-- strings.
--
-- See Documents\Xondra.dat.ERD.md for the entity-relationship diagram.
-- ============================================================================

PRAGMA foreign_keys = ON;

-- ----------------------------------------------------------------------------
-- Lookup tables (referenced by Backup / BackupSet / BackupSetEmptyDir / caches)
-- ----------------------------------------------------------------------------
CREATE TABLE LocalDrive (
    ID    INTEGER PRIMARY KEY AUTOINCREMENT,
    Drive STRING UNIQUE NOT NULL
);

CREATE TABLE LocalDirectory (
    ID        INTEGER PRIMARY KEY AUTOINCREMENT,
    Directory STRING UNIQUE NOT NULL
);

CREATE TABLE LocalFilename (
    ID       INTEGER PRIMARY KEY AUTOINCREMENT,
    Filename STRING UNIQUE NOT NULL
);

-- ----------------------------------------------------------------------------
-- File — deduplicated by content hash
-- ----------------------------------------------------------------------------
CREATE TABLE File (
    ID                 INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE,
    OriginalFileHash   CHAR (128) NOT NULL,
    OrigHMACSHA512     CHAR (128),
    Filesize           INTEGER,
    LocalVerified      BOOLEAN,
    BackupHash         CHAR (128),
    FilesizeCompressed INTEGER
);

CREATE INDEX ix_file_OriginalFileHash ON File (OriginalFileHash ASC);

-- ----------------------------------------------------------------------------
-- Backup — one row per backup run
-- ----------------------------------------------------------------------------
CREATE TABLE Backup (
    ID              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE,
    ComputerGUID    TEXT,
    StartDate       DATETIME,
    EndDate         DATETIME,
    FileCount       INTEGER DEFAULT (0),
    ErrorCount      INTEGER DEFAULT (0),
    LastDirID       INTEGER DEFAULT (0),
    LastDir         STRING,
    TargetFileCount INTEGER,
    ProcessingDate  DATETIME,
    Status          STRING,
    SettingsJSON    TEXT
);

-- ----------------------------------------------------------------------------
-- Error — errors raised during a Backup run
-- ----------------------------------------------------------------------------
CREATE TABLE Error (
    ID            INTEGER PRIMARY KEY AUTOINCREMENT UNIQUE NOT NULL,
    BackupID      INTEGER,
    ProcedureName STRING,
    Error         STRING
);

-- ----------------------------------------------------------------------------
-- BackupSet — files captured by a Backup run
-- ----------------------------------------------------------------------------
CREATE TABLE BackupSet (
    ID            INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE,
    BackupID      INTEGER NOT NULL,
    DirID         INTEGER NOT NULL REFERENCES LocalDirectory (ID),
    FileID        INTEGER REFERENCES File (ID) NOT NULL,
    FilenameID    INTEGER REFERENCES LocalFilename (ID) NOT NULL,
    DriveID       INTEGER NOT NULL REFERENCES LocalDrive (ID),
    Error         INTEGER,
    Attributes    INTEGER,
    CreationTime  TEXT,
    LastWriteTime TEXT
);

CREATE INDEX ix_backupset_all ON BackupSet (BackupID, DirID, FileID, FilenameID, DriveID);

-- ----------------------------------------------------------------------------
-- BackupSetEmptyDir — empty directories captured by a Backup run
-- ----------------------------------------------------------------------------
CREATE TABLE BackupSetEmptyDir (
    ID       INTEGER PRIMARY KEY AUTOINCREMENT
                     NOT NULL
                     UNIQUE,
    BackupID INTEGER NOT NULL,
    DirID    INTEGER NOT NULL
                     REFERENCES LocalDirectory (ID),
    DriveID  INTEGER NOT NULL
                     REFERENCES LocalDrive (ID),
    Error    INTEGER
);

-- ----------------------------------------------------------------------------
-- Caches
-- ----------------------------------------------------------------------------
CREATE TABLE FileCache (
    ID                  INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    ComputerGUID        STRING NOT NULL,
    Drive               STRING NOT NULL,
    Directory           STRING NOT NULL,
    Filename            STRING,
    LastModifiedDateUTC DATETIME,
    ArchiveBit          BOOLEAN,
    FileSize            INTEGER,
    LocalDriveID        INTEGER,
    LocalDirectoryID    INTEGER,
    LocalFilenameID     INTEGER,
    Backup              INTEGER DEFAULT (1),
    Attributes          INTEGER,
    CreationTime        TEXT,
    LastWriteTime       TEXT
);

CREATE TABLE DirCache (
    ID               INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    ComputerGUID     STRING NOT NULL,
    Drive            STRING NOT NULL,
    Directory        STRING NOT NULL,
    LocalDriveID     INTEGER,
    LocalDirectoryID INTEGER
);

-- ----------------------------------------------------------------------------
-- FileList — standalone/legacy table, no relationships to other tables
-- ----------------------------------------------------------------------------
CREATE TABLE FileList (
    ID        INTEGER PRIMARY KEY AUTOINCREMENT UNIQUE NOT NULL,
    Directory STRING,
    Filename  STRING
);
