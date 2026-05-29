using System;

namespace OneFileBox.Models;

public class SyncRecord
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string RepoName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Operation { get; set; } = "";
    public string Result { get; set; } = "";
    public string Message { get; set; } = "";

    public string TimestampDisplay => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    public string RepoNameDisplay => RepoName;
    public string FilePathDisplay => System.IO.Path.GetFileName(FilePath) ?? FilePath;
    public string OperationDisplay => Operation;
    public string ResultDisplay => Result switch
    {
        "Success" => "成功",
        "Failed" => "失败",
        "Skipped" => "跳过",
        _ => Result
    };
}