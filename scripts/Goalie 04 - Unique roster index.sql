/*
  Goalie normalization - 04: unique (SessionId, UserId) on SessionRosters

  Phase 0 (2026-10-04) found zero duplicate (SessionId, UserId) rows, so the index is safe to add.
  Every roster writer already checks for an existing row (AddOrUpdatePlayerToRosterAsync,
  AddRosterPlayerAsync, TRG_Sessions_PopulateRoster); this turns a lost race into an error
  instead of a silent duplicate.

  Idempotent. Run in SSMS, review the verification output, then COMMIT (or ROLLBACK).

  Not needed (Phase 0 findings):
    Goalie 01 - the live CurrentSessionRoster view already maps Position 3 to 'Goalie'.
    Goalie 02 - TRG_BuySells_UpdateRoster does not exist; buyers reach the roster only through the
                Api, which now lands a Goalie-preference buyer as TBD (ToSkaterPosition).
    Goalie 03 - no Regulars rows have PositionPreference = 3, so TRG_Sessions_PopulateRoster seeds
                no goalies.
*/
SET XACT_ABORT ON;
BEGIN TRAN;

-- Must return no rows
SELECT SessionId, UserId, COUNT(*) AS c
FROM SessionRosters
GROUP BY SessionId, UserId
HAVING COUNT(*) > 1;

IF EXISTS (SELECT 1 FROM SessionRosters GROUP BY SessionId, UserId HAVING COUNT(*) > 1)
    THROW 50001, 'Duplicate (SessionId, UserId) rows exist in SessionRosters; resolve them before adding the unique index.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_SessionRosters_SessionId_UserId' AND object_id = OBJECT_ID(N'dbo.SessionRosters'))
    CREATE UNIQUE INDEX UX_SessionRosters_SessionId_UserId ON dbo.SessionRosters (SessionId, UserId);

-- Verification
SELECT i.name, i.is_unique, c.name AS ColumnName, ic.key_ordinal
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID(N'dbo.SessionRosters') AND i.name = N'UX_SessionRosters_SessionId_UserId'
ORDER BY ic.key_ordinal;

-- COMMIT;
-- ROLLBACK;
