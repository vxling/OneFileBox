using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using OneFileBox.Models;

namespace OneFileBox.Services;

public class SyncRecordService
{
    private static SyncRecordService? _instance;
    public static SyncRecordService Instance => _instance ??= new SyncRecordService();

    private readonly SqliteSyncRecordStore _store;
    public ObservableCollection<SyncRecord> Records { get; } = new();

    public SyncRecordService()
    {
        _store = new SqliteSyncRecordStore();
        Task.Run(() => _store.CleanupAll());
    }

    public void LoadRecordsForRepo(string repoName)
    {
        Records.Clear();
        foreach (var r in _store.GetRecords(repoName))
            Records.Add(r);
    }

    public void AddRecord(string repoName, string filePath, string operation, string result, string message = "")
    {
        var timestamp = DateTime.Now;
        var record = new SyncRecord
        {
            Timestamp = timestamp,
            RepoName = repoName,
            FilePath = filePath,
            Operation = operation,
            Result = result,
            Message = message
        };

        Records.Insert(0, record);
        if (Records.Count > 1000) Records.RemoveAt(Records.Count - 1);

        _store.AddRecord(repoName, timestamp, filePath, operation, result, message);
    }

    public void DeleteRepoRecords(string repoName) => _store.DeleteRepo(repoName);
}