-- ============================================================================
-- Xondra.cfg — Data
--
-- Populates empty tables (created via Xondra.cfg.DDL.sql) with the values
-- currently present in Xondra.cfg. Insert order respects foreign keys:
-- Job -> Attribute -> Value / MultiValue.
-- ============================================================================

BEGIN TRANSACTION;

-- ----------------------------------------------------------------------------
-- Job (3 rows)
--
-- NOTE: Job.Type for ID 0 is stored as 'All' here (source Xondra.cfg has
-- 'ALL'). Corrected so it matches the casing of the 3 Attribute.JobTypes
-- values that reference it ('LocalShareName', 'ComputerGUID', 'JobType'),
-- since the DDL enables PRAGMA foreign_keys = ON and SQLite string FK
-- comparisons are case-sensitive.
-- ----------------------------------------------------------------------------
INSERT INTO Job (ID, Type) VALUES (0, 'All');
INSERT INTO Job (ID, Type) VALUES (1, 'Backup');
INSERT INTO Job (ID, Type) VALUES (2, 'Restore');

-- ----------------------------------------------------------------------------
-- Attribute (15 rows)
-- ----------------------------------------------------------------------------
INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes, ReadOnly, AllowedValues) VALUES (1, 'TargetDirectory', 0, 'Backup', 0, NULL);
INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes, ReadOnly, AllowedValues) VALUES (2, 'ExcludedFile', 1, 'Backup', 0, NULL);
INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes, ReadOnly, AllowedValues) VALUES (3, 'ExcludedDirectory', 1, 'Backup', 0, NULL);
INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes, ReadOnly, AllowedValues) VALUES (4, 'SourceDirectory', 0, 'Backup', 0, NULL);
INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes, ReadOnly, AllowedValues) VALUES (5, 'LocalShareName', 0, 'All', 1, NULL);
INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes, ReadOnly, AllowedValues) VALUES (6, 'ComputerGUID', 0, 'All', 1, NULL);
INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes, ReadOnly, AllowedValues) VALUES (7, 'BackupType', 0, 'Backup', 0, 'HASH,ARCHIVEBIT');
INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes, ReadOnly, AllowedValues) VALUES (8, 'InMemoryBackupInterval', 0, 'Backup', 0, 'POSITIVE INTEGERS');
INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes, ReadOnly, AllowedValues) VALUES (9, 'UseVSS', 0, 'Backup', 0, 'TRUE,FALSE');
INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes, ReadOnly, AllowedValues) VALUES (10, 'InMemoryMode', 0, 'Backup', 0, 'TRUE,FALSE');
INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes, ReadOnly, AllowedValues) VALUES (11, 'IncludeEmptyDirectories', 0, 'Backup', 0, NULL);
INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes, ReadOnly, AllowedValues) VALUES (12, 'JobType', 0, 'All', 1, 'BACKUP');
INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes, ReadOnly, AllowedValues) VALUES (13, 'RetentionPeriod', 0, 'Backup', 0, 'POSITIVE INTEGERS');
INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes, ReadOnly, AllowedValues) VALUES (14, 'RetentionPeriodUnits', 0, 'Backup', 0, 'DAYS,WEEKS,MONTHS,YEARS');
INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes, ReadOnly, AllowedValues) VALUES (15, 'OnFileExists', 0, 'Restore', 0, 'SKIP,REPLACE');

-- ----------------------------------------------------------------------------
-- Value (12 rows)
-- ----------------------------------------------------------------------------
INSERT INTO Value (ID, AttributeID, Value, JobID) VALUES (4, 1, 'D:\XondraBackup', 1);
INSERT INTO Value (ID, AttributeID, Value, JobID) VALUES (5, 4, 'C:\Users\Daniel\Desktop\Test Directory', 1);
INSERT INTO Value (ID, AttributeID, Value, JobID) VALUES (6, 5, 'XondraBackupVssShare', 0);
INSERT INTO Value (ID, AttributeID, Value, JobID) VALUES (8, 6, '1ef11263-b499-40f1-9179-c57e88efdb2d', 0);
INSERT INTO Value (ID, AttributeID, Value, JobID) VALUES (11, 7, 'ARCHIVEBIT', 1);
INSERT INTO Value (ID, AttributeID, Value, JobID) VALUES (12, 8, 10000, 1);
INSERT INTO Value (ID, AttributeID, Value, JobID) VALUES (13, 9, 'false', 1);
INSERT INTO Value (ID, AttributeID, Value, JobID) VALUES (14, 10, 'true', 1);
INSERT INTO Value (ID, AttributeID, Value, JobID) VALUES (15, 11, 'true', 1);
INSERT INTO Value (ID, AttributeID, Value, JobID) VALUES (16, 12, 'Backup', 1);
INSERT INTO Value (ID, AttributeID, Value, JobID) VALUES (17, 14, 'Years', 1);
INSERT INTO Value (ID, AttributeID, Value, JobID) VALUES (18, 13, 7, 1);

-- ----------------------------------------------------------------------------
-- MultiValue (6 rows)
-- ----------------------------------------------------------------------------
INSERT INTO MultiValue (ID, AttributeID, Value, JobID) VALUES (1, 2, 'swapfile.sys', 1);
INSERT INTO MultiValue (ID, AttributeID, Value, JobID) VALUES (2, 2, 'pagefile.sys', 1);
INSERT INTO MultiValue (ID, AttributeID, Value, JobID) VALUES (3, 2, 'hiberfil.sys', 1);
INSERT INTO MultiValue (ID, AttributeID, Value, JobID) VALUES (4, 3, '\$Recycle.Bin', 1);
INSERT INTO MultiValue (ID, AttributeID, Value, JobID) VALUES (5, 2, 'Xondra.cfg', 1);
INSERT INTO MultiValue (ID, AttributeID, Value, JobID) VALUES (6, 2, 'Xondra.dat', 1);

COMMIT;
