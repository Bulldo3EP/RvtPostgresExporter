using System;
using System.Globalization;

namespace RvtPostgresExporter.Normalization
{
    public static class ValueNormalizer
    {
        // Возвращаем строку для TXT (безопасно и предсказуемо)
        public static string ToTxtString(object value, string dataType)
        {
            if (value == null) return "";

            var t = (dataType ?? "text").Trim().ToLowerInvariant();

            try
            {
                switch (t)
                {
                    case "integer":
                        if (value is int i) return i.ToString(CultureInfo.InvariantCulture);
                        if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var ii))
                            return ii.ToString(CultureInfo.InvariantCulture);
                        return "";

                    case "double":
                        if (value is double d) return d.ToString(CultureInfo.InvariantCulture);
                        if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var dd))
                            return dd.ToString(CultureInfo.InvariantCulture);
                        return "";

                    case "boolean":
                        if (value is bool b) return b ? "true" : "false";
                        var s = Convert.ToString(value);
                        if (string.Equals(s, "Да", StringComparison.OrdinalIgnoreCase)) return "true";
                        if (string.Equals(s, "Нет", StringComparison.OrdinalIgnoreCase)) return "false";
                        if (string.Equals(s, "1")) return "true";
                        if (string.Equals(s, "0")) return "false";
                        if (bool.TryParse(s, out var bb)) return bb ? "true" : "false";
                        return "";

                    case "timestamp":
                        if (value is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                        if (DateTime.TryParse(Convert.ToString(value), out var dtt))
                            return dtt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                        return "";

                    default:
                        return SanitizeText(Convert.ToString(value));
                }
            }
            catch
            {
                return "";
            }
        }

        public static string SanitizeText(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
        }
    }
}