using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using MobusTCP.Models;
using MobusTCP.Services.Interfaces;

namespace MobusTCP.Services;

public class DatabaseService : IDatabaseService
{
    private readonly string _historianDir;
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    public string DatabasePath => _dbPath;
    public string HistorianDirectory => _historianDir;

    public DatabaseService(string? customDbPath = null)
    {
        _historianDir = ResolveHistorianDirectory();
        _dbPath = customDbPath ?? Path.Combine(_historianDir, "scada_robot.db");
        _connectionString = $"Data Source={_dbPath};";
    }

    public static string ResolveHistorianDirectory()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string currentDir = Directory.GetCurrentDirectory();

        // 1. Check if current working directory has Historian or is project root
        string p1 = Path.Combine(currentDir, "Historian");
        if (Directory.Exists(p1) || File.Exists(Path.Combine(currentDir, "SCADA.csproj")) || File.Exists(Path.Combine(currentDir, "SCADA.sln")))
        {
            if (!Directory.Exists(p1)) Directory.CreateDirectory(p1);
            return p1;
        }

        // 2. Search upwards from base directory for solution/project root
        var dirInfo = new DirectoryInfo(baseDir);
        while (dirInfo != null)
        {
            if (File.Exists(Path.Combine(dirInfo.FullName, "SCADA.csproj")) || File.Exists(Path.Combine(dirInfo.FullName, "SCADA.sln")))
            {
                string p = Path.Combine(dirInfo.FullName, "Historian");
                if (!Directory.Exists(p)) Directory.CreateDirectory(p);
                return p;
            }
            dirInfo = dirInfo.Parent;
        }

