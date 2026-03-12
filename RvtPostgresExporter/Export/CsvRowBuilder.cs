using System.Collections.Generic;

namespace RvtPostgresExporter.Export
{
    internal static class CsvRowBuilder
    {
        public static string BuildSemicolonRow(IList<string> left, IList<string> right)
        {
            var all = new List<string>(left.Count + right.Count);
            all.AddRange(left);
            all.AddRange(right);

            for (int i = 0; i < all.Count; i++)
                all[i] = all[i] ?? "";

            return string.Join(";", all);
        }
    }
}