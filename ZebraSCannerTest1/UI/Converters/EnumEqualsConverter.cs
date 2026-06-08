using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1.UI.Converters;

public class EnumEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is InventoryMode mode && parameter is string target)
            return string.Equals(mode.ToString(), target, StringComparison.OrdinalIgnoreCase);
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
}
