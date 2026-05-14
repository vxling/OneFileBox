using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OneFileBox.Converters;

/// <summary>
/// 将 int（选中的导航索引）与一个目标索引比较，返回 bool。
/// 用于 NavSelectedIndex 与具体页面索引比对，决定是否显示某个页面。
/// </summary>
public class IntToBoolConverter : IValueConverter
{
    public static readonly IntToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int selectedIndex && parameter is string targetStr && int.TryParse(targetStr, out int targetIndex))
        {
            return selectedIndex == targetIndex;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string targetStr && int.TryParse(targetStr, out int targetIndex))
        {
            return targetIndex;
        }
        return Avalonia.Data.BindingOperations.DoNothing;
    }
}