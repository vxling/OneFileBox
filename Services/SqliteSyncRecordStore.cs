using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using OneFileBox.Models;

namespace OneFileBox.Services;

public class SqliteSyncRecordStore
{
    private readonly string _dbPath;
    private const int MaxAgeDays = 10;
    private const int MaxRecordsPerRepo = 10_000;

    public SqliteSyncRecordStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = System.IO.Path.Combine(appData, "OneFileBox");
        System.IO.Directory.CreateDirectory(dir);
        _dbPath = System.IO.Path.Combine(dir, "sync_records.db");
        InitSchema();
    }

    private void InitSchema()
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS SyncRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RepoName TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                Operation TEXT NOT NULL,
                Result TEXT NOT NULL,
                Message TEXT DEFAULT '',
                Timestamp TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_SyncRecords_RepoName ON SyncRecords(RepoName);
            CREATE INDEX IF NOT EXISTS IX_SyncRecords_Timestamp ON SyncRecords(Timestamp)";
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection GetConnection() => new($"Data Source={_dbPath}");

    public void AddRecord(string repoName, DateTime timestamp, string filePath, string operation, string result, string message)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO SyncRecords (RepoName, FilePath, Operation, Result, Message, Timestamp)
            VALUES (@repoName, @filePath, @operation, @result, @message, @timestamp)";
        cmd.Parameters.AddWithValue("@repoName", repoName);
        cmd.Parameters.AddWithValue("@filePath", filePath);
        cmd.Parameters.AddWithValue("@operation", operation);
        cmd.Parameters.AddWithValue("@result", result);
        cmd.Parameters.AddWithValue("@message", message);
        cmd.Parameters.AddWithValue("@timestamp", timestamp.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public List<SyncRecord> GetRecords(string repoName, int limit = 500)
    {
        var records = new List<SyncRecord>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, RepoName, FilePath, Operation, Result, Message, Timestamp
            FROM SyncRecords
            WHERE RepoName = @repoName
            ORDER BY Timestamp DESC
            LIMIT @limit";
        cmd.Parameters.AddWithValue("@repoName", repoName);
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            records.Add(new SyncRecord
            {
                Id = reader.GetInt64(0),
                RepoName = reader.GetString(1),
                FilePath = reader.GetString(2),
                Operation = reader.GetString(3),
                Result = reader.GetString(4),
                Message = reader.IsDBNull(5) ? "" : reader.GetString(5),
                Timestamp = DateTime.Parse(reader.GetString(6))
            });
        }
        return records;
    }

    public void DeleteRepo(string repoName)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM SyncRecords WHERE RepoName = @repoName";
        cmd.Parameters.AddWithValue("@repoName", repoName);
        cmd.ExecuteNonQuery();
    }

    public void CleanupAll()
    {
        using var conn = GetConnection();
        var cutoff = DateTime.Now.AddDays(-MaxAgeDays).ToString("O");

        // Delete old records
        using var cmd1 = conn.CreateCommand();
        cmd1.CommandText = "DELETE FROM SyncRecords WHERE Timestamp < @cutoff";
        cmd1.Parameters.AddWithValue("@cutoff", cutoff);
        cmd1.ExecuteNonQuery();

        // Per-repo cap
        using var cmdSel = conn.CreateCommand();
        cmdSel.CommandText = "SELECT DISTINCT RepoName FROM SyncRecords";
        var repoNames = new List<string>();
        using (var r = cmdSel.ExecuteReader())
            while (r.Read()) repoNames.Add(r.GetString(0));

        foreach (var repo in repoNames)
        {
            using var cmd2 = conn.CreateCommand();
            cmd2.CommandText = @"
                DELETE FROM SyncRecords
                WHERE RepoName = @repoName AND Id NOT IN (
                    SELECT Id FROM SyncRecords WHERE RepoName = @repoName
                    ORDER BY Timestamp DESC LIMIT @limit
                )";
            cmd2.Parameters.AddWithValue("@repoName", repo);
            cmd2.Parameters.AddWithValue("@limit", MaxRecordsPerRepo);
            cmd2.ExecuteNonQuery();
        }
    }
}