using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OneFileBox.Views;

public enum MessageBoxIconType { Info, Warning, Error, Question, Success }
public enum MessageBoxButtonType { OK, YesNo, OKCancel, YesNoCancel }
public enum MessageBoxResult { None, OK, Yes, No, Cancel }

public partial class MessageBoxWindow : Window
{
    public string BoxTitle { get; set; } = "";
    public string Message { get; set; } = "";
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    public MessageBoxWindow()
    {
        DataContext = this;
        InitializeComponent();
    }

    public static MessageBoxResult Show(
        Window? owner,
        string message,
        string title,
        MessageBoxButtonType buttons = MessageBoxButtonType.OK,
        MessageBoxIconType icon = MessageBoxIconType.Info)
    {
        var msgbox = new MessageBoxWindow
        {
            BoxTitle = title,
            Message = message
        };

        msgbox.TitleText.Text = title;
        msgbox.MessageText.Text = message;
        msgbox.SetIcon(icon);
        msgbox.BuildButtons(buttons);

        if (owner != null)
        {
            msgbox.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            msgbox.ShowDialog(owner);
        }
        else
        {
            msgbox.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            msgbox.Show();
        }

        return msgbox.Result;
    }

    private void SetIcon(MessageBoxIconType icon)
    {
        var (symbol, colorHex) = icon switch
        {
            MessageBoxIconType.Info => ("ℹ", "#2196F3"),
            MessageBoxIconType.Warning => ("⚠", "#FFA000"),
            MessageBoxIconType.Error => ("✕", "#E53935"),
            MessageBoxIconType.Question => ("?", "#0072C9"),
            MessageBoxIconType.Success => ("✓", "#2E7D32"),
            _ => ("ℹ", "#2196F3")
        };

        IconText.Text = symbol;
        IconText.Foreground = Avalonia.Media.Brushes.White;
        try { IconText.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(colorHex)); }
        catch { }
    }

    private void BuildButtons(MessageBoxButtonType buttonType)
    {
        ButtonsPanel.Children.Clear();

        var btnDefs = buttonType switch
        {
            MessageBoxButtonType.OK => new[] { ("确定", MessageBoxResult.OK) },
            MessageBoxButtonType.YesNo => new[] { ("是", MessageBoxResult.Yes), ("否", MessageBoxResult.No) },
            MessageBoxButtonType.OKCancel => new[] { ("确定", MessageBoxResult.OK), ("取消", MessageBoxResult.Cancel) },
            MessageBoxButtonType.YesNoCancel => new[] { ("是", MessageBoxResult.Yes), ("否", MessageBoxResult.No), ("取消", MessageBoxResult.Cancel) },
            _ => new[] { ("确定", MessageBoxResult.OK) }
        };

        foreach (var (label, result) in btnDefs)
        {
            var btn = new Button
            {
                Content = label,
                MinWidth = 80,
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(6, 0, 0, 0),
                Tag = result
            };
            btn.Click += Button_Click;
            ButtonsPanel.Children.Add(btn);
        }
    }

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is MessageBoxResult r)
        {
            Result = r;
            Close();
        }
    }
}