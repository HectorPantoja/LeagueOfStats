-- Compatible with the original prototype; existing rows are preserved.
CREATE TABLE IF NOT EXISTS Samples (
 Id INTEGER PRIMARY KEY, SessionId TEXT NOT NULL, RecordedUtc TEXT NOT NULL,
 GameTime REAL NOT NULL, Level INTEGER NOT NULL, Gold REAL NOT NULL);
CREATE INDEX IF NOT EXISTS IX_Samples_RecordedUtc ON Samples(RecordedUtc);
CREATE INDEX IF NOT EXISTS IX_Samples_SessionId ON Samples(SessionId);
-- Legacy cache is retained, but superseded by the typed localized catalogs.
CREATE TABLE IF NOT EXISTS Catalog (
 Region TEXT PRIMARY KEY, Version TEXT NOT NULL, UpdatedUtc TEXT NOT NULL,
 Champions TEXT NOT NULL, Items TEXT NOT NULL);
