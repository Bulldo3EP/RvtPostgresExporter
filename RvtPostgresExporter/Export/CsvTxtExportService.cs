using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using RvtPostgresExporter.Revit;

namespace RvtPostgresExporter.Export
{
    public sealed class CsvTxtExportService
    {
        // новый разделитель
        private const string Sep = ";";

        public sealed class ParamField
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string Method { get; set; }
            public string Normalize { get; set; }
        }

        public List<ParamField> LoadProfileOrThrow(string jsonPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath))
                throw new ArgumentException("Путь к parameters.json пустой.", nameof(jsonPath));

            if (!File.Exists(jsonPath))
                throw new FileNotFoundException("Файл parameters.json не найден: " + jsonPath, jsonPath);

            var text = File.ReadAllText(jsonPath);
            var list = JsonConvert.DeserializeObject<List<ParamField>>(text);

            if (list == null || list.Count == 0)
                throw new InvalidOperationException("parameters.json пустой.");

            return list;
        }

        public void Export(string outPath, List<ParamField> fields, List<RevitExportCollector.ExportRow> rows)
        {
            if (string.IsNullOrWhiteSpace(outPath))
                throw new ArgumentException("Путь для экспорта пустой.", nameof(outPath));

            if (fields == null || fields.Count == 0)
                throw new ArgumentException("fields пустой.", nameof(fields));

            if (rows == null)
                rows = new List<RevitExportCollector.ExportRow>();

            EnsureDir(outPath);

            var utf8NoBom = new UTF8Encoding(false);

            using (var writer = new StreamWriter(outPath, false, utf8NoBom))
            {
                writer.WriteLine(string.Join(Sep, BuildHeader(fields)));

                foreach (var r in rows)
                {
                    var values = new List<string>();

                    foreach (var f in fields)
                    {
                        values.Add(GetValue(f, r));
                    }

                    writer.WriteLine(string.Join(Sep, values));
                }
            }
        }

        private static IEnumerable<string> BuildHeader(List<ParamField> fields)
        {
            foreach (var f in fields)
            {
                yield return f?.Name ?? "";
            }
        }

        private static string GetValue(ParamField field, RevitExportCollector.ExportRow row)
        {
            if (field == null) return "";

            string method = (field.Method ?? "").Trim();
            bool isParam = method == "Param";

            string raw;

            switch (method)
            {
                case "FilesName":
                case "FileName":
                case "SourceFile":
                    raw = row?.SourceFile ?? "";
                    break;

                case "RecordType":
                    raw = row?.RecordType ?? "";
                    break;

                case "Id":
                case "ReportingId":
                    raw = row == null ? "" : row.ReportingId.ToString(CultureInfo.InvariantCulture);
                    break;

                case "BuiltInCategory":
                case "Category":
                    raw = row?.CategoryName ?? "";
                    break;

                case "BuiltInFamilyName":
                case "Family":
                    raw = row?.FamilyName ?? "";
                    break;

                case "BuiltInTypeName":
                case "Type":
                    raw = row?.TypeName ?? "";
                    break;

                case "Param":

                    if (row?.Doc == null || row.Element == null || string.IsNullOrWhiteSpace(field.Name))
                        raw = "";
                    else
                        raw = RevitParameterReader.GetUserParameterValue(row.Doc, row.Element, field.Name) ?? "";

                    break;

                default:
                    raw = "";
                    break;
            }

            if (!string.IsNullOrWhiteSpace(field.Normalize) &&
                field.Normalize.Equals("Number", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeNumber(raw);
            }

            raw = NormalizeText(raw);

            if (isParam && string.IsNullOrEmpty(raw))
                return "None";

            return raw;
        }

        private static string NormalizeText(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
        }

        private static string NormalizeNumber(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";

            var m = Regex.Match(s, @"-?\d[\d\s]*([.,]\d+)?");
            if (!m.Success) return "";

            var raw = m.Value.Replace(" ", "").Trim();
            raw = raw.Replace(",", ".");

            double d;
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                return "";

            return d.ToString(CultureInfo.InvariantCulture);
        }

        private static void EnsureDir(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
        }
    }
}