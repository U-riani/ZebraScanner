using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1.UI.Converters
{
    public class InventoryModeToHeaderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is InventoryMode mode)
                return mode == InventoryMode.Loots ? "Box" : "Section";
            return "Section";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
