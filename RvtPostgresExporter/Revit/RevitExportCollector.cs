using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace RvtPostgresExporter.Revit
{
    public sealed class RevitExportCollector
    {
        public sealed class ExportRow
        {
            public string SourceFile { get; set; }
            public string RecordType { get; set; } // ModelElement | Room

            public Document Doc { get; set; }
            public Element Element { get; set; }

            public int ReportingId { get; set; }
            public string CategoryName { get; set; }
            public string FamilyName { get; set; }
            public string TypeName { get; set; }
        }

        public List<ExportRow> CollectFromLinksWithRooms(
            Document mainDoc,
            string navisViewNameContains = "navisworks",
            bool includeRooms = true)
        {
            if (mainDoc == null) throw new ArgumentNullException(nameof(mainDoc));

            var rows = new List<ExportRow>();

            var linkInstances = new FilteredElementCollector(mainDoc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();

            foreach (var linkInst in linkInstances)
            {
                var linkedDoc = linkInst.GetLinkDocument();
                if (linkedDoc == null)
                    continue;

                string sourceName = Path.GetFileName(linkedDoc.PathName);
                if (string.IsNullOrWhiteSpace(sourceName))
                    sourceName = linkedDoc.Title;

                // A) Model elements from Navisworks view
                var view = Find3dView(linkedDoc, navisViewNameContains);
                if (view != null)
                {
                    var elems = new FilteredElementCollector(linkedDoc, view.Id)
                        .WhereElementIsNotElementType()
                        .ToElements()
                        .Where(IsAllowedModelElement)
                        .ToList();

                    foreach (var e in elems)
                        rows.Add(BuildRow(linkedDoc, sourceName, e, "ModelElement"));
                }

                // B) Rooms
                if (includeRooms)
                {
                    var rooms = new FilteredElementCollector(linkedDoc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .ToElements()
                        .ToList();

                    foreach (var r in rooms)
                        rows.Add(BuildRow(linkedDoc, sourceName, r, "Room"));
                }
            }

            return rows;
        }

        private static bool IsAllowedModelElement(Element e)
        {
            if (e == null) return false;
            if (e.Category == null) return false;

            int catId = e.Category.Id.IntegerValue;

            // исключаем Камеры
            if (catId == (int)BuiltInCategory.OST_Cameras)
                return false;

            return true;
        }

        private ExportRow BuildRow(Document doc, string sourceName, Element e, string recordType)
        {
            return new ExportRow
            {
                Doc = doc,
                Element = e,
                SourceFile = sourceName,
                RecordType = recordType,

                ReportingId = RevitParameterReader.GetReportingInstanceId(e),
                CategoryName = RevitParameterReader.GetCategoryName(e),

                // ВАЖНО: теперь это будет НЕ null
                FamilyName = RevitParameterReader.GetFamilyNameFromType(doc, e),
                TypeName = RevitParameterReader.GetTypeNameFromType(doc, e)
            };
        }

        private static View Find3dView(Document doc, string contains)
        {
            if (doc == null) return null;
            if (string.IsNullOrWhiteSpace(contains)) contains = "navisworks";

            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate)
                .ToList();

            var exact = views.FirstOrDefault(v =>
                v.Name.Equals(contains, StringComparison.CurrentCultureIgnoreCase));
            if (exact != null) return exact;

            return views.FirstOrDefault(v =>
                v.Name.IndexOf(contains, StringComparison.CurrentCultureIgnoreCase) >= 0);
        }
    }
}