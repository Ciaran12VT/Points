using System.Globalization;

namespace Points.Models;

public enum UdmdFieldType
{
    Text,
    Dropdown,
    Number,
    Date,
    Boolean,
    Image
}

public static class UdmdRelatedEntityTypes
{
    public const string Activity = "Activity";
    public const string BudgetTransaction = "BudgetTransaction";
    public const string TrackerValue = "TrackerValue";

    private static readonly HashSet<string> SupportedValues = new(StringComparer.Ordinal)
    {
        Activity,
        BudgetTransaction,
        TrackerValue
    };

    public static bool IsSupported(string? value) =>
        !string.IsNullOrWhiteSpace(value) && SupportedValues.Contains(value);
}

public sealed class UdmdConfigModel
{
    public long UdmdConfigID { get; set; }
    public long CardID { get; set; }
    public string FieldName { get; set; } = "";
    public string FieldType { get; set; } = UdmdFieldType.Text.ToString();
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public UdmdFieldType FieldTypeKind
    {
        get
        {
            return Enum.TryParse<UdmdFieldType>(FieldType, true, out var parsed)
                ? parsed
                : UdmdFieldType.Text;
        }
        set => FieldType = value.ToString();
    }
}

public sealed class UdmdDropdownModel
{
    public long UdmdDropdownID { get; set; }
    public long UdmdConfigID { get; set; }
    public string DropdownValue { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UdmdTransModel
{
    public long UdmdTransID { get; set; }
    public long CardID { get; set; }
    public long UdmdConfigID { get; set; }
    public string RelatedEntityType { get; set; } = "";
    public long RelatedEntityId { get; set; }
    public string FieldValue { get; set; } = "";

    public string FieldName { get; set; } = "";
    public string FieldType { get; set; } = UdmdFieldType.Text.ToString();

    public UdmdFieldType FieldTypeKind
    {
        get
        {
            return Enum.TryParse<UdmdFieldType>(FieldType, true, out var parsed)
                ? parsed
                : UdmdFieldType.Text;
        }
    }
}

public sealed class UdmdValueInput
{
    public long UdmdConfigID { get; set; }
    public string? FieldValue { get; set; }
}

public sealed class UdmdFieldPromptModel
{
    public UdmdConfigModel Config { get; init; } = new();
    public IReadOnlyList<UdmdDropdownModel> DropdownValues { get; init; } = Array.Empty<UdmdDropdownModel>();
    public string? FieldValue { get; set; }
}

public static class UdmdValueFormatter
{
    public static string ToDisplayString(UdmdTransModel value)
    {
        if (value.FieldTypeKind == UdmdFieldType.Date &&
            DateTime.TryParse(value.FieldValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
        {
            return dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        }

        return value.FieldValue;
    }
}
