using System.Collections.ObjectModel;

namespace Points.Models;

public sealed class MissionShareEnvelope
{
    public string Format { get; set; } = MissionShareFileTypes.Format;
    public int Version { get; set; } = MissionShareFileTypes.Version;
    public string SharedBy { get; set; } = "";
    public DateTime ExportedAtUtc { get; set; }
    public MissionShareMission Mission { get; set; } = new();
}

public sealed class MissionShareMission
{
    public Guid MissionGuid { get; set; }
    public string Title { get; set; } = "";
    public string Tags { get; set; } = "";
    public string Status { get; set; } = "";
    public string Description { get; set; } = "";
    public string? SharedWith { get; set; }
    public MissionSubType SubType { get; set; }
    public double Value { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime AvailableFromDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime? EventDate { get; set; }
    public string EstimatedTimeText { get; set; } = "";
    public bool IsFailed { get; set; }
    public double ValuePerMinute { get; set; }
}

public sealed class MissionSharePreview
{
    public MissionSharePreview(
        string sourcePath,
        MissionShareEnvelope envelope,
        MissionCardModel incomingMission,
        MissionCardModel? existingMission,
        IReadOnlyList<MissionShareDiffItem> diffItems)
    {
        SourcePath = sourcePath;
        Envelope = envelope;
        IncomingMission = incomingMission;
        ExistingMission = existingMission;
        DiffItems = new ObservableCollection<MissionShareDiffItem>(diffItems);
    }

    public string SourcePath { get; }
    public MissionShareEnvelope Envelope { get; }
    public MissionCardModel IncomingMission { get; }
    public MissionCardModel? ExistingMission { get; }
    public bool IsUpdate => ExistingMission != null;
    public ObservableCollection<MissionShareDiffItem> DiffItems { get; }
}

public sealed record MissionShareDiffItem(
    string Field,
    string CurrentValue,
    string IncomingValue);

public static class MissionShareFileTypes
{
    public const string Format = "points.mission";
    public const int Version = 1;
    public const string Extension = ".pmj";
    public const string ContentType = "application/vnd.points.mission+json";
}
