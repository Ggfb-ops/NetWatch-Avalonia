using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media;

namespace NetWatch.ViewModels;

public class LevelToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Critical = new(Color.Parse("#ef4444"));
    private static readonly SolidColorBrush Error = new(Color.Parse("#f97316"));
    private static readonly SolidColorBrush Warning = new(Color.Parse("#eab308"));
    private static readonly SolidColorBrush Info = new(Color.Parse("#3b82f6"));
    private static readonly SolidColorBrush All = new(Color.Parse("#71717a"));
    private static readonly SolidColorBrush Default = new(Color.Parse("#64748b"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string level)
        {
            return level switch
            {
                "critical" => Critical,
                "error" => Error,
                "warning" => Warning,
                "info" => Info,
                "all" => All,
                _ => Default
            };
        }
        return Default;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}

public class NotNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value != null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}

public class BoolToCursorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true) return new Cursor(StandardCursorType.Hand);
        return new Cursor(StandardCursorType.Arrow);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}
