using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OneFileBox.Models;
using OneFileBox.Services;

namespace OneFileBox.Views;

public partial class SyncRecordWindow : Window
{
    private readonly SyncRecordService _syncService;

    public SyncRecordWindow()
    {
        InitializeComponent();
        _syncService = SyncRecordService.Instance;
        RecordsGrid.ItemsSource = _syncService.Records;

        var count = _syncService.Records.Count;
        CountText.Text = $" ({count} 条)";
    }

    private void Clear_Click(object? sender, RoutedEventArgs e)
    {
        var repoName = _syncService.Records.FirstOrDefault()?.RepoName ?? "";
        if (!string.IsNullOrEmpty(repoName))
            _syncService.DeleteRepoRecords(repoName);
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}