using Xunit;

namespace Points.Tests.Time;

public sealed class TimeHandlingGuardrailTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string AppSourceRoot = Path.Combine(RepositoryRoot, "Points");

    [Fact]
    public void DirectCurrentTimeApis_DoNotSpreadBeyondExplicitAllowances()
    {
        var allowances = Allowances(
            ("Points/Converters/LockTitleColorConverter.cs", "DateTime.Now", 1),
            ("Points/Global/GlobalVariables.cs", "DateTime.Today", 2),
            ("Points/Models/ActivityTimeMath.cs", "DateTime.Now", 1),
            ("Points/Models/ActivityTimeMath.cs", "DateTime.UtcNow", 1),
            ("Points/Services/Time/SystemClock.cs", "DateTime.Now", 1),
            ("Points/Services/Time/SystemClock.cs", "DateTime.UtcNow", 1),
            ("Points/ViewModels/AchievementDetailsViewModel.cs", "DateTime.Now", 5),
            ("Points/ViewModels/BudgetDetailsViewModel.cs", "DateTime.Now", 3),
            ("Points/ViewModels/MissionDetailsViewModel.cs", "DateTime.Today", 1),
            ("Points/Views/Achievements/TrophyViewerPage.xaml.cs", "DateTime.Now", 1),
            ("Points/Views/Cards/BudgetCardView.xaml.cs", "DateTime.Now", 1),
            ("Points/Views/Cards/MissionCardView.xaml.cs", "DateTime.Now", 1),
            ("Points/Views/Cards/ScCardView.xaml.cs", "DateTime.Now", 1),
            ("Points/Views/Cards/TrackerCardView.xaml.cs", "DateTime.UtcNow", 1),
            ("Points/Views/Details/UdmdPromptPage.cs", "DateTime.Now", 1),
            ("Points/Views/Details/UdmdPromptPage.cs", "DateTime.Today", 1),
            ("Points/Views/Popups/EditActiveTimePopup.cs", "DateTime.Now", 1),
            ("Points/Views/Schedules/CardSchedulesPage.xaml.cs", "DateTime.Now", 1),
            ("Points/Views/Schedules/ScheduleEditPage.xaml.cs", "DateTime.Now", 1),
            ("Points/Views/Shared/DateRangePickerView.xaml.cs", "DateTime.Now", 1),
            ("Points/Views/Shared/DateRangePickerView.xaml.cs", "DateTime.Today", 2));

        AssertNoNewOccurrences(
            "direct current-time API",
            new[] { "DateTime.Now", "DateTime.UtcNow", "DateTime.Today" },
            allowances,
            "Use IClock for instant/local current time. UI-only fallback usages should be intentionally reviewed and added to the allowance only when unavoidable.");
    }

    [Fact]
    public void DirectTimezoneConversionAndParsingApis_DoNotSpreadBeyondExplicitAllowances()
    {
        var allowances = Allowances(
            ("Points/Global/GlobalVariables.cs", ".ToLocalTime(", 1),
            ("Points/Global/GlobalVariables.cs", "DateTime.SpecifyKind(", 2),
            ("Points/Models/AchievementCardModel.cs", ".ToLocalTime(", 1),
            ("Points/Models/ActivityTimeMath.cs", ".ToUniversalTime(", 2),
            ("Points/Models/ActivityTimeMath.cs", "DateTime.SpecifyKind(", 2),
            ("Points/Models/MissionCardModel.cs", ".ToLocalTime(", 1),
            ("Points/Models/PlannerModels.cs", "DateTime.SpecifyKind(", 1),
            ("Points/Models/TimeScopeRange.cs", ".ToLocalTime(", 1),
            ("Points/Models/TimeScopeRange.cs", "DateTime.SpecifyKind(", 2),
            ("Points/Models/UdmdModels.cs", "DateTime.TryParse(", 1),
            ("Points/Services/Scheduling/WallClockScheduleTime.cs", ".ToLocalTime(", 1),
            ("Points/Services/Scheduling/WallClockScheduleTime.cs", "DateTime.SpecifyKind(", 3),
            ("Points/Services/Sqlite/SqliteDbService.cs", ".ToUniversalTime(", 1),
            ("Points/Services/Sqlite/SqliteDbService.cs", "DateTime.SpecifyKind(", 3),
            ("Points/Services/Sqlite/SqliteDbService.cs", "DateTime.Parse(", 4),
            ("Points/Services/Sqlite/SqliteDbService.cs", "DateTime.TryParse(", 2),
            ("Points/Services/Time/LegacyTimeReader.cs", "DateTime.SpecifyKind(", 4),
            ("Points/Services/Time/LegacyTimeReader.cs", "DateTime.TryParse(", 2),
            ("Points/Services/Time/LegacyTimeReader.cs", "DateTimeOffset.Parse(", 2),
            ("Points/Services/Time/StrictTimeSerializer.cs", ".ToUniversalTime(", 1),
            ("Points/Services/Time/StrictTimeSerializer.cs", "DateTime.SpecifyKind(", 5),
            ("Points/Services/Time/StrictTimeSerializer.cs", "DateTimeOffset.Parse(", 1),
            ("Points/Services/Time/StrictTimeSerializer.cs", "DateTimeOffset.TryParse(", 1),
            ("Points/Services/Time/TimeDisplayFormatter.cs", "DateTime.SpecifyKind(", 4),
            ("Points/Services/Time/TimeZoneService.cs", "DateTime.SpecifyKind(", 2),
            ("Points/ViewModels/LeaderboardPlannerViewModel.cs", "DateTime.SpecifyKind(", 1),
            ("Points/ViewModels/LeaderboardViewModel.cs", "DateTime.SpecifyKind(", 3),
            ("Points/Views/Details/EventTrackerDetailsPage.xaml.cs", "DateTime.TryParse(", 2));

        AssertNoNewOccurrences(
            "direct timezone conversion or parsing API",
            new[]
            {
                ".ToLocalTime(",
                ".ToUniversalTime(",
                "DateTime.SpecifyKind(",
                "DateTime.Parse(",
                "DateTime.TryParse(",
                "DateTimeOffset.Parse(",
                "DateTimeOffset.TryParse("
            },
            allowances,
            "Use ITimeZoneService, StrictTimeSerializer, LegacyTimeReader, or a named helper that documents wall-clock vs instant semantics.");
    }

    [Fact]
    public void RawRoundTripDateTimeSerialization_DoesNotSpreadBeyondLegacySqliteAllowance()
    {
        var allowances = Allowances(
            ("Points/Services/Sqlite/SqliteDbService.cs", ".ToString(\"o\"", 27));

        AssertNoNewOccurrences(
            "raw DateTime round-trip serialization",
            new[] { ".ToString(\"o\"", ".ToString(\"O\"" },
            allowances,
            "Use StrictTimeSerializer for new persistence writes. Existing SQLite allowances represent legacy code still being retired.");
    }

    private static void AssertNoNewOccurrences(
        string label,
        IReadOnlyCollection<string> tokens,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> allowances,
        string remediation)
    {
        var counts = CountOccurrences(tokens);
        var errors = new List<string>();

        foreach (var (file, tokenCounts) in counts.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            foreach (var (token, actualCount) in tokenCounts.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var allowedCount = GetAllowedCount(allowances, file, token);
                if (actualCount > allowedCount)
                    errors.Add($"{file}: '{token}' count is {actualCount}, allowed maximum is {allowedCount}.");
            }
        }

        Assert.True(
            errors.Count == 0,
            $"New {label} usage detected.{Environment.NewLine}{remediation}{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
    }

    private static IReadOnlyDictionary<string, Dictionary<string, int>> CountOccurrences(IReadOnlyCollection<string> tokens)
    {
        var result = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

        foreach (var file in EnumerateSourceFiles())
        {
            var relativePath = ToRepositoryRelativePath(file);
            var fileCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var line in File.ReadLines(file))
            {
                var code = StripLineComment(line);
                if (string.IsNullOrWhiteSpace(code))
                    continue;

                foreach (var token in tokens)
                {
                    var count = CountToken(code, token);
                    if (count == 0)
                        continue;

                    fileCounts[token] = fileCounts.GetValueOrDefault(token) + count;
                }
            }

            if (fileCounts.Count > 0)
                result[relativePath] = fileCounts;
        }

        return result;
    }

    private static IEnumerable<string> EnumerateSourceFiles()
    {
        return Directory
            .EnumerateFiles(AppSourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsUnderFolder(path, "bin"))
            .Where(path => !IsUnderFolder(path, "obj"));
    }

    private static string StripLineComment(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
            trimmed.StartsWith("*", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var commentIndex = line.IndexOf("//", StringComparison.Ordinal);
        return commentIndex >= 0 ? line[..commentIndex] : line;
    }

    private static int CountToken(string text, string token)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Allowances(
        params (string File, string Token, int MaxCount)[] entries)
    {
        var result = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (!result.TryGetValue(entry.File, out var tokens))
            {
                tokens = new Dictionary<string, int>(StringComparer.Ordinal);
                result[entry.File] = tokens;
            }

            tokens[entry.Token] = entry.MaxCount;
        }

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<string, int>)pair.Value,
            StringComparer.Ordinal);
    }

    private static int GetAllowedCount(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> allowances,
        string file,
        string token)
    {
        return allowances.TryGetValue(file, out var tokenAllowances) &&
               tokenAllowances.TryGetValue(token, out var count)
            ? count
            : 0;
    }

    private static string ToRepositoryRelativePath(string file)
    {
        return Path.GetRelativePath(RepositoryRoot, file)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private static bool IsUnderFolder(string path, string folderName)
    {
        var segments = Path.GetRelativePath(RepositoryRoot, path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return segments.Any(segment => string.Equals(segment, folderName, StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Points.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from the test output directory.");
    }
}
