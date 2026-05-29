using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OneFileBox.Converters;

public class SyncStatusToColorConverter : IValueConverter
{
    public static readonly SyncStatusToColorConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not OneFileBox.ViewModels.MainWindowViewModel.SyncStatusType status)
            return new SolidColorBrush(Colors.Gray);

        return status switch
        {
            OneFileBox.ViewModels.MainWindowViewModel.SyncStatusType.Idle => new SolidColorBrush(Colors.Gray),
            OneFileBox.ViewModels.MainWindowViewModel.SyncStatusType.Syncing => new SolidColorBrush(Colors.DodgerBlue),
            OneFileBox.ViewModels.MainWindowViewModel.SyncStatusType.Success => new SolidColorBrush(Colors.Green),
            OneFileBox.ViewModels.MainWindowViewModel.SyncStatusType.Failed => new SolidColorBrush(Colors.Red),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}