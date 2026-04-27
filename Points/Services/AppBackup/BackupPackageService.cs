using Points.Global;
using Points.Services.Sqlite.Interfaces;
using System.IO.Compression;
using System.Text.Json;

namespace Points.Services.Backup
{
    public enum BackupResourceKind
    {
        Database,
        Folder
    }

    public class BackupResourceOption
    {
        public string Key { get; init; } = "";
        public string Title { get; init; } = "";
        public string Description { get; init; } = "";
        public BackupResourceKind Kind { get; init; }
    }

    public sealed class BackupImportResource : BackupResourceOption
    {
        public string SourcePath { get; init; } = "";
    }

    public sealed class BackupImportPlan : IDisposable
    {
        public string DisplayName { get; init; } = "";
        public IReadOnlyList<BackupImportResource> Resources { get; init; } = Array.Empty<BackupImportResource>();
        public IReadOnlyList<string> CleanupPaths { get; init; } = Array.Empty<string>();

        public void Dispose()
        {
            foreach (var path in CleanupPaths)
            {
                try
                {
                    if (Directory.Exists(path))
                        Directory.Delete(path, recursive: true);
                    else if (File.Exists(path))
                        File.Delete(path);
                }
                catch
                {
                    // Best effort cleanup for cache files.
                }
            }
        }
    }

    public static class BackupPackageService
    {
        private const string ManifestFileName = "manifest.json";
        private const string PackageFormat = "PointsBackup";
        private const int PackageVersion = 1;

        private static readonly IReadOnlyList<BackupResourceDefinition> Definitions = new List<BackupResourceDefinition>
        {
            new(
                Key: "database",
                Title: "Database",
                Description: "SQLite database file.",
                Kind: BackupResourceKind.Database,
                GetLocalPath: () => AppPaths.DatabasePath,
                PackagePath: "database/points.db3"),

            new(
                Key: "achievement_trophies",
                Title: "Achievements folder",
                Description: "Achievement trophy files.",
                Kind: BackupResourceKind.Folder,
                GetLocalPath: () => AppPaths.AchievementTrophiesFolder,
                PackagePath: "folders/trophies"),

            new(
                Key: "mission_resources",
                Title: "Mission Resources folder",
                Description: "Mission resource attachments.",
                Kind: BackupResourceKind.Folder,
                GetLocalPath: () => AppPaths.MissionResourcesFolder,
                PackagePath: "folders/resources")
        };

        public static IReadOnlyList<BackupResourceOption> GetExportableResources()
        {
            return Definitions
                .Select(def => new BackupResourceOption
                {
                    Key = def.Key,
                    Title = def.Title,
                    Description = def.Description,
                    Kind = def.Kind
                })
                .ToList();
        }

        public static async Task<string> CreateExportPackageAsync(
            IDbService db,
            IEnumerable<string> selectedKeys,
            CancellationToken cancellationToken = default)
        {
            var selected = ResolveDefinitions(selectedKeys);
            var zipPath = Path.Combine(FileSystem.CacheDirectory, $"points_backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            var includesDatabase = selected.Any(x => x.Kind == BackupResourceKind.Database);
            var packageCreated = false;

            if (includesDatabase)
                await db.CloseDatabaseAsync();

            try
            {
                using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                var manifest = new BackupManifest
                {
                    Format = PackageFormat,
                    Version = PackageVersion,
                    CreatedAtUtc = DateTime.UtcNow
                };

                foreach (var definition in selected)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var localPath = definition.GetLocalPath();

                    if (definition.Kind == BackupResourceKind.Database)
                    {
                        if (!File.Exists(localPath))
                            throw new FileNotFoundException("The Points database file could not be found.", localPath);

                        archive.CreateEntryFromFile(localPath, definition.PackagePath, CompressionLevel.Optimal);
                    }
                    else
                    {
                        AddDirectoryToArchive(archive, localPath, definition.PackagePath);
                    }

                    manifest.Items.Add(new BackupManifestItem
                    {
                        Key = definition.Key,
                        Kind = definition.Kind.ToString(),
                        PackagePath = definition.PackagePath
                    });
                }

                var manifestEntry = archive.CreateEntry(ManifestFileName, CompressionLevel.Optimal);
                await using var manifestStream = manifestEntry.Open();
                await JsonSerializer.SerializeAsync(
                    manifestStream,
                    manifest,
                    new JsonSerializerOptions { WriteIndented = true },
                    cancellationToken);

                packageCreated = true;
            }
            finally
            {
                if (includesDatabase)
                    await db.ReinitializeDatabaseAsync();

                if (!packageCreated)
                    TryDelete(zipPath);
            }

            return zipPath;
        }

