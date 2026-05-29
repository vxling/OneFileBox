#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OneFileBox.Models;
using OneFileBox.Services;

namespace OneFileBox.Views;

public partial class ConflictWindow : Window
{
    public ObservableCollection<ConflictedFileInfo> ConflictFiles { get; set; } = new();

    public ConflictWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public void SetConflicts(IEnumerable<ConflictedFileInfo> conflicts)
    {
        ConflictFiles.Clear();
        foreach (var c in conflicts)
            ConflictFiles.Add(c);
    }

    private void KeepAllLocal_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var c in ConflictFiles)
            c.SelectedResolution = ConflictResolution.KeepLocal;
        RefreshList();
    }

    private void AcceptAllServer_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var c in ConflictFiles)
        {
            c.SelectedResolution = c.IsTreeConflict
                ? ConflictResolution.KeepLocal
                : ConflictResolution.AcceptServer;
        }
        RefreshList();
    }

    private void RefreshList()
    {
        var list = ConflictFiles.ToList();
        ConflictFiles = new ObservableCollection<ConflictedFileInfo>(list);
        DataContext = null;
        DataContext = this;
    }

    private async void OK_Click(object? sender, RoutedEventArgs e)
    {
        await ApplyResolutionsAsync();
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        SvnCliLog.Information("[ConflictWindow] User deferred — conflicts left unresolved");
        Close();
    }

    public bool WasApplied => _applied;
    private bool _applied;

    private async Task ApplyResolutionsAsync()
    {
        var svnSvc = new SvnCliService();
        int handled = 0;

        foreach (var info in ConflictFiles)
        {
            try
            {
                var parentDir = Path.GetDirectoryName(info.FilePath) ?? "";
                var fileName = Path.GetFileName(info.FilePath);
                var accept = SvnAccept.Working;

                switch (info.SelectedResolution)
                {
                    case ConflictResolution.KeepLocal:
                        accept = info.IsTreeConflict ? SvnAccept.Working : SvnAccept.MineFull;
                        await svnSvc.ResolveAsync(info.FilePath, accept);
                        await svnSvc.CommitAsync(parentDir, $"Auto-sync: [Conflict Resolved — Kept Local] {fileName}");
                        SvnCliLog.Information("Conflict KeepLocal resolved: {File}", info.FilePath);
                        handled++;
                        break;

                    case ConflictResolution.AcceptServer:
                        if (info.IsTreeConflict)
                        {
                            // Tree conflicts can only be resolved with Working (keep local)
                            await svnSvc.ResolveAsync(info.FilePath, SvnAccept.Working);
                            SvnCliLog.Warning("Tree conflict forced to KeepLocal: {File}", info.FilePath);
                        }
                        else
                        {
                            await svnSvc.ResolveAsync(info.FilePath, SvnAccept.TheirsFull);
                            SvnCliLog.Information("Conflict AcceptServer resolved: {File}", info.FilePath);
                        }
                        handled++;
                        break;

                    case ConflictResolution.KeepBoth:
                        var backupPath = info.FilePath + $".local-backup-{DateTime.UtcNow:yyyyMMddHHmmss}";
                        File.Copy(info.FilePath, backupPath, overwrite: true);
                        SvnCliLog.Information("KeepBoth: copied {Original} → {Backup}", info.FilePath, backupPath);
                        accept = info.IsTreeConflict ? SvnAccept.Working : SvnAccept.TheirsFull;
                        await svnSvc.ResolveAsync(info.FilePath, accept);
                        handled++;
                        break;
                }
            }
            catch (Exception ex)
            {
                SvnCliLog.Error(ex, "Failed to resolve conflict for {File}", info.FilePath);
            }
        }

        SvnCliLog.Information("[ConflictWindow] Applied {Count} resolutions", handled);
        _applied = true;
    }
}