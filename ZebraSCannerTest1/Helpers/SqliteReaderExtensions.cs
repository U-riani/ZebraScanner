using Microsoft.Data.Sqlite;

namespace ZebraSCannerTest1.Helpers
{
    public static class SqliteReaderExtensions
    {
        public static string SafeGetString(this SqliteDataReader reader, int index)
            => reader.IsDBNull(index) ? "" : reader.GetString(index);

        public static int SafeGetInt(this SqliteDataReader reader, int index)
        {
            try
            {
                if (reader.IsDBNull(index)) return 0;
                return Convert.ToInt32(reader.GetValue(index));
            }
            catch { return 0; }
        }

        public static DateTime SafeGetDate(this SqliteDataReader reader, int index)
        {
            try
            {
                if (reader.IsDBNull(index)) return DateTime.MinValue;
                return DateTime.Parse(reader.GetValue(index)?.ToString() ?? "");
            }
            catch { return DateTime.MinValue; }
        }
    }
}
