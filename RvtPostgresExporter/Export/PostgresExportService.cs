using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RvtPostgresExporter.Database;
using RvtPostgresExporter.Revit;

namespace RvtPostgresExporter.Export
{
    public sealed class PostgresExportService
    {
        public sealed class ParamField
        {
            public string Name { get; set; }
            public string Type { get; set; }       // TEXT | INTEGER | DOUBLE PRECISION
            public string Method { get; set; }     // FilesName | RecordType | Id | BuiltInCategory | BuiltInFamilyName | BuiltInTypeName | Param
            public string Normalize { get; set; }  // "Number" (optional)
        }

        public sealed class ExportOptions
        {
            public int BatchSize { get; set; } = 500;
            public string CreatedBy { get; set; }
            public string SourceModel { get; set; }
            public string RevitVersion { get; set; } = "2023";
            public string ProfileName { get; set; }
            public string ProfileHash { get; set; }
            public string Comment { get; set; }
        }

        public sealed class ExportProgress
        {
            public int TotalRows { get; set; }
            public int DoneRows { get; set; }
            public int BatchNumber { get; set; }
            public int BatchCount { get; set; }
            public Guid ExportVersionId { get; set; }
            public Guid ExportBatchId { get; set; }
            public int Affected { get; set; }
        }

        public static List<ParamField> LoadProfileOrThrow(string jsonPath)
        {
            var text = System.IO.File.ReadAllText(jsonPath);
            var list = JsonConvert.DeserializeObject<List<ParamField>>(text);

            if (list == null || list.Count == 0)
                throw new InvalidOperationException("parameters.json пустой.");

            return list;
        }

        public Guid ExportVersionedToExportsRawV2(
            string connectionString,
            List<ParamField> fields,
            List<RevitExportCollector.ExportRow> rows,
            ExportOptions options,
            IProgress<ExportProgress> progress)
        {
            if (!DbAppDomainHost.EnsureMvpObjectsV2Isolated(connectionString, out var ensureErr))
                throw new InvalidOperationException("EnsureMvpObjectsV2 failed: " + ensureErr);

            var exportVersionId = Guid.NewGuid();

            if (!DbAppDomainHost.CreateExportVersionV2Isolated(
                connectionString,
                exportVersionId,
                options.CreatedBy,
                options.SourceModel,
                options.RevitVersion,
                options.ProfileName,
                options.ProfileHash,
                options.Comment,
                out var verErr))
            {
                throw new InvalidOperationException("CreateExportVersionV2 failed: " + verErr);
            }

            if (rows == null) rows = new List<RevitExportCollector.ExportRow>();

            int total = rows.Count;
            int done = 0;
            int batchSize = Math.Max(1, options.BatchSize);
            int batchCount = (int)Math.Ceiling(total / (double)batchSize);
            int batchNumber = 0;

            foreach (var chunk in Chunk(rows, batchSize))
            {
                batchNumber++;
                var exportBatchId = Guid.NewGuid();

                var payload = new JArray();

                foreach (var r in chunk)
                {
                    var dataObj = new JObject();

                    foreach (var f in fields)
                    {
                        var raw = ResolveValue(f, r);           // object
                        var dbVal = NormalizeToDbValue(raw, f); // object or null

                        dataObj[f.Name] = dbVal == null
                            ? JValue.CreateNull()
                            : JToken.FromObject(dbVal);
                    }

                    payload.Add(new JObject
                    {
                        ["sourceFile"] = r.SourceFile ?? "",
                        ["recordType"] = r.RecordType ?? "",
                        ["reportingId"] = r.ReportingId,   // int -> JSON number
                        ["data"] = dataObj
                    });
                }

                string rowsJson = JsonConvert.SerializeObject(payload);

                int affected = DbAppDomainHost.UpsertExportsRawBatchV2Isolated(
                    connectionString,
                    exportVersionId,
                    exportBatchId,
                    rowsJson,
                    out var insertErr);

                if (!string.IsNullOrWhiteSpace(insertErr))
                    throw new InvalidOperationException(insertErr);

                done += chunk.Count;

                if (progress != null)
                {
                    progress.Report(new ExportProgress
                    {
                        TotalRows = total,
                        DoneRows = done,
                        BatchNumber = batchNumber,
                        BatchCount = batchCount,
                        ExportVersionId = exportVersionId,
                        ExportBatchId = exportBatchId,
                        Affected = affected
                    });
                }
            }

            return exportVersionId;
        }

