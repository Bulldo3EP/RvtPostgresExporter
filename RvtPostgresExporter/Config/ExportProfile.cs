using System.Collections.Generic;

namespace RvtPostgresExporter.Config
{
    public sealed class ExportProfile
    {
        public int SchemaVersion { get; set; } = 1;
        public string TableName { get; set; } = "revit_elements";
        public string PrimaryKey { get; set; } = "instance_id";
        public ElementFilter ElementFilter { get; set; }
        public List<ExportColumn> Columns { get; set; } = new List<ExportColumn>();
    }

    public sealed class ElementFilter
    {
        public List<string> CategoryNames { get; set; } = new List<string>();
    }

    public sealed class ExportColumn
    {
        public string ColumnName { get; set; }          // имя столбца/колонки (и заголовок в TXT)
        public string ParameterName { get; set; }       // имя параметра Revit или спец-поле __Category и т.п.
        public string DataType { get; set; }            // text/integer/double/boolean/timestamp
        public bool Required { get; set; }              // если нужно
    }
}