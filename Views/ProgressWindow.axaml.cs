using System;
using Avalonia;
using Avalonia.Controls;

namespace OneFileBox.Views;

public partial class ProgressWindow : Window
{
    public event EventHandler? CancelRequested;

    public ProgressWindow()
    {
        InitializeComponent();
    }

    public void UpdateProgress(double progress, string? statusText = null, string? title = null)
    {
        ProgressBar.Value = progress;

        if (statusText != null)
        {
            StatusText.Text = statusText;
            StatusText.IsVisible = true;
        }

        if (title != null)
            Title = title;
    }

    public bool CanCancel
    {
        get => CancelButton.IsVisible;
        set => CancelButton.IsVisible = value;
    }

    public void SetTitle(string title) => Title = title;

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CancelButton.IsEnabled = false;
        CancelButton.Content = "正在取消...";
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}