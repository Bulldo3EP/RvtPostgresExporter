using System;
using System.IO;
using System.Reflection;

namespace RvtPostgresExporter.Database
{
    public static class DbAppDomainHost
    {
        private static AppDomain CreateDbDomain(out string asmPath, out string pluginDir)
        {
            asmPath = Assembly.GetExecutingAssembly().Location;
            pluginDir = Path.GetDirectoryName(asmPath);

            var configPath = Path.Combine(pluginDir, "DbDomain.config");

            var setup = new AppDomainSetup
            {
                ApplicationBase = pluginDir,
                PrivateBinPath = pluginDir,
                ShadowCopyFiles = "false",
                ConfigurationFile = File.Exists(configPath) ? configPath : null
            };

            return AppDomain.CreateDomain("RvtPostgresExporter.DbDomain", null, setup);
        }

        private static object CreateRunner(AppDomain domain, string asmPath)
        {
            return domain.CreateInstanceFromAndUnwrap(
                asmPath,
                "RvtPostgresExporter.Database.IsolatedDbRunner");
        }

        // ✅ НУЖНО ДЛЯ PostgresService.cs
        public static bool CheckConnectionIsolated(string connectionString, out string error)
        {
            error = null;
            var domain = CreateDbDomain(out var asmPath, out _);

            try
            {
                var runner = CreateRunner(domain, asmPath);
                var mi = runner.GetType().GetMethod("CheckConnection");
                if (mi == null)
                {
                    error = "IsolatedDbRunner.CheckConnection not found";
                    return false;
                }

                object[] args = { connectionString, null };
                var okObj = mi.Invoke(runner, args);
                error = args[1] as string;

                return okObj is bool b && b;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                try { AppDomain.Unload(domain); } catch { }
            }
        }

        public static bool EnsureMvpObjectsV2Isolated(string connectionString, out string error)
        {
            error = null;
            var domain = CreateDbDomain(out var asmPath, out _);

            try
            {
                var runner = CreateRunner(domain, asmPath);
                var mi = runner.GetType().GetMethod("EnsureMvpObjectsV2");
                if (mi == null)
                {
                    error = "IsolatedDbRunner.EnsureMvpObjectsV2 not found";
                    return false;
                }

                object[] args = { connectionString, null };
                var okObj = mi.Invoke(runner, args);
                error = args[1] as string;

                return okObj is bool b && b;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                try { AppDomain.Unload(domain); } catch { }
            }
        }

        public static bool CreateExportVersionV2Isolated(
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
            var domain = CreateDbDomain(out var asmPath, out _);

            try
            {
                var runner = CreateRunner(domain, asmPath);
                var mi = runner.GetType().GetMethod("CreateExportVersionV2");
                if (mi == null)
                {
                    error = "IsolatedDbRunner.CreateExportVersionV2 not found";
                    return false;
                }

                object[] args =
                {
                    connectionString, exportVersionId,
                    createdBy, sourceModel, revitVersion,
                    profileName, profileHash, comment,
                    null
                };

                var okObj = mi.Invoke(runner, args);
                error = args[8] as string;

                return okObj is bool b && b;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                try { AppDomain.Unload(domain); } catch { }
            }
        }

        public static int UpsertExportsRawBatchV2Isolated(
            string connectionString,
            Guid exportVersionId,
            Guid exportBatchId,
            string rowsJson,
            out string error)
        {
            error = null;
            var domain = CreateDbDomain(out var asmPath, out _);

            try
            {
                var runner = CreateRunner(domain, asmPath);
                var mi = runner.GetType().GetMethod("UpsertExportsRawBatchV2");
                if (mi == null)
                {
                    error = "IsolatedDbRunner.UpsertExportsRawBatchV2 not found";
                    return 0;
                }

                object[] args = { connectionString, exportVersionId, exportBatchId, rowsJson, null };
                var resObj = mi.Invoke(runner, args);
                error = args[4] as string;

                return resObj is int i ? i : 0;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return 0;
            }
            finally
            {
                try { AppDomain.Unload(domain); } catch { }
            }
        }
    }
}