using System;
using Npgsql;

namespace RvtPostgresExporter.Database
{
    /// <summary>
    /// В отдельном AppDomain (DbDomain) – чтобы не ловить конфликты сборок в Revit.
    /// </summary>
    public sealed class IsolatedDbRunner : MarshalByRefObject
    {
        public override object InitializeLifetimeService() => null;

        public bool CheckConnection(string connectionString, out string error)
        {
            error = null;

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand("SELECT 1", conn))
                    {
                        var x = cmd.ExecuteScalar();
                        return x != null && Convert.ToInt32(x) == 1;
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Создаёт таблицы v2 + индексы (idempotent).
        /// </summary>
        public bool EnsureMvpObjectsV2(string connectionString, out string error)
        {
            error = null;

            const string ddl = @"
CREATE TABLE IF NOT EXISTS public.export_versions_v2 (
    export_version_id uuid PRIMARY KEY,
    created_at        timestamptz NOT NULL DEFAULT now(),
    created_by        text NULL,
    source_model      text NULL,
    revit_version     text NULL,
    profile_name      text NULL,
    profile_hash      text NULL,
    comment           text NULL
);

CREATE TABLE IF NOT EXISTS public.exports_raw_v2 (
    id                bigserial PRIMARY KEY,
    export_version_id uuid       NOT NULL REFERENCES public.export_versions_v2(export_version_id),
    export_batch_id   uuid       NOT NULL,
    exported_at       timestamptz NOT NULL DEFAULT now(),
    source_file       text    NULL,
    record_type       text    NULL,
    reporting_id      integer NULL,
    data              jsonb   NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_exports_raw_v2_key
ON public.exports_raw_v2(export_version_id, record_type, reporting_id, source_file);

CREATE INDEX IF NOT EXISTS ix_exports_raw_v2_version
ON public.exports_raw_v2(export_version_id);

CREATE INDEX IF NOT EXISTS ix_exports_raw_v2_batch
ON public.exports_raw_v2(export_batch_id);

CREATE INDEX IF NOT EXISTS ix_exports_raw_v2_reporting_id
ON public.exports_raw_v2(reporting_id);

CREATE INDEX IF NOT EXISTS ix_exports_raw_v2_record_type
ON public.exports_raw_v2(record_type);

CREATE INDEX IF NOT EXISTS ix_exports_raw_v2_data_gin
ON public.exports_raw_v2 USING gin (data);
";

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(ddl, conn))
                    {
                        cmd.CommandTimeout = 120;
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public bool CreateExportVersionV2(
            string connectionString,
            Guid exportVersionId,
            string createdBy,
            string sourceModel,
            string revitVersion,
            string profileName,
            string profileHash,
            string comment,
            out string error)
        {
            error = null;

            const string sql = @"
INSERT INTO public.export_versions_v2
(export_version_id, created_by, source_model, revit_version, profile_name, profile_hash, comment)
VALUES
(@id, @by, @model, @revit, @pname, @phash, @comment)
ON CONFLICT (export_version_id) DO NOTHING;";

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.CommandTimeout = 60;
                        cmd.Parameters.AddWithValue("@id", exportVersionId);
                        cmd.Parameters.AddWithValue("@by", (object)createdBy ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@model", (object)sourceModel ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@revit", (object)revitVersion ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@pname", (object)profileName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@phash", (object)profileHash ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@comment", (object)comment ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// UPSERT батчем через jsonb_to_recordset.
        /// rowsJson = JSON-массив:
        /// [
        ///   { "sourceFile":"..", "recordType":"..", "reportingId":123, "data":{...} },
        ///   ...
        /// ]
        /// Возвращает affected rows.
        /// </summary>
        public int UpsertExportsRawBatchV2(
            string connectionString,
            Guid exportVersionId,
            Guid exportBatchId,
            string rowsJson,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(rowsJson))
                return 0;

            const string sql = @"
WITH rows AS (
    SELECT *
    FROM jsonb_to_recordset(@rows::jsonb)
         AS x(sourceFile text, recordType text, reportingId integer, data jsonb)
)
INSERT INTO public.exports_raw_v2
(export_version_id, export_batch_id, source_file, record_type, reporting_id, data)
SELECT
    @ver,
    @batch,
    rows.sourceFile,
    rows.recordType,
    rows.reportingId,
    COALESCE(rows.data, '{}'::jsonb)
FROM rows
ON CONFLICT (export_version_id, record_type, reporting_id, source_file)
DO UPDATE SET
    export_batch_id = EXCLUDED.export_batch_id,
    exported_at     = now(),
    data            = EXCLUDED.data;";

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    using (var cmd = new NpgsqlCommand(sql, conn, tx))
                    {
                        cmd.CommandTimeout = 120;
                        cmd.Parameters.AddWithValue("@ver", exportVersionId);
                        cmd.Parameters.AddWithValue("@batch", exportBatchId);
                        cmd.Parameters.AddWithValue("@rows", rowsJson);

                        int affected = cmd.ExecuteNonQuery();
                        tx.Commit();
                        return affected;
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return 0;
            }
        }
    }
}