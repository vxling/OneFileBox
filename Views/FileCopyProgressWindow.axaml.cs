using System;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OneFileBox.Services;

namespace OneFileBox.Views;

public partial class FileCopyProgressWindow : Window
{
    private FileCopier? _fileCopier;

    public FileCopyProgressWindow()
    {
        InitializeComponent();
    }


    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        _fileCopier?.Cancel();
        Close();
    }

    public void SetCopier(FileCopier copier)
    {
        _fileCopier = copier;
    }

    public void UpdateProgress(CopyProgress progress)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            CurrentFileText.Text = progress.CurrentFile;
            Avalonia.Controls.ToolTip.SetTip(CurrentFileText, progress.CurrentFile);
            ItemIndexText.Text = $"{progress.CurrentIndex} / {progress.TotalCount}";
            ProgressBar.Value = progress.ProgressPercent;
            BytesText.Text = progress.BytesDisplay;
        });
    }

    public void SetCompleted(string message)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            CurrentFileText.Text = message;
            ItemIndexText.Text = "";
            ProgressBar.Value = 100;
            CancelButton.Content = "关闭";
        });
    }

    public void SetError(string error)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            CurrentFileText.Text = "复制失败";
            ItemIndexText.Text = error;
            CancelButton.Content = "关闭";
        });
    }
}