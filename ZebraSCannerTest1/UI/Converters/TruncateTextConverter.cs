using System.Globalization;

namespace ZebraSCannerTest1.UI.Converters
{
    public class TruncateTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "—";

            string text = value.ToString()?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(text))
                return "—";

            int maxLength = 10;

            if (parameter != null && int.TryParse(parameter.ToString(), out int parsed))
                maxLength = parsed;

            if (text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength) + "...";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}