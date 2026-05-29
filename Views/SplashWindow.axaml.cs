#nullable enable
using System;
using Avalonia.Controls;
using Avalonia.Threading;

namespace OneFileBox.Views;

public partial class SplashWindow : Window
{
    private DispatcherTimer? _fakeProgressTimer;
    private int _step;
    private int _totalSteps = 3;

    public SplashWindow()
    {
        InitializeComponent();
        _fakeProgressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _fakeProgressTimer.Tick += (_, _) =>
        {
            if (ProgressBar.Value < 90)
                ProgressBar.Value += 15;
        };
    }

    public void SetStatus(string status)
    {
        StatusText.Text = status;
        _step++;
        StepText.Text = $"Step {_step} / {_totalSteps}";
        SplashLog.Debug("Status: {0}", status);
        ProgressBar.Value = Math.Min(ProgressBar.Value + 20, 95);
        Dispatcher.UIThread.Invoke(() => { }, DispatcherPriority.Render);
    }

    public void SetProgress(double percent, string status)
    {
        StatusText.Text = status;
        ProgressBar.Value = Math.Min(percent, 99);
        Dispatcher.UIThread.Invoke(() => { }, DispatcherPriority.Render);
    }

    public void ShowErrorAndClose(string message)
    {
        _fakeProgressTimer?.Stop();
        StatusText.Text = $"Startup failed: {message}";
        ProgressBar.Value = 0;
        SplashLog.Error(null, "Startup failed: {0}", message);
    }
}

internal static class SplashLog
{
    internal static void Debug(string msg, params object[] args) =>
        Console.WriteLine(args.Length > 0 ? $"[Splash] {msg}" : $"[Splash] {msg}");
    internal static void Error(Exception? ex, string msg, params object[] args) =>
        Console.WriteLine(ex != null ? $"[Splash] ERROR {msg}: {ex.Message}" : $"[Splash] ERROR {msg}");
}