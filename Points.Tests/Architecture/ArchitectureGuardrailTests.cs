using Points.Services.Sqlite;
using Xunit;

namespace Points.Tests.Architecture;

public sealed class ArchitectureGuardrailTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string AppSourceRoot = Path.Combine(RepositoryRoot, "Points");

    [Fact]
    public void PlatformNavigationAndDialogApis_StayBehindNavigationServices()
    {
        var tokens = new[]
        {
            "Shell.Current",
            "Application.Current.MainPage",
            "Application.Current!.MainPage",
            ".DisplayAlert(",
            ".DisplayActionSheet(",
            ".DisplayPromptAsync("
        };

        var violations = new List<string>();

        foreach (var occurrence in FindOccurrences(tokens, ".cs", ".xaml"))
        {
            if (IsAllowedPlatformUiUsage(occurrence))
                continue;

            violations.Add(FormatOccurrence(occurrence));
        }

        Assert.True(
            violations.Count == 0,
            "Direct navigation/dialog platform APIs must stay behind Points/Services/Navigation or be routed through injected dialog/navigation abstractions."
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void LegacyAggregatePersistenceService_IsRetired()
    {
        var violations = FindOccurrences(new[] { "IDbService" }, ".cs", ".xaml")
            .Select(FormatOccurrence)
            .ToList();

        Assert.True(
            violations.Count == 0,
            "The old aggregate IDbService should not reappear; consumers must depend on focused persistence interfaces."
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ConcreteSqliteServices_DoNotLeakIntoViewsOrViewModels()
    {
        var violations = FindOccurrences(new[] { "SqliteDbService", "new Sqlite" }, ".cs")
            .Where(occurrence =>
                occurrence.File.StartsWith("Points/ViewModels/", StringComparison.Ordinal) ||
                occurrence.File.StartsWith("Points/Views/", StringComparison.Ordinal))
            .Select(FormatOccurrence)
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Views and ViewModels should depend on focused abstractions, not concrete SQLite services."
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void DomainLayers_DoNotReferenceSqliteNamespaces()
    {
        var violations = FindOccurrences(new[] { "using Points.Services.Sqlite;" }, ".cs")
            .Where(occurrence =>
                occurrence.File.StartsWith("Points/ViewModels/", StringComparison.Ordinal) ||
                occurrence.File.StartsWith("Points/Views/", StringComparison.Ordinal))
            .Select(FormatOccurrence)
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Views and ViewModels should depend on Points.Services.Persistence contracts rather than SQLite-specific namespaces."
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void LegacySqliteInterfacesFolder_DoesNotReappear()
    {
        var oldFolder = Path.Combine(AppSourceRoot, "Services", "Sqlite", "Interfaces");
        var oldNamespace = string.Concat("Points.Services.Sqlite", ".Interfaces");
        var violations = FindOccurrences(new[] { oldNamespace }, ".cs", ".csproj")
            .Select(FormatOccurrence)
            .ToList();

        Assert.True(
            !Directory.Exists(oldFolder) && violations.Count == 0,
            "Domain persistence contracts belong under Points.Services.Persistence. SQLite-specific contracts belong under Points.Services.Sqlite."
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ViewModels_AreOrganizedIntoFeatureFolders()
    {
        var rootViewModels = Directory
            .EnumerateFiles(Path.Combine(AppSourceRoot, "ViewModels"), "*.cs", SearchOption.TopDirectoryOnly)
            .Select(ToRepositoryRelativePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            rootViewModels.Count == 0,
            "ViewModels should live in feature folders under Points/ViewModels rather than in the root folder."
            + Environment.NewLine
            + string.Join(Environment.NewLine, rootViewModels));
    }

    [Fact]
    public void RetiredViewCatchAllFolders_DoNotReappear()
    {
        var retiredFolders = new[]
        {
            Path.Combine(AppSourceRoot, "Views", "Details"),
            Path.Combine(AppSourceRoot, "Views", "Popups")
        };

        var retiredNamespaces = new[]
        {
            "Points.Views.Details",
            "Points.Views.Popups"
        };

        var violations = retiredFolders
            .Where(Directory.Exists)
            .Select(path => ToRepositoryRelativePath(path))
            .Concat(FindOccurrences(retiredNamespaces, ".cs", ".xaml")
                .Select(FormatOccurrence))
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Feature pages and popups should live under named feature folders rather than catch-all Views/Details or Views/Popups buckets."
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void XamlEventHandlers_DoNotSpreadBeyondExplicitAllowances()
    {
        var allowances = Allowances();

        AssertNoNewOccurrences(
            "XAML event handler",
            new[] { "Clicked=\"", "Tapped=\"", "Invoked=\"" },
            allowances,
            ".xaml",
            "Prefer command bindings and ViewModel/coordinator interaction flows. Existing allowances represent cleanup debt scheduled for later passes.");
    }

    [Fact]
    public void ServiceLocatorUsage_DoesNotSpreadBeyondExplicitAllowances()
    {
        var allowances = Allowances(
            ("Points/Platforms/Android/ActiveCardForegroundService.cs", "ServiceHelper.GetService", 5),
            ("Points/Platforms/Android/AlarmReceiver.cs", "ServiceHelper.GetService", 2),
            ("Points/Platforms/Android/BootReceiver.cs", "ServiceHelper.GetService", 2),
            ("Points/Platforms/Android/ScheduledBackupWorker.cs", "ServiceHelper.GetService", 1));

        AssertNoNewOccurrences(
            "service locator",
            new[] { "ServiceHelper.GetService" },
            allowances,
            ".cs",
            "Prefer constructor injection. Service locator access is only allowed at platform entrypoints that cannot be built by the container.");
    }

    [Fact]
    public void DirectContainerResolution_StaysInCompositionRoot()
    {
        var violations = FindOccurrences(
                new[] { "IServiceProvider", "GetRequiredService", ".GetService(" },
                ".cs")
            .Where(occurrence =>
                occurrence.File is not "Points/MauiProgram.cs" &&
                occurrence.File is not "Points/AppShell.xaml.cs" &&
                occurrence.File is not "Points/Helpers/ServiceHelper.cs")
            .Select(FormatOccurrence)
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Direct container access belongs in the composition root or ServiceHelper platform bridge; app code should receive dependencies through constructors."
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    private static bool IsAllowedPlatformUiUsage(SourceOccurrence occurrence)
    {
        if (occurrence.File.StartsWith("Points/Services/Navigation/", StringComparison.Ordinal))
            return true;

        if (occurrence.Token is ".DisplayAlert(" or ".DisplayActionSheet(" or ".DisplayPromptAsync(")
        {
            return occurrence.Code.Contains("_dialogs.", StringComparison.Ordinal) ||
                   occurrence.Code.Contains("Dialogs.", StringComparison.Ordinal);
        }

        return false;
    }

    private static void AssertNoNewOccurrences(
        string label,
        IReadOnlyCollection<string> tokens,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> allowances,
        string extension,
        string remediation)
    {
        var counts = CountOccurrences(tokens, extension);
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

    private static IReadOnlyDictionary<string, Dictionary<string, int>> CountOccurrences(
        IReadOnlyCollection<string> tokens,
        string extension)
    {
        var result = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

        foreach (var file in EnumerateSourceFiles(extension))
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

    private static IEnumerable<SourceOccurrence> FindOccurrences(
        IReadOnlyCollection<string> tokens,
        params string[] extensions)
    {
        foreach (var file in EnumerateSourceFiles(extensions))
        {
            var relativePath = ToRepositoryRelativePath(file);
            var lineNumber = 0;

            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                var code = StripLineComment(line);
                if (string.IsNullOrWhiteSpace(code))
                    continue;

                foreach (var token in tokens)
                {
                    if (!code.Contains(token, StringComparison.Ordinal))
                        continue;

                    yield return new SourceOccurrence(relativePath, lineNumber, token, code.Trim());
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateSourceFiles(params string[] extensions)
    {
        return extensions
            .SelectMany(extension => Directory.EnumerateFiles(AppSourceRoot, $"*{extension}", SearchOption.AllDirectories))
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

    private static string FormatOccurrence(SourceOccurrence occurrence)
    {
        return $"{occurrence.File}:{occurrence.LineNumber}: '{occurrence.Token}' in {occurrence.Code}";
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

    private sealed record SourceOccurrence(string File, int LineNumber, string Token, string Code);
}
