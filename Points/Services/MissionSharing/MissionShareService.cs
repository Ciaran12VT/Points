using Points.Global;
using Points.Models;
using Points.Services.Persistence;
using Points.Services.Time;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Points.Services.MissionSharing;

public sealed class MissionShareService : IMissionShareService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly ISettingsService _settings;
    private readonly IMissionCardService _missions;
    private readonly ICardWriteService _cards;
    private readonly IClock _clock;

    public MissionShareService(
        ISettingsService settings,
        IMissionCardService missions,
        ICardWriteService cards,
        IClock clock)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _missions = missions ?? throw new ArgumentNullException(nameof(missions));
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task ShareMissionAsync(MissionCardModel mission)
    {
        if (mission == null)
            throw new ArgumentNullException(nameof(mission));

        var path = await ExportMissionAsync(mission);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = $"Share {mission.Title}",
            File = new ShareFile(path, MissionShareFileTypes.ContentType)
        });
    }

    public async Task<MissionSharePreview> CreateImportPreviewAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("A mission share file path is required.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("The mission share file could not be found.", filePath);

        await using var stream = File.OpenRead(filePath);
        var envelope = await JsonSerializer.DeserializeAsync<MissionShareEnvelope>(stream, JsonOptions)
            ?? throw new InvalidOperationException("The mission share file is empty.");

        ValidateEnvelope(envelope);

        var incoming = ToMissionModel(envelope.Mission, _clock);
        incoming.SharedWith = string.IsNullOrWhiteSpace(envelope.SharedBy)
            ? incoming.SharedWith
            : envelope.SharedBy.Trim();

        var existing = await _missions.GetMissionCardModelByGuidAsync(incoming.MissionGuid);
        var diff = existing == null
            ? Array.Empty<MissionShareDiffItem>()
            : BuildDiff(existing, incoming);

        return new MissionSharePreview(filePath, envelope, incoming, existing, diff);
    }

    public async Task<MissionCardModel> AcceptImportAsync(MissionSharePreview preview)
    {
        if (preview == null)
            throw new ArgumentNullException(nameof(preview));

        var target = preview.ExistingMission;
        if (target == null)
        {
            target = CloneForNewImport(preview.IncomingMission);
        }
        else
        {
            ApplyImportedFields(target, preview.IncomingMission);
        }

        target.SharedWith = string.IsNullOrWhiteSpace(preview.Envelope.SharedBy)
            ? target.SharedWith
            : preview.Envelope.SharedBy.Trim();

        await _cards.SaveCardModelAsync(target);

        return target;
    }

    private async Task<string> ExportMissionAsync(MissionCardModel mission)
    {
        var envelope = new MissionShareEnvelope
        {
            SharedBy = await GetSharedByNameAsync(),
            ExportedAtUtc = _clock.UtcNow,
            Mission = ToShareMission(mission)
        };

        var directory = Path.Combine(FileSystem.CacheDirectory, "MissionShares");
        Directory.CreateDirectory(directory);

        var fileName = $"{MakeSafeFileName(mission.Title)}_{mission.MissionGuid:N}{MissionShareFileTypes.Extension}";
        var path = Path.Combine(directory, fileName);
        var json = JsonSerializer.Serialize(envelope, JsonOptions);

        await File.WriteAllTextAsync(path, json);

        return path;
    }

    private async Task<string> GetSharedByNameAsync()
    {
        var settings = await _settings.GetSettingsAsync();
        var username = settings
            .FirstOrDefault(x => x.SettingKey == SettingKeys.Username)
            ?.StringValue;

        return string.IsNullOrWhiteSpace(username)
            ? "Points user"
            : username.Trim();
    }

    private static MissionShareMission ToShareMission(MissionCardModel mission)
    {
        if (mission.MissionGuid == Guid.Empty)
            mission.MissionGuid = Guid.NewGuid();

        return new MissionShareMission
        {
            MissionGuid = mission.MissionGuid,
            Title = mission.Title ?? "",
            Tags = mission.Tags ?? "",
            Status = mission.Status ?? "",
            Description = mission.Description ?? "",
            SharedWith = mission.SharedWith,
            SubType = mission.SubType,
            Value = mission.Value,
            CreatedDate = mission.CreatedDate,
            AvailableFromDate = mission.AvailableFromDate,
            DueDate = mission.DueDate,
            CompletedDate = mission.CompletedDate,
            EventDate = mission.EventDate,
            EstimatedTimeText = mission.EstCompletionTimeText,
            IsFailed = mission.IsFailed,
            ValuePerMinute = mission.ValuePerMinute
        };
    }

    private static MissionCardModel ToMissionModel(MissionShareMission mission, IClock clock)
    {
        var localToday = clock.LocalNow.Date;

        var model = new MissionCardModel
        {
            MissionGuid = mission.MissionGuid == Guid.Empty ? Guid.NewGuid() : mission.MissionGuid,
            Title = string.IsNullOrWhiteSpace(mission.Title) ? "Shared mission" : mission.Title,
            Tags = mission.Tags ?? "",
            Description = mission.Description ?? "",
            SharedWith = mission.SharedWith,
            SubType = mission.SubType,
            Value = mission.Value,
            CreatedDate = mission.CreatedDate == default ? clock.UtcNow : mission.CreatedDate,
            AvailableFromDate = mission.AvailableFromDate == default ? localToday : mission.AvailableFromDate,
            DueDate = mission.DueDate == default ? localToday.AddDays(1) : mission.DueDate,
            EventDate = mission.EventDate,
            EstCompletionTime = ParseDuration(mission.EstimatedTimeText),
            ValuePerMinute = mission.ValuePerMinute
        };

        model.ApplyCompletionState(mission.Status, mission.IsFailed, mission.CompletedDate);
        return model;
    }

    private static MissionCardModel CloneForNewImport(MissionCardModel incoming)
    {
        var clone = new MissionCardModel
        {
            MissionGuid = incoming.MissionGuid,
            DisplayOrder = incoming.DisplayOrder,
            Title = incoming.Title,
            Tags = incoming.Tags,
            Description = incoming.Description,
            SharedWith = incoming.SharedWith,
            SubType = incoming.SubType,
            Value = incoming.Value,
            CreatedDate = incoming.CreatedDate,
            AvailableFromDate = incoming.AvailableFromDate,
            DueDate = incoming.DueDate,
            EventDate = incoming.EventDate,
            EstCompletionTime = incoming.EstCompletionTime,
            ValuePerMinute = incoming.ValuePerMinute
        };

        clone.ApplyCompletionState(incoming.Status, incoming.IsFailed, incoming.CompletedDate);
        return clone;
    }

    private static void ApplyImportedFields(MissionCardModel target, MissionCardModel incoming)
    {
        target.MissionGuid = incoming.MissionGuid;
        target.Title = incoming.Title;
        target.Tags = incoming.Tags;
        target.Description = incoming.Description;
        target.SubType = incoming.SubType;
        target.Value = incoming.Value;
        target.CreatedDate = incoming.CreatedDate;
        target.AvailableFromDate = incoming.AvailableFromDate;
        target.DueDate = incoming.DueDate;
        target.EventDate = incoming.EventDate;
        target.EstCompletionTime = incoming.EstCompletionTime;
        target.ValuePerMinute = incoming.ValuePerMinute;
        target.ApplyCompletionState(incoming.Status, incoming.IsFailed, incoming.CompletedDate);
    }

    private static IReadOnlyList<MissionShareDiffItem> BuildDiff(
        MissionCardModel current,
        MissionCardModel incoming)
    {
        var rows = new List<MissionShareDiffItem>();

        AddDiff(rows, "Title", current.Title, incoming.Title);
        AddDiff(rows, "Tags", current.Tags, incoming.Tags);
        AddDiff(rows, "Status", current.Status, incoming.Status);
        AddDiff(rows, "Description", current.Description, incoming.Description);
        AddDiff(rows, "Shared With", current.SharedWith, incoming.SharedWith);
        AddDiff(rows, "SubType", current.SubType.ToString(), incoming.SubType.ToString());
        AddDiff(rows, "Value", FormatNumber(current.Value), FormatNumber(incoming.Value));
        AddDiff(rows, "Value / Min", FormatNumber(current.ValuePerMinute), FormatNumber(incoming.ValuePerMinute));
        AddDiff(rows, "Available", FormatDate(current.AvailableFromDate), FormatDate(incoming.AvailableFromDate));
        AddDiff(rows, "Due", FormatDate(current.DueDate), FormatDate(incoming.DueDate));
        AddDiff(rows, "Event", FormatDate(current.EventDate), FormatDate(incoming.EventDate));
        AddDiff(rows, "Completed", FormatDate(current.CompletedDate), FormatDate(incoming.CompletedDate));
        AddDiff(rows, "Estimate", current.EstCompletionTimeText, incoming.EstCompletionTimeText);

        return rows;
    }

    private static void AddDiff(
        ICollection<MissionShareDiffItem> rows,
        string field,
        string? current,
        string? incoming)
    {
        current ??= "";
        incoming ??= "";

        if (string.Equals(current, incoming, StringComparison.Ordinal))
            return;

        rows.Add(new MissionShareDiffItem(field, current, incoming));
    }

    private static void ValidateEnvelope(MissionShareEnvelope envelope)
    {
        if (!string.Equals(envelope.Format, MissionShareFileTypes.Format, StringComparison.Ordinal))
            throw new InvalidOperationException("This is not a Points mission share file.");

        if (envelope.Version != MissionShareFileTypes.Version)
            throw new InvalidOperationException("This Points mission share file version is not supported.");

        if (envelope.Mission.MissionGuid == Guid.Empty)
            throw new InvalidOperationException("The mission share file does not include a valid mission identifier.");
    }

    private static TimeSpan? ParseDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var parts = value.Split(':');
        if (parts.Length != 3)
            return null;

        return int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours) &&
               int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) &&
               int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? new TimeSpan(hours, minutes, seconds)
            : null;
    }

    private static string MakeSafeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "mission";

        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        var trimmed = name.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "mission" : trimmed;
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatDate(DateTime value)
    {
        return value == default
            ? ""
            : value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private static string FormatDate(DateTime? value)
    {
        return value.HasValue ? FormatDate(value.Value) : "";
    }
}
