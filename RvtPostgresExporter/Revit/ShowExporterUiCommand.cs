using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows;

namespace RvtPostgresExporter.Revit
{
    [Transaction(TransactionMode.Manual)]
    public sealed class ShowExporterUiCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp?.ActiveUIDocument;

            if (uiDoc == null || uiDoc.Document == null)
            {
                // Add-in Manager может запускать команду без активного документа
                TaskDialog.Show("RvtPostgresExporter",
                    "Нет активного документа Revit.\n\n" +
                    "Откройте проект (.rvt) и запустите команду снова.");
                return Result.Cancelled;
            }

            // Открываем WPF окно
            var win = new UI.MainWindow(uiDoc);
            // Важно: если окно WPF, можно привязать Owner к текущему окну Revit (опционально)
            win.ShowDialog();

            return Result.Succeeded;
        }
    }
}
