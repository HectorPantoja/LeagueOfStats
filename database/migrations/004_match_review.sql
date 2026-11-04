-- Stores only the active player's review data and events exposed by the official Live Client API.
CREATE TABLE IF NOT EXISTS MatchSessions (
    SessionId TEXT PRIMARY KEY,
    StartedUtc TEXT NOT NULL,
    EndedUtc TEXT NULL,
    DurationSeconds REAL NOT NULL DEFAULT 0,
    EndReason TEXT NULL,
    ReviewJson TEXT NULL
);
CREATE TABLE IF NOT EXISTS MatchSamples (
    Id INTEGER PRIMARY KEY,
    SessionId TEXT NOT NULL REFERENCES MatchSessions(SessionId) ON DELETE CASCADE,
    RecordedUtc TEXT NOT NULL,
    GameTime REAL NOT NULL,
    Level INTEGER NOT NULL,
    Gold REAL NOT NULL,
    Kills INTEGER NOT NULL DEFAULT 0,
    Deaths INTEGER NOT NULL DEFAULT 0,
    Assists INTEGER NOT NULL DEFAULT 0,
    CreepScore INTEGER NOT NULL DEFAULT 0,
    Position TEXT NOT NULL DEFAULT '',
    Champion TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS IX_MatchSamples_SessionTime ON MatchSamples(SessionId, GameTime);
CREATE TABLE IF NOT EXISTS MatchEvents (
    SessionId TEXT NOT NULL REFERENCES MatchSessions(SessionId) ON DELETE CASCADE,
    EventId INTEGER NOT NULL,
    EventTime REAL NOT NULL,
    EventName TEXT NOT NULL,
    EventJson TEXT NOT NULL,
    PRIMARY KEY(SessionId, EventId)
);
CREATE INDEX IF NOT EXISTS IX_MatchEvents_SessionTime ON MatchEvents(SessionId, EventTime);