        public static BackupImportPlan CreateLegacyDatabaseImportPlan(string databasePath, IEnumerable<string>? cleanupPaths = null)
        {
            if (!File.Exists(databasePath))
                throw new FileNotFoundException("The selected database file could not be found.", databasePath);

            var definition = Definitions.First(x => x.Kind == BackupResourceKind.Database);

            return new BackupImportPlan
            {
                DisplayName = Path.GetFileName(databasePath),
                CleanupPaths = cleanupPaths?.ToList() ?? new List<string>(),
                Resources = new List<BackupImportResource>
                {
                    ToImportResource(definition, databasePath)
                }
            };
        }

        public static BackupImportPlan InspectPackageFolder(string packageFolderPath, IEnumerable<string>? cleanupPaths = null)
        {
            if (!Directory.Exists(packageFolderPath))
                throw new DirectoryNotFoundException($"The selected folder does not exist: {packageFolderPath}");

            var manifestPath = Path.Combine(packageFolderPath, ManifestFileName);
            if (!File.Exists(manifestPath))
                throw new InvalidDataException("The selected folder is not a Points backup package. The manifest file is missing.");

            ValidateManifest(manifestPath);

            var resources = DiscoverResources(packageFolderPath);
            if (resources.Count == 0)
                throw new InvalidDataException("No importable Points backup resources were found in the selected folder.");

            return new BackupImportPlan
            {
                DisplayName = new DirectoryInfo(packageFolderPath).Name,
                CleanupPaths = cleanupPaths?.ToList() ?? new List<string>(),
                Resources = resources
            };
        }

        public static async Task<BackupImportPlan> InspectZipPackageAsync(string zipPath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(zipPath))
                throw new FileNotFoundException("The selected backup file could not be found.", zipPath);

            var extractFolder = Path.Combine(FileSystem.CacheDirectory, $"points_import_{Guid.NewGuid():N}");
            Directory.CreateDirectory(extractFolder);

            try
            {
                ExtractZipSafely(zipPath, extractFolder);
                return InspectPackageFolder(extractFolder, new[] { extractFolder, zipPath });
            }
            catch
            {
                try
                {
                    Directory.Delete(extractFolder, recursive: true);
                }
                catch
                {
                    // Best effort cleanup for failed imports.
                }

                throw;
            }
            finally
            {
                await Task.CompletedTask;
            }
        }

