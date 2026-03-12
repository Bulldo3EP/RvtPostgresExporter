using System;
using System.Globalization;
using Autodesk.Revit.DB;

namespace RvtPostgresExporter.Revit
{
    public static class RevitParameterReader
    {
        public static int GetReportingInstanceId(Element element)
        {
            if (element == null) return -1;

            ElementId reportingId = element.Id;
            if (element is FamilyInstance fi && fi.SuperComponent != null)
                reportingId = fi.SuperComponent.Id;

            return reportingId.IntegerValue;
        }

        public static string GetCategoryName(Element element)
        {
            return element?.Category?.Name ?? "";
        }

        private static ElementType GetElementType(Document doc, Element element)
        {
            if (doc == null || element == null) return null;

            var typeId = element.GetTypeId();
            if (typeId == null || typeId == ElementId.InvalidElementId) return null;

            return doc.GetElement(typeId) as ElementType;
        }

        public static string GetTypeNameFromType(Document doc, Element element)
        {
            var type = GetElementType(doc, element);
            return type == null ? "" : Normalize(type.Name);
        }

        public static string GetFamilyNameFromType(Document doc, Element element)
        {
            var type = GetElementType(doc, element);
            if (type == null) return "";

            if (type is FamilySymbol fs)
                return Normalize(fs.FamilyName);

            try { return Normalize(type.FamilyName); }
            catch { return ""; }
        }

        // Instance -> Type (твой подход)
        public static string GetUserParameterValue(Document doc, Element element, string parameterName)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (string.IsNullOrWhiteSpace(parameterName)) return "None";

            bool exists = false;

            var instanceParam = element.LookupParameter(parameterName);
            if (instanceParam != null)
            {
                exists = true;
                if (instanceParam.HasValue)
                    return ConvertParameterToString(instanceParam);

                return "";
            }

            var type = GetElementType(doc, element);
            if (type != null)
            {
                var typeParam = type.LookupParameter(parameterName);
                if (typeParam != null)
                {
                    exists = true;
                    if (typeParam.HasValue)
                        return ConvertParameterToString(typeParam);

                    return "";
                }
            }

            return exists ? "" : "None";
        }

        private static string ConvertParameterToString(Parameter param)
        {
            if (param == null) return "";

            string result;

            switch (param.StorageType)
            {
                case StorageType.String:
                    result = param.AsString();
                    break;

                case StorageType.Double:
                    // Главное: AsValueString() может вернуть "0,85 м³" или "1 250,00 мм"
                    // Мы оставляем это строкой, а нормализацию чисел делаем в CsvTxtExportService.
                    result = param.AsValueString();
                    if (string.IsNullOrWhiteSpace(result))
                    {
                        // fallback: если форматирование не дало строку
                        result = param.AsDouble().ToString(CultureInfo.InvariantCulture);
                    }
                    break;

                case StorageType.Integer:
                    try
                    {
                        if (param.Definition != null &&
                            param.Definition.GetDataType() == SpecTypeId.Boolean.YesNo)
                        {
                            result = param.AsInteger() == 1 ? "Да" : "Нет";
                        }
                        else
                        {
                            result = param.AsInteger().ToString(CultureInfo.InvariantCulture);
                        }
                    }
                    catch
                    {
                        result = param.AsInteger().ToString(CultureInfo.InvariantCulture);
                    }
                    break;

                case StorageType.ElementId:
                    var id = param.AsElementId();
                    result = (id == null || id == ElementId.InvalidElementId) ? "" : id.IntegerValue.ToString(CultureInfo.InvariantCulture);
                    break;

                default:
                    result = "";
                    break;
            }

            return Normalize(result);
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
        }
    }
}