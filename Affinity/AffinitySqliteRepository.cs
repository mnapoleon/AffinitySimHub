using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;

namespace Affinity
{
    public sealed class AffinityContextTotals
    {
        public double TotalDistanceMeters { get; set; }

        public double TotalTimeDrivenSeconds { get; set; }
    }

    public sealed class AffinitySqliteRepository : IDisposable
    {
        private const string CreateSchemaSql = @"
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS games (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    normalized_name TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS cars (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    game_id INTEGER NOT NULL,
    model_name TEXT NOT NULL,
    normalized_model_name TEXT NOT NULL,
    display_name TEXT NULL,
    UNIQUE(game_id, normalized_model_name),
    FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS tracks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    game_id INTEGER NOT NULL,
    raw_track_name TEXT NOT NULL,
    track_name_with_config TEXT NOT NULL,
    normalized_track_name_with_config TEXT NOT NULL,
    display_name TEXT NULL,
    UNIQUE(game_id, normalized_track_name_with_config),
    FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS track_contexts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    game_id INTEGER NOT NULL,
    car_id INTEGER NOT NULL,
    track_id INTEGER NOT NULL,
    UNIQUE(game_id, car_id, track_id),
    FOREIGN KEY(game_id) REFERENCES games(id) ON DELETE CASCADE,
    FOREIGN KEY(car_id) REFERENCES cars(id) ON DELETE CASCADE,
    FOREIGN KEY(track_id) REFERENCES tracks(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_uid TEXT NOT NULL UNIQUE,
    track_context_id INTEGER NOT NULL,
    started_utc TEXT NOT NULL,
    ended_utc TEXT NOT NULL,
    session_date_utc TEXT NOT NULL,
    distance_meters REAL NOT NULL,
    time_driven_seconds REAL NOT NULL,
    created_utc TEXT NOT NULL,
    last_updated_utc TEXT NOT NULL,
    FOREIGN KEY(track_context_id) REFERENCES track_contexts(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_cars_game_model
ON cars (game_id, normalized_model_name);

CREATE INDEX IF NOT EXISTS ix_tracks_game_track_config
ON tracks (game_id, normalized_track_name_with_config);

CREATE INDEX IF NOT EXISTS ix_track_contexts_lookup
ON track_contexts (game_id, car_id, track_id);

CREATE INDEX IF NOT EXISTS ix_sessions_context_date
ON sessions (track_context_id, session_date_utc DESC);

CREATE INDEX IF NOT EXISTS ix_sessions_date
ON sessions (session_date_utc DESC);

CREATE INDEX IF NOT EXISTS ix_sessions_started
ON sessions (started_utc DESC);
";

        private readonly string _databasePath;
        private SQLiteConnection _connection;

        public AffinitySqliteRepository(string databasePath)
        {
            _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        }

        public void Initialize()
        {
            string directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
            _connection.Open();

            using (var command = _connection.CreateCommand())
            {
                command.CommandText = CreateSchemaSql;
                command.ExecuteNonQuery();
            }

            EnsureCompatibleSchema();
        }

        public bool HasSessionData()
        {
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = "SELECT EXISTS(SELECT 1 FROM sessions LIMIT 1);";
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
            }
        }

        public void ImportLegacyDatabase(AffinityDatabase database, DateTime migrationDateUtc)
        {
            if (database?.Games == null)
            {
                return;
            }

            DateTime importDateUtc = ToUtc(migrationDateUtc).Date;
            int importedSessionIndex = 0;

            using (var transaction = _connection.BeginTransaction())
            {
                foreach (KeyValuePair<string, GameBucket> gameEntry in database.Games)
                {
                    if (gameEntry.Value?.Cars == null)
                    {
                        continue;
                    }

                    long gameId = GetOrCreateGameId(gameEntry.Key, transaction);

                    foreach (KeyValuePair<string, CarBucket> carEntry in gameEntry.Value.Cars)
                    {
                        if (carEntry.Value?.Tracks == null)
                        {
                            continue;
                        }

                            long carId = GetOrCreateCarId(gameId, carEntry.Key, null, transaction);

                        foreach (KeyValuePair<string, TrackBucket> trackEntry in carEntry.Value.Tracks)
                        {
                            TrackBucket track = trackEntry.Value;
                            if (track == null)
                            {
                                continue;
                            }

                            string trackName = string.IsNullOrWhiteSpace(track.TrackName) ? trackEntry.Key : track.TrackName;
                            string trackNameWithConfig = string.IsNullOrWhiteSpace(track.TrackNameWithConfig) ? trackEntry.Key : track.TrackNameWithConfig;
                            long trackId = GetOrCreateTrackId(gameId, trackName, trackNameWithConfig, null, transaction);
                            long trackContextId = GetOrCreateTrackContextId(gameId, carId, trackId, transaction);
                            DateTime startedUtc = importDateUtc;
                            DateTime endedUtc = importDateUtc.AddSeconds(Math.Max(0.0, track.UsedTime));

                            UpsertSession(
                                $"legacy-{importDateUtc:yyyyMMdd}-{importedSessionIndex++}",
                                trackContextId,
                                startedUtc,
                                endedUtc,
                                importDateUtc,
                                Math.Max(0.0, track.TotalDistanceMeters),
                                Math.Max(0.0, track.UsedTime),
                                track.CreatedUtc == default ? startedUtc : ToUtc(track.CreatedUtc),
                                track.LastUpdatedUtc == default ? endedUtc : ToUtc(track.LastUpdatedUtc),
                                transaction);
                        }
                    }
                }

                transaction.Commit();
            }
        }

        public void UpsertSession(
            string sessionUid,
            string gameName,
            string carModel,
            string trackName,
            string trackNameWithConfig,
            DateTime startedUtc,
            DateTime endedUtc,
            double distanceMeters,
            double usedTimeSeconds)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                long gameId = GetOrCreateGameId(gameName, transaction);
                long carId = GetOrCreateCarId(gameId, carModel, null, transaction);
                long trackId = GetOrCreateTrackId(gameId, trackName, trackNameWithConfig, null, transaction);
                long trackContextId = GetOrCreateTrackContextId(gameId, carId, trackId, transaction);
                DateTime normalizedStartedUtc = ToUtc(startedUtc);
                DateTime normalizedEndedUtc = ToUtc(endedUtc);
                DateTime sessionDateUtc = normalizedStartedUtc.Date;
                DateTime nowUtc = DateTime.UtcNow;

                UpsertSession(
                    sessionUid,
                    trackContextId,
                    normalizedStartedUtc,
                    normalizedEndedUtc,
                    sessionDateUtc,
                    Math.Max(0.0, distanceMeters),
                    Math.Max(0.0, usedTimeSeconds),
                    nowUtc,
                    nowUtc,
                    transaction);

                transaction.Commit();
            }
        }

        public AffinityContextTotals GetContextTotals(string gameName, string carModel, string trackNameWithConfig)
        {
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    COALESCE(SUM(s.distance_meters), 0.0),
    COALESCE(SUM(s.time_driven_seconds), 0.0)
FROM sessions s
INNER JOIN track_contexts tc ON tc.id = s.track_context_id
INNER JOIN games g ON g.id = tc.game_id
INNER JOIN cars c ON c.id = tc.car_id
INNER JOIN tracks t ON t.id = tc.track_id
WHERE g.normalized_name = @gameName
  AND c.normalized_model_name = @carModel
  AND t.normalized_track_name_with_config = @trackNameWithConfig;";
                command.Parameters.AddWithValue("@gameName", AffinityGameName.Normalize(gameName));
                command.Parameters.AddWithValue("@carModel", NormalizeIdentityValue(carModel));
                command.Parameters.AddWithValue("@trackNameWithConfig", NormalizeIdentityValue(trackNameWithConfig));

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return new AffinityContextTotals();
                    }

                    return new AffinityContextTotals
                    {
                        TotalDistanceMeters = reader.IsDBNull(0) ? 0.0 : Convert.ToDouble(reader.GetValue(0), CultureInfo.InvariantCulture),
                        TotalTimeDrivenSeconds = reader.IsDBNull(1) ? 0.0 : Convert.ToDouble(reader.GetValue(1), CultureInfo.InvariantCulture)
                    };
                }
            }
        }

        public List<DistanceSummary> GetDistanceSummaries(DateTime? sessionDateStartUtc = null, DateTime? sessionDateEndUtc = null)
        {
            var summaries = new List<DistanceSummary>();

            using (var command = _connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    g.name,
    COALESCE(NULLIF(c.display_name, ''), c.model_name),
    t.raw_track_name,
    COALESCE(NULLIF(t.display_name, ''), t.track_name_with_config),
    COALESCE(SUM(s.distance_meters), 0.0),
    COALESCE(SUM(s.time_driven_seconds), 0.0),
    MAX(s.ended_utc)
FROM sessions s
INNER JOIN track_contexts tc ON tc.id = s.track_context_id
INNER JOIN games g ON g.id = tc.game_id
INNER JOIN cars c ON c.id = tc.car_id
INNER JOIN tracks t ON t.id = tc.track_id
WHERE (@sessionDateStartUtc IS NULL OR s.started_utc >= @sessionDateStartUtc)
  AND (@sessionDateEndUtc IS NULL OR s.started_utc < @sessionDateEndUtc)
GROUP BY g.name, COALESCE(NULLIF(c.display_name, ''), c.model_name), t.raw_track_name, COALESCE(NULLIF(t.display_name, ''), t.track_name_with_config);";
                command.Parameters.AddWithValue("@sessionDateStartUtc", sessionDateStartUtc.HasValue ? (object)ToIsoUtc(sessionDateStartUtc.Value) : DBNull.Value);
                command.Parameters.AddWithValue("@sessionDateEndUtc", sessionDateEndUtc.HasValue ? (object)ToIsoUtc(sessionDateEndUtc.Value) : DBNull.Value);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        double totalDistanceMeters = reader.IsDBNull(4) ? 0.0 : Convert.ToDouble(reader.GetValue(4), CultureInfo.InvariantCulture);
                        summaries.Add(new DistanceSummary
                        {
                            GameName = reader.GetString(0),
                            CarModel = reader.GetString(1),
                            TrackName = reader.GetString(2),
                            TrackNameWithConfig = reader.GetString(3),
                            TotalDistanceKm = totalDistanceMeters / 1000.0,
                            TotalDistanceMiles = totalDistanceMeters / 1609.344,
                            UsedTime = reader.IsDBNull(5) ? 0.0 : Convert.ToDouble(reader.GetValue(5), CultureInfo.InvariantCulture),
                            LastUpdatedUtc = ReadUtcDateTime(reader, 6)
                        });
                    }
                }
            }

            return summaries;
        }

        public void Dispose()
        {
            _connection?.Dispose();
            _connection = null;
        }

        private void UpsertSession(
            string sessionUid,
            long trackContextId,
            DateTime startedUtc,
            DateTime endedUtc,
            DateTime sessionDateUtc,
            double distanceMeters,
            double timeDrivenSeconds,
            DateTime createdUtc,
            DateTime lastUpdatedUtc,
            SQLiteTransaction transaction)
        {
            using (var command = _connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO sessions (
    session_uid,
    track_context_id,
    started_utc,
    ended_utc,
    session_date_utc,
    distance_meters,
    time_driven_seconds,
    created_utc,
    last_updated_utc)
VALUES (
    @sessionUid,
    @trackContextId,
    @startedUtc,
    @endedUtc,
    @sessionDateUtc,
    @distanceMeters,
    @timeDrivenSeconds,
    @createdUtc,
    @lastUpdatedUtc)
ON CONFLICT(session_uid) DO UPDATE SET
    track_context_id = excluded.track_context_id,
    started_utc = excluded.started_utc,
    ended_utc = excluded.ended_utc,
    session_date_utc = excluded.session_date_utc,
    distance_meters = excluded.distance_meters,
    time_driven_seconds = excluded.time_driven_seconds,
    last_updated_utc = excluded.last_updated_utc;";
                command.Parameters.AddWithValue("@sessionUid", sessionUid);
                command.Parameters.AddWithValue("@trackContextId", trackContextId);
                command.Parameters.AddWithValue("@startedUtc", ToIsoUtc(startedUtc));
                command.Parameters.AddWithValue("@endedUtc", ToIsoUtc(endedUtc));
                command.Parameters.AddWithValue("@sessionDateUtc", ToIsoUtc(sessionDateUtc));
                command.Parameters.AddWithValue("@distanceMeters", distanceMeters);
                command.Parameters.AddWithValue("@timeDrivenSeconds", timeDrivenSeconds);
                command.Parameters.AddWithValue("@createdUtc", ToIsoUtc(createdUtc));
                command.Parameters.AddWithValue("@lastUpdatedUtc", ToIsoUtc(lastUpdatedUtc));
                command.ExecuteNonQuery();
            }
        }

        private long GetOrCreateGameId(string gameName, SQLiteTransaction transaction)
        {
            using (var command = _connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO games (name, normalized_name)
VALUES (@name, @normalizedName)
ON CONFLICT(normalized_name) DO UPDATE SET
    name = excluded.name
RETURNING id;";
                command.Parameters.AddWithValue("@name", string.IsNullOrWhiteSpace(gameName) ? "Unknown Game" : gameName.Trim());
                command.Parameters.AddWithValue("@normalizedName", AffinityGameName.Normalize(gameName));
                return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private long GetOrCreateCarId(long gameId, string carModel, string displayName, SQLiteTransaction transaction)
        {
            using (var command = _connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO cars (game_id, model_name, normalized_model_name, display_name)
VALUES (@gameId, @modelName, @normalizedModelName, @displayName)
ON CONFLICT(game_id, normalized_model_name) DO UPDATE SET
    model_name = excluded.model_name,
    display_name = COALESCE(excluded.display_name, cars.display_name)
RETURNING id;";
                command.Parameters.AddWithValue("@gameId", gameId);
                command.Parameters.AddWithValue("@modelName", string.IsNullOrWhiteSpace(carModel) ? "Unknown Car" : carModel.Trim());
                command.Parameters.AddWithValue("@normalizedModelName", NormalizeIdentityValue(carModel));
                command.Parameters.AddWithValue("@displayName", ToDbValue(displayName));
                return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private long GetOrCreateTrackId(long gameId, string trackName, string trackNameWithConfig, string displayName, SQLiteTransaction transaction)
        {
            using (var command = _connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO tracks (game_id, raw_track_name, track_name_with_config, normalized_track_name_with_config, display_name)
VALUES (@gameId, @rawTrackName, @trackNameWithConfig, @normalizedTrackNameWithConfig, @displayName)
ON CONFLICT(game_id, normalized_track_name_with_config) DO UPDATE SET
    raw_track_name = excluded.raw_track_name,
    track_name_with_config = excluded.track_name_with_config,
    display_name = COALESCE(excluded.display_name, tracks.display_name)
RETURNING id;";
                command.Parameters.AddWithValue("@gameId", gameId);
                command.Parameters.AddWithValue("@rawTrackName", string.IsNullOrWhiteSpace(trackName) ? "Unknown Track" : trackName.Trim());
                command.Parameters.AddWithValue("@trackNameWithConfig", string.IsNullOrWhiteSpace(trackNameWithConfig) ? "Unknown Track" : trackNameWithConfig.Trim());
                command.Parameters.AddWithValue("@normalizedTrackNameWithConfig", NormalizeIdentityValue(trackNameWithConfig));
                command.Parameters.AddWithValue("@displayName", ToDbValue(displayName));
                return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private void EnsureCompatibleSchema()
        {
            EnsureColumnExists("cars", "display_name", "TEXT NULL");
            EnsureColumnExists("tracks", "display_name", "TEXT NULL");

            if (!ColumnExists("sessions", "time_driven_seconds"))
            {
                if (ColumnExists("sessions", "used_time_seconds"))
                {
                    ExecuteNonQuery("ALTER TABLE sessions RENAME COLUMN used_time_seconds TO time_driven_seconds;");
                }
                else
                {
                    EnsureColumnExists("sessions", "time_driven_seconds", "REAL NOT NULL DEFAULT 0");
                }
            }
        }

        private void EnsureColumnExists(string tableName, string columnName, string columnDefinition)
        {
            if (ColumnExists(tableName, columnName))
            {
                return;
            }

            ExecuteNonQuery($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};");
        }

        private bool ColumnExists(string tableName, string columnName)
        {
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = $"PRAGMA table_info({tableName});";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void ExecuteNonQuery(string sql)
        {
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        private long GetOrCreateTrackContextId(long gameId, long carId, long trackId, SQLiteTransaction transaction)
        {
            using (var command = _connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO track_contexts (game_id, car_id, track_id)
VALUES (@gameId, @carId, @trackId)
ON CONFLICT(game_id, car_id, track_id) DO UPDATE SET
    game_id = excluded.game_id
RETURNING id;";
                command.Parameters.AddWithValue("@gameId", gameId);
                command.Parameters.AddWithValue("@carId", carId);
                command.Parameters.AddWithValue("@trackId", trackId);
                return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private static string NormalizeIdentityValue(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private static DateTime ReadUtcDateTime(SQLiteDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
            {
                return DateTime.MinValue;
            }

            return DateTime.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        }

        private static DateTime ToUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            return value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value.ToUniversalTime();
        }

        private static string ToIsoUtc(DateTime value)
        {
            return ToUtc(value).ToString("o", CultureInfo.InvariantCulture);
        }

        private static object ToDbValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }
    }
}
