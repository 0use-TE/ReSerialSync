using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Styling;
using TextMateSharp.Grammars;

namespace ReSerialSync.Converters;

public sealed class MarkdownThemeConverter : IValueConverter
{
    public static MarkdownThemeConverter Instance { get; } = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is ThemeVariant variant && variant == ThemeVariant.Light ? ThemeName.LightPlus : ThemeName.DarkPlus;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