        // 3. Fallback to base directory / Historian
        string fallback = Path.Combine(baseDir, "Historian");
        if (!Directory.Exists(fallback)) Directory.CreateDirectory(fallback);
        return fallback;
    }

    public async Task InitializeDatabaseAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            // Enable WAL mode for performance
            await using (var walCmd = conn.CreateCommand())
            {
                walCmd.CommandText = "PRAGMA journal_mode = WAL;";
                await walCmd.ExecuteNonQueryAsync();
            }

            // 1. Table TelemetryLog (Historian)
            string createTelemetrySql = @"
                CREATE TABLE IF NOT EXISTS TelemetryLog (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    Q1 REAL NOT NULL,
                    Q2 REAL NOT NULL,
                    Q3 REAL NOT NULL,
                    Q4 REAL NOT NULL,
                    X REAL NOT NULL,
                    Y REAL NOT NULL,
                    Z REAL NOT NULL,
                    Pitch REAL NOT NULL,
                    Tau1 REAL NOT NULL,
                    Tau2 REAL NOT NULL,
                    Tau3 REAL NOT NULL,
                    Tau4 REAL NOT NULL,
                    TotalPower REAL NOT NULL,
                    PlcStatus TEXT,
                    LatencyMs REAL NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IDX_Telemetry_Timestamp ON TelemetryLog(Timestamp);
            ";

            // 2. Table AlarmsAndEvents
            string createAlarmsSql = @"
                CREATE TABLE IF NOT EXISTS AlarmsAndEvents (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    EventType TEXT NOT NULL,
                    Source TEXT NOT NULL,
                    Message TEXT NOT NULL,
                    Acknowledged INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS IDX_Alarms_Timestamp ON AlarmsAndEvents(Timestamp);
            ";

            // 3. Table RecipePrograms
            string createRecipesSql = @"
                CREATE TABLE IF NOT EXISTS RecipePrograms (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Description TEXT,
                    CreatedAt TEXT NOT NULL,
                    ProfileType TEXT NOT NULL,
                    Duration REAL NOT NULL,
                    StartQ1 REAL NOT NULL,
                    StartQ2 REAL NOT NULL,
                    StartQ3 REAL NOT NULL,
                    StartQ4 REAL NOT NULL,
                    EndQ1 REAL NOT NULL,
                    EndQ2 REAL NOT NULL,
                    EndQ3 REAL NOT NULL,
                    EndQ4 REAL NOT NULL,
                    StartX REAL NOT NULL,
                    StartY REAL NOT NULL,
                    StartZ REAL NOT NULL,
                    StartPitch REAL NOT NULL,
                    EndX REAL NOT NULL,
                    EndY REAL NOT NULL,
                    EndZ REAL NOT NULL,
                    EndPitch REAL NOT NULL
                );
            ";

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = createTelemetrySql + createAlarmsSql + createRecipesSql;
                await cmd.ExecuteNonQueryAsync();
            }

            // Seed default recipes if empty
            await SeedDefaultRecipesIfEmptyAsync(conn);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private static async Task SeedDefaultRecipesIfEmptyAsync(SqliteConnection conn)
    {
        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM RecipePrograms;";
        long count = (long)(await countCmd.ExecuteScalarAsync() ?? 0L);

        if (count == 0)
        {
            await using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO RecipePrograms (Name, Description, CreatedAt, ProfileType, Duration,
                    StartQ1, StartQ2, StartQ3, StartQ4, EndQ1, EndQ2, EndQ3, EndQ4,
                    StartX, StartY, StartZ, StartPitch, EndX, EndY, EndZ, EndPitch)
                VALUES 
                ('Pick and Place A', 'Gắp vật từ khay A sang khay B', datetime('now'), 'QuinticPolynomial', 3.0,
                 0.0, 45.0, -45.0, 0.0, 90.0, 20.0, -20.0, 45.0,
                 250.0, 0.0, 200.0, 0.0, 150.0, 200.0, 100.0, -30.0),
                ('Homing Routine', 'Chuyển về trạng thái chờ mặc định', datetime('now'), 'TrapezoidalVelocity', 2.0,
                 90.0, 20.0, -20.0, 45.0, 0.0, 45.0, -45.0, 0.0,
                 150.0, 200.0, 100.0, -30.0, 250.0, 0.0, 200.0, 0.0);
            ";
            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    public async Task LogTelemetryAsync(TelemetryLogEntry entry)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO TelemetryLog (Timestamp, Q1, Q2, Q3, Q4, X, Y, Z, Pitch, Tau1, Tau2, Tau3, Tau4, TotalPower, PlcStatus, LatencyMs)
                VALUES (@Timestamp, @Q1, @Q2, @Q3, @Q4, @X, @Y, @Z, @Pitch, @Tau1, @Tau2, @Tau3, @Tau4, @TotalPower, @PlcStatus, @LatencyMs);
            ";

            cmd.Parameters.AddWithValue("@Timestamp", entry.Timestamp.ToString("o"));
            cmd.Parameters.AddWithValue("@Q1", entry.Q1);
            cmd.Parameters.AddWithValue("@Q2", entry.Q2);
            cmd.Parameters.AddWithValue("@Q3", entry.Q3);
            cmd.Parameters.AddWithValue("@Q4", entry.Q4);
            cmd.Parameters.AddWithValue("@X", entry.X);
            cmd.Parameters.AddWithValue("@Y", entry.Y);
            cmd.Parameters.AddWithValue("@Z", entry.Z);
            cmd.Parameters.AddWithValue("@Pitch", entry.Pitch);
            cmd.Parameters.AddWithValue("@Tau1", entry.Tau1);
            cmd.Parameters.AddWithValue("@Tau2", entry.Tau2);
            cmd.Parameters.AddWithValue("@Tau3", entry.Tau3);
            cmd.Parameters.AddWithValue("@Tau4", entry.Tau4);
            cmd.Parameters.AddWithValue("@TotalPower", entry.TotalPower);
            cmd.Parameters.AddWithValue("@PlcStatus", entry.PlcStatus ?? "READY");
            cmd.Parameters.AddWithValue("@LatencyMs", entry.LatencyMs);

            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task LogAlarmAsync(string eventType, string source, string message)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO AlarmsAndEvents (Timestamp, EventType, Source, Message, Acknowledged)
                VALUES (@Timestamp, @EventType, @Source, @Message, 0);
            ";

            cmd.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@EventType", eventType);
            cmd.Parameters.AddWithValue("@Source", source);
            cmd.Parameters.AddWithValue("@Message", message);

            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<List<TelemetryLogEntry>> GetTelemetryHistoryAsync(DateTime? start = null, DateTime? end = null, int limit = 500)
    {
        var list = new List<TelemetryLogEntry>();
        await _dbLock.WaitAsync();
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            string sql = "SELECT Id, Timestamp, Q1, Q2, Q3, Q4, X, Y, Z, Pitch, Tau1, Tau2, Tau3, Tau4, TotalPower, PlcStatus, LatencyMs FROM TelemetryLog ";
            var conditions = new List<string>();

            if (start.HasValue)
            {
                conditions.Add("Timestamp >= @Start");
                cmd.Parameters.AddWithValue("@Start", start.Value.ToString("o"));
            }
            if (end.HasValue)
            {
                conditions.Add("Timestamp <= @End");
                cmd.Parameters.AddWithValue("@End", end.Value.ToString("o"));
            }

            if (conditions.Count > 0)
            {
                sql += "WHERE " + string.Join(" AND ", conditions) + " ";
            }

            sql += "ORDER BY Timestamp DESC LIMIT @Limit;";
            cmd.Parameters.AddWithValue("@Limit", limit);
            cmd.CommandText = sql;

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TelemetryLogEntry
                {
                    Id = reader.GetInt64(0),
                    Timestamp = DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
                    Q1 = reader.GetDouble(2),
                    Q2 = reader.GetDouble(3),
                    Q3 = reader.GetDouble(4),
                    Q4 = reader.GetDouble(5),
                    X = reader.GetDouble(6),
                    Y = reader.GetDouble(7),
                    Z = reader.GetDouble(8),
                    Pitch = reader.GetDouble(9),
                    Tau1 = reader.GetDouble(10),
                    Tau2 = reader.GetDouble(11),
                    Tau3 = reader.GetDouble(12),
                    Tau4 = reader.GetDouble(13),
                    TotalPower = reader.GetDouble(14),
                    PlcStatus = reader.IsDBNull(15) ? "READY" : reader.GetString(15),
                    LatencyMs = reader.GetDouble(16)
                });
            }
        }
        finally
        {
            _dbLock.Release();
        }
        return list;
    }

    public async Task<List<AlarmEventEntry>> GetAlarmsHistoryAsync(int limit = 100)
    {
        var list = new List<AlarmEventEntry>();
        await _dbLock.WaitAsync();
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Timestamp, EventType, Source, Message, Acknowledged
                FROM AlarmsAndEvents
                ORDER BY Timestamp DESC
                LIMIT @Limit;
            ";
            cmd.Parameters.AddWithValue("@Limit", limit);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new AlarmEventEntry
                {
                    Id = reader.GetInt64(0),
                    Timestamp = DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
                    EventType = reader.GetString(2),
                    Source = reader.GetString(3),
                    Message = reader.GetString(4),
                    Acknowledged = reader.GetInt32(5) == 1
                });
            }
        }
        finally
        {
            _dbLock.Release();
        }
        return list;
    }

    public async Task AcknowledgeAlarmAsync(long alarmId)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE AlarmsAndEvents SET Acknowledged = 1 WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@Id", alarmId);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<List<RecipeProgram>> GetRecipesAsync()
    {
        var list = new List<RecipeProgram>();
        await _dbLock.WaitAsync();
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Name, Description, CreatedAt, ProfileType, Duration,
                       StartQ1, StartQ2, StartQ3, StartQ4, EndQ1, EndQ2, EndQ3, EndQ4,
                       StartX, StartY, StartZ, StartPitch, EndX, EndY, EndZ, EndPitch
                FROM RecipePrograms
                ORDER BY Id DESC;
            ";

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new RecipeProgram
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    CreatedAt = DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                    ProfileType = reader.GetString(4),
                    Duration = reader.GetDouble(5),
                    StartQ1 = reader.GetDouble(6),
                    StartQ2 = reader.GetDouble(7),
                    StartQ3 = reader.GetDouble(8),
                    StartQ4 = reader.GetDouble(9),
                    EndQ1 = reader.GetDouble(10),
                    EndQ2 = reader.GetDouble(11),
                    EndQ3 = reader.GetDouble(12),
                    EndQ4 = reader.GetDouble(13),
                    StartX = reader.GetDouble(14),
                    StartY = reader.GetDouble(15),
                    StartZ = reader.GetDouble(16),
                    StartPitch = reader.GetDouble(17),
                    EndX = reader.GetDouble(18),
                    EndY = reader.GetDouble(19),
                    EndZ = reader.GetDouble(20),
                    EndPitch = reader.GetDouble(21)
                });
            }
        }
        finally
        {
            _dbLock.Release();
        }
        return list;
    }

    public async Task<long> SaveRecipeAsync(RecipeProgram recipe)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            if (recipe.Id > 0)
            {
                cmd.CommandText = @"
                    UPDATE RecipePrograms SET 
                        Name = @Name, Description = @Description, ProfileType = @ProfileType, Duration = @Duration,
                        StartQ1 = @StartQ1, StartQ2 = @StartQ2, StartQ3 = @StartQ3, StartQ4 = @StartQ4,
                        EndQ1 = @EndQ1, EndQ2 = @EndQ2, EndQ3 = @EndQ3, EndQ4 = @EndQ4,
                        StartX = @StartX, StartY = @StartY, StartZ = @StartZ, StartPitch = @StartPitch,
                        EndX = @EndX, EndY = @EndY, EndZ = @EndZ, EndPitch = @EndPitch
                    WHERE Id = @Id;
                ";
                cmd.Parameters.AddWithValue("@Id", recipe.Id);
            }
            else
            {
                cmd.CommandText = @"
                    INSERT INTO RecipePrograms 
                        (Name, Description, CreatedAt, ProfileType, Duration,
                         StartQ1, StartQ2, StartQ3, StartQ4, EndQ1, EndQ2, EndQ3, EndQ4,
                         StartX, StartY, StartZ, StartPitch, EndX, EndY, EndZ, EndPitch)
                    VALUES
                        (@Name, @Description, @CreatedAt, @ProfileType, @Duration,
                         @StartQ1, @StartQ2, @StartQ3, @StartQ4, @EndQ1, @EndQ2, @EndQ3, @EndQ4,
                         @StartX, @StartY, @StartZ, @StartPitch, @EndX, @EndY, @EndZ, @EndPitch);
                    SELECT last_insert_rowid();
                ";
                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("o"));
            }

            cmd.Parameters.AddWithValue("@Name", recipe.Name);
            cmd.Parameters.AddWithValue("@Description", recipe.Description ?? "");
            cmd.Parameters.AddWithValue("@ProfileType", recipe.ProfileType);
            cmd.Parameters.AddWithValue("@Duration", recipe.Duration);
            cmd.Parameters.AddWithValue("@StartQ1", recipe.StartQ1);
            cmd.Parameters.AddWithValue("@StartQ2", recipe.StartQ2);
            cmd.Parameters.AddWithValue("@StartQ3", recipe.StartQ3);
            cmd.Parameters.AddWithValue("@StartQ4", recipe.StartQ4);
            cmd.Parameters.AddWithValue("@EndQ1", recipe.EndQ1);
            cmd.Parameters.AddWithValue("@EndQ2", recipe.EndQ2);
            cmd.Parameters.AddWithValue("@EndQ3", recipe.EndQ3);
            cmd.Parameters.AddWithValue("@EndQ4", recipe.EndQ4);
            cmd.Parameters.AddWithValue("@StartX", recipe.StartX);
            cmd.Parameters.AddWithValue("@StartY", recipe.StartY);
            cmd.Parameters.AddWithValue("@StartZ", recipe.StartZ);
            cmd.Parameters.AddWithValue("@StartPitch", recipe.StartPitch);
            cmd.Parameters.AddWithValue("@EndX", recipe.EndX);
            cmd.Parameters.AddWithValue("@EndY", recipe.EndY);
            cmd.Parameters.AddWithValue("@EndZ", recipe.EndZ);
            cmd.Parameters.AddWithValue("@EndPitch", recipe.EndPitch);

            if (recipe.Id > 0)
            {
                await cmd.ExecuteNonQueryAsync();
                return recipe.Id;
            }
            else
            {
                var result = await cmd.ExecuteScalarAsync();
                return (long)(result ?? 0L);
            }
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<bool> DeleteRecipeAsync(long recipeId)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM RecipePrograms WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@Id", recipeId);
            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<int> ClearTelemetryOlderThanAsync(int days)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var cutoff = DateTime.UtcNow.AddDays(-days).ToString("o");
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM TelemetryLog WHERE Timestamp < @Cutoff;";
            cmd.Parameters.AddWithValue("@Cutoff", cutoff);
            return await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<string> ExportTelemetryToCsvAsync(string? filePath = null, DateTime? start = null, DateTime? end = null)
    {
        string targetPath = filePath ?? Path.Combine(_historianDir, $"robot_telemetry_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        
        string? parentDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        var records = await GetTelemetryHistoryAsync(start, end, 10000);
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,Q1,Q2,Q3,Q4,X,Y,Z,Pitch,Tau1,Tau2,Tau3,Tau4,TotalPower,PlcStatus,LatencyMs");

        foreach (var r in records)
        {
            sb.AppendLine($"{r.FormattedTimestamp},{r.Q1:F2},{r.Q2:F2},{r.Q3:F2},{r.Q4:F2},{r.X:F2},{r.Y:F2},{r.Z:F2},{r.Pitch:F2},{r.Tau1:F2},{r.Tau2:F2},{r.Tau3:F2},{r.Tau4:F2},{r.TotalPower:F2},{r.PlcStatus},{r.LatencyMs:F1}");
        }

        await File.WriteAllTextAsync(targetPath, sb.ToString(), Encoding.UTF8);
        return targetPath;
    }

    public async Task<long> GetTelemetryCountAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM TelemetryLog;";
            return (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        }
        finally
        {
            _dbLock.Release();
        }
    }
}