        public static async Task RestoreAsync(
            IDbService db,
            BackupImportPlan plan,
            IEnumerable<string> selectedKeys,
            CancellationToken cancellationToken = default)
        {
            var selectedKeySet = selectedKeys.ToHashSet(StringComparer.Ordinal);
            var selectedResources = plan.Resources
                .Where(x => selectedKeySet.Contains(x.Key))
                .ToList();

            if (selectedResources.Count == 0)
                throw new InvalidOperationException("Select at least one item to import.");

            var includesDatabase = selectedResources.Any(x => x.Kind == BackupResourceKind.Database);

            if (includesDatabase)
                await db.CloseDatabaseAsync();

            try
            {
                foreach (var resource in selectedResources.Where(x => x.Kind == BackupResourceKind.Folder))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var definition = Definitions.First(x => x.Key == resource.Key);
                    RestoreFolder(resource.SourcePath, definition.GetLocalPath());
                }

                var databaseResource = selectedResources.FirstOrDefault(x => x.Kind == BackupResourceKind.Database);
                if (databaseResource != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Directory.CreateDirectory(AppPaths.DbFolder);
                    File.Copy(databaseResource.SourcePath, AppPaths.DatabasePath, overwrite: true);
                }
            }
            finally
            {
                if (includesDatabase)
                    await db.ReinitializeDatabaseAsync();
            }
        }

        private static List<BackupResourceDefinition> ResolveDefinitions(IEnumerable<string> selectedKeys)
        {
            var keySet = selectedKeys.ToHashSet(StringComparer.Ordinal);
            var selected = Definitions
                .Where(x => keySet.Contains(x.Key))
                .ToList();

            if (selected.Count == 0)
                throw new InvalidOperationException("Select at least one item to export.");

            return selected;
        }

        private static List<BackupImportResource> DiscoverResources(string packageFolderPath)
        {
            return Definitions
                .Select(def =>
                {
                    var sourcePath = PackagePathToLocalPath(packageFolderPath, def.PackagePath);

                    var exists = def.Kind == BackupResourceKind.Database
                        ? File.Exists(sourcePath)
                        : Directory.Exists(sourcePath);

                    return exists ? ToImportResource(def, sourcePath) : null;
                })
                .Where(x => x != null)
                .Cast<BackupImportResource>()
                .ToList();
        }

        private static BackupImportResource ToImportResource(BackupResourceDefinition definition, string sourcePath)
        {
            return new BackupImportResource
            {
                Key = definition.Key,
                Title = definition.Title,
                Description = definition.Description,
                Kind = definition.Kind,
                SourcePath = sourcePath
            };
        }

        private static void ValidateManifest(string manifestPath)
        {
            var manifest = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(manifestPath));
            if (manifest == null ||
                !string.Equals(manifest.Format, PackageFormat, StringComparison.Ordinal) ||
                manifest.Version > PackageVersion)
            {
                throw new InvalidDataException("The selected folder is not a supported Points backup package.");
            }
        }

        private static void AddDirectoryToArchive(ZipArchive archive, string sourceFolder, string packageFolder)
        {
            Directory.CreateDirectory(sourceFolder);

            foreach (var file in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceFolder, file);
                archive.CreateEntryFromFile(file, CombineZipPath(packageFolder, relativePath), CompressionLevel.Optimal);
            }
        }

        private static void ExtractZipSafely(string zipPath, string destinationFolder)
        {
            var destinationRoot = Path.GetFullPath(destinationFolder);
            var destinationRootWithSeparator = destinationRoot.EndsWith(Path.DirectorySeparatorChar)
                ? destinationRoot
                : destinationRoot + Path.DirectorySeparatorChar;

            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
                if (!destinationPath.StartsWith(destinationRootWithSeparator, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The selected zip contains an invalid path.");

                if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                entry.ExtractToFile(destinationPath, overwrite: true);
            }
        }

        private static void RestoreFolder(string sourceFolder, string destinationFolder)
        {
            if (!Directory.Exists(sourceFolder))
                throw new DirectoryNotFoundException($"The backup folder could not be found: {sourceFolder}");

            var sourceFullPath = Path.GetFullPath(sourceFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var destinationFullPath = Path.GetFullPath(destinationFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(sourceFullPath, destinationFullPath, StringComparison.OrdinalIgnoreCase))
                return;

            var parentFolder = Path.GetDirectoryName(destinationFullPath);
            if (string.IsNullOrWhiteSpace(parentFolder))
                throw new InvalidOperationException("Could not resolve the folder restore destination.");

            Directory.CreateDirectory(parentFolder);

            var destinationName = Path.GetFileName(destinationFullPath);
            var tempFolder = Path.Combine(parentFolder, $".{destinationName}_restore_{Guid.NewGuid():N}");
            var backupFolder = Path.Combine(parentFolder, $".{destinationName}_backup_{Guid.NewGuid():N}");
            var restoreFailed = false;
            var backupRestoredOrNotNeeded = false;

            CopyDirectory(sourceFolder, tempFolder);

            try
            {
                if (Directory.Exists(destinationFolder))
                {
                    Directory.Move(destinationFolder, backupFolder);
                }
                else
                {
                    backupRestoredOrNotNeeded = true;
                }

                Directory.Move(tempFolder, destinationFolder);
                backupRestoredOrNotNeeded = true;
            }
            catch
            {
                restoreFailed = true;

                try
                {
                    if (!Directory.Exists(destinationFolder) && Directory.Exists(backupFolder))
                    {
                        Directory.Move(backupFolder, destinationFolder);
                        backupRestoredOrNotNeeded = true;
                    }
                }
                catch
                {
                    // Preserve the original exception.
                }

                throw;
            }
            finally
            {
                TryDeleteDirectory(tempFolder);

                if (!restoreFailed || backupRestoredOrNotNeeded)
                    TryDeleteDirectory(backupFolder);
            }
        }

        private static void CopyDirectory(string sourceFolder, string destinationFolder)
        {
            Directory.CreateDirectory(destinationFolder);

            foreach (var directory in Directory.EnumerateDirectories(sourceFolder, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceFolder, directory);
                Directory.CreateDirectory(Path.Combine(destinationFolder, relativePath));
            }

            foreach (var file in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceFolder, file);
                var destinationPath = Path.Combine(destinationFolder, relativePath);
                var destinationDirectory = Path.GetDirectoryName(destinationPath);

                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                File.Copy(file, destinationPath, overwrite: true);
            }
        }

        private static string PackagePathToLocalPath(string rootPath, string packagePath)
        {
            return Path.Combine(new[] { rootPath }.Concat(packagePath.Split('/')).ToArray());
        }

        private static string CombineZipPath(params string[] parts)
        {
            return NormalizeZipPath(Path.Combine(parts));
        }

        private static string NormalizeZipPath(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best effort cleanup for cache files.
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
                // Best effort cleanup for failed folder swaps.
            }
        }

        private sealed record BackupResourceDefinition(
            string Key,
            string Title,
            string Description,
            BackupResourceKind Kind,
            Func<string> GetLocalPath,
            string PackagePath);

        private sealed class BackupManifest
        {
            public string Format { get; set; } = PackageFormat;
            public int Version { get; set; } = PackageVersion;
            public DateTime CreatedAtUtc { get; set; }
            public List<BackupManifestItem> Items { get; set; } = new();
        }

        private sealed class BackupManifestItem
        {
            public string Key { get; set; } = "";
            public string Kind { get; set; } = "";
            public string PackagePath { get; set; } = "";
        }
    }
}
