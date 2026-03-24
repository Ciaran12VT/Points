using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models.DbModels
{
    public static class SettingValueTypes
    {
        public const string String = "string";
        public const string Bool = "bool";
        public const string Int = "int";
        public const string NullableInt = "nullable-int";
        public const string Double = "double";
    }

    public sealed class SettingModel
    {
        public string SettingKey { get; set; } = string.Empty;
        public string SettingValue { get; set; } = string.Empty;
        public string ValueType { get; set; } = SettingValueTypes.String;
        public string Category { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsUserEditable { get; set; }
        public int SortOrder { get; set; }
    }
}
