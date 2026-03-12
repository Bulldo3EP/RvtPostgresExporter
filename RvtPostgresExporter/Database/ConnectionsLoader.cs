using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace RvtPostgresExporter.Database
{
    public sealed class ConnectionsLoader
    {
        public ConnectionsLoadResult Load(string filePath)
        {
            var res = new ConnectionsLoadResult();

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                res.IsValid = false;
                var msg = "connections.json не найден: " + (filePath ?? "(null)");
                res.Errors.Add(msg);
                AppLog.Error(msg);
                return res;
            }

            try
            {
                AppLog.Info("Чтение connections.json: " + filePath);

                var json = File.ReadAllText(filePath);
                var token = JToken.Parse(json);

                var schema = JSchema.Parse(ConnectionsSchema.Json);

                // ✅ FIX: явный тип для устранения неоднозначности перегрузок IsValid
                IList<ValidationError> errors;
                if (!token.IsValid(schema, out errors))
                {
                    res.IsValid = false;
                    res.Errors.Add("connections.json не прошёл валидацию схемы:");
                    AppLog.Error("connections.json schema validation failed.");

                    foreach (var e in errors)
                    {
                        var line = e.Message;
                        res.Errors.Add(line);
                        AppLog.Error("Schema error: " + line);
                    }

                    return res;
                }

                var cfg = JsonConvert.DeserializeObject<ConnectionsConfig>(json);
                if (cfg == null || cfg.Connections == null || cfg.Connections.Count == 0)
                {
                    res.IsValid = false;
                    var msg = "connections.json пустой или не содержит connections[].";
                    res.Errors.Add(msg);
                    AppLog.Error(msg);
                    return res;
                }

                res.IsValid = true;
                res.Config = cfg;

                AppLog.Info($"connections.json загружен: schemaVersion={cfg.SchemaVersion}, connections={cfg.Connections.Count}");
                return res;
            }
            catch (JsonReaderException jex)
            {
                res.IsValid = false;
                var msg = "Ошибка JSON (парсинг connections.json): " + jex.Message;
                res.Errors.Add(msg);
                AppLog.Error(msg, jex);
                return res;
            }
            catch (Exception ex)
            {
                res.IsValid = false;
                var msg = "Ошибка чтения connections.json: " + ex.Message;
                res.Errors.Add(msg);
                AppLog.Error(msg, ex);
                return res;
            }
        }
    }
}
