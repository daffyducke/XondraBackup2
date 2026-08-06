-- ============================================================================
-- Xondra.cfg — DDL
--
-- Job/attribute configuration model: Job types, Attribute definitions that
-- apply to those job types, and the configured Value / MultiValue rows
-- (single-value vs. multi-value settings) tied to a specific job
-- (or JobID = 0 for "global"/default settings).
--
-- See Documents\Xondra.cfg.ERD.md for the entity-relationship diagram.
-- ============================================================================

PRAGMA foreign_keys = ON;

-- ----------------------------------------------------------------------------
-- Job
-- ----------------------------------------------------------------------------
CREATE TABLE Job (
    ID   INTEGER PRIMARY KEY UNIQUE NOT NULL,
    Type TEXT NOT NULL UNIQUE
);

-- ----------------------------------------------------------------------------
-- Attribute
-- ----------------------------------------------------------------------------
CREATE TABLE Attribute (
    ID             INTEGER PRIMARY KEY AUTOINCREMENT UNIQUE NOT NULL,
    Name           STRING UNIQUE NOT NULL,
    MultipleValues INTEGER,
    JobTypes       TEXT REFERENCES Job (Type),
    ReadOnly       INTEGER,
    AllowedValues  TEXT
);

-- ----------------------------------------------------------------------------
-- Value
-- ----------------------------------------------------------------------------
CREATE TABLE Value (
    ID          INTEGER PRIMARY KEY AUTOINCREMENT UNIQUE NOT NULL,
    AttributeID INTEGER REFERENCES Attribute (ID) NOT NULL,
    Value       STRING NOT NULL,
    JobID       INTEGER
);

CREATE INDEX ix_attributes_attributeid_value ON Value (AttributeID ASC, Value ASC);

-- ----------------------------------------------------------------------------
-- MultiValue
-- ----------------------------------------------------------------------------
CREATE TABLE MultiValue (
    ID          INTEGER PRIMARY KEY AUTOINCREMENT
                        UNIQUE
                        NOT NULL,
    AttributeID INTEGER REFERENCES Attribute (ID) NOT NULL,
    Value       STRING  NOT NULL,
    JobID       INTEGER
);

-- ----------------------------------------------------------------------------
-- Views
-- ----------------------------------------------------------------------------

-- Flattens Value and MultiValue into a single (JobID, Attribute, AttributeID,
-- Value, MultipleValues, ReadOnly) result set.
CREATE VIEW JobAttributes AS
SELECT v.JobID, a.Name AS Attribute, a.id AS AttributeID, v.Value, a.MultipleValues, a.ReadOnly
FROM value v
INNER JOIN Attribute a ON a.id = v.AttributeID
UNION ALL
SELECT mv.JobID, a.Name AS Attribute, a.id AS AttributeID, mv.Value, a.MultipleValues, a.ReadOnly
FROM Multivalue mv
INNER JOIN Attribute a ON a.id = mv.AttributeID;

-- Builds a per-JobID JSON object of {AttributeName: value}, merging
-- job-specific values, global/default values where a job doesn't override
-- them, and multi-value attributes as JSON arrays. Produces one
-- BackupSettings JSON blob per job.
CREATE VIEW Settings_Json AS
SELECT v.JobID, Replace(Group_concat(json_object(y.name, coalesce(y.value,json(json_value)))), '},{', ',') AS BackupSettings
FROM (Select Distinct JobID from Value where JobID <> 0) v
inner join  (SELECT v1.JobID, a1.name, v1.value, NULL json_value
    FROM Attribute a1
    INNER JOIN Value v1
        ON v1.AttributeID = a1.id
    WHERE v1.JobID <> 0

    Union all


    SELECT v5.JobID, a3.name, v3.value, NULL json_value
    FROM Attribute a3
    INNER JOIN Value v3
        ON v3.AttributeID = a3.id
    Cross join (Select Distinct JobID from Value where JobID <> 0) v5
    WHERE v3.JobID = 0


    UNION ALL

    SELECT x.JobID, x.name, NULL value, x.value as json_value
    FROM (SELECT mv2.JobID, a2.name, json_group_array(mv2.value) AS value
        FROM Attribute a2
        INNER JOIN MultiValue mv2
            ON mv2.AttributeID = a2.id
        where mv2.JobID <> 0
        GROUP BY mv2.JobID, a2.name
        ) x

    UNION ALL

    SELECT v6.JobID, z.name, NULL value, z.value as json_value
    FROM (SELECT mv4.JobID, a4.name, json_group_array(mv4.value) AS value
        FROM Attribute a4
        INNER JOIN MultiValue mv4
            ON mv4.AttributeID = a4.id
        where mv4.JobID = 0
        GROUP BY mv4.JobID, a4.name
        ) z
    Cross join (Select Distinct JobID from Value where JobID <> 0) v6
    ) y
    on y.JobID = v.JobID
where v.JobID <> 0;