        private static object ResolveValue(ParamField f, RevitExportCollector.ExportRow r)
        {
            var m = (f.Method ?? "").Trim();

            if (m == "FilesName") return r.SourceFile ?? "";
            if (m == "RecordType") return r.RecordType ?? "";
            if (m == "Id") return r.ReportingId; // int
            if (m == "BuiltInCategory") return r.CategoryName ?? "";
            if (m == "BuiltInFamilyName") return r.FamilyName ?? "";
            if (m == "BuiltInTypeName") return r.TypeName ?? "";

            if (m == "Param")
            {
                // string: "None" (нет параметра) / "" (есть, но пуст) / "0,85 м³"
                return RevitParameterReader.GetUserParameterValue(r.Doc, r.Element, f.Name);
            }

            return "";
        }

        private static object NormalizeToDbValue(object rawObj, ParamField field)
        {
            if (rawObj == null) return null;

            // "None" -> null
            var asString = rawObj as string;
            if (asString != null && string.Equals(asString, "None", StringComparison.OrdinalIgnoreCase))
                return null;

            var type = (field.Type ?? "TEXT").Trim().ToUpperInvariant();
            var normalize = (field.Normalize ?? "").Trim();

            // если уже число (напр. reportingId)
            if (type == "INTEGER" && rawObj is int) return rawObj;
            if ((type == "DOUBLE PRECISION" || type == "DOUBLE") && rawObj is double) return rawObj;

            var raw = Convert.ToString(rawObj, CultureInfo.CurrentCulture);
            if (raw == null) return null;

            raw = raw.Trim();
            if (raw.Length == 0) return null;

            if (type == "TEXT")
                return raw;

            // Normalize:Number -> extract number from "0,85 м³"
            if (string.Equals(normalize, "Number", StringComparison.OrdinalIgnoreCase))
            {
                if (type == "INTEGER")
                {
                    int i;
                    if (TryExtractInt(raw, out i)) return i;
                    return null;
                }

                if (type == "DOUBLE PRECISION" || type == "DOUBLE")
                {
                    double d;
                    if (TryExtractDouble(raw, out d)) return d;
                    return null;
                }
            }

            // fallback: try parse "as is"
            if (type == "INTEGER")
            {
                int i;
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) return i;
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out i)) return i;
                return null;
            }

            if (type == "DOUBLE PRECISION" || type == "DOUBLE")
            {
                double d;
                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out d)) return d;
                return null;
            }

            return raw;
        }

        private static bool TryExtractDouble(string input, out double value)
        {
            value = 0d;
            if (string.IsNullOrWhiteSpace(input)) return false;

            input = input.Replace('\u00A0', ' ');

            var m = Regex.Match(input, @"[-+]?\d[\d\s]*([.,]\d+)?");
            if (!m.Success) return false;

            var num = m.Value.Replace(" ", "").Replace(",", ".");
            return double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryExtractInt(string input, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;

            input = input.Replace('\u00A0', ' ');

            var m = Regex.Match(input, @"[-+]?\d[\d\s]*");
            if (!m.Success) return false;

            var num = m.Value.Replace(" ", "");
            return int.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static IEnumerable<List<T>> Chunk<T>(List<T> items, int size)
        {
            for (int i = 0; i < items.Count; i += size)
            {
                yield return items.GetRange(i, Math.Min(size, items.Count - i));
            }
        }
    }
}