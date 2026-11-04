ALTER TABLE MatchSessions ADD COLUMN ChampionId TEXT NOT NULL DEFAULT '';
UPDATE MatchSessions SET Champion=COALESCE((
    SELECT Champion FROM MatchSamples WHERE SessionId=MatchSessions.SessionId ORDER BY GameTime DESC,Id DESC LIMIT 1
), '') WHERE Champion='';
UPDATE MatchSessions SET Champion=COALESCE((
    SELECT Champion FROM MatchSamples WHERE SessionId=MatchSessions.SessionId ORDER BY GameTime DESC,Id DESC LIMIT 1
), '') WHERE Champion='';
