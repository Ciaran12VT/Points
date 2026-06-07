using Points.Services.Sqlite;
using Microsoft.Maui.Graphics;
using Points.Models;
using Points.Services.Shortcuts;
using Points.Services.Persistence;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.Shortcuts
{
    public sealed class SqliteShortcutServiceTests
    {
        [Fact]
        public async Task UpsertShortcutGroupAsync_InsertsGroupAndSetsId()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteShortcutService(context);

            var group = await service.UpsertShortcutGroupAsync(new ShortcutGroupModel
            {
                Name = "Focus",
                Color = Colors.Red,
                ShortcutGroupOrder = 2
            });

            var saved = Assert.Single(await service.GetShortcutGroupsAsync());
            Assert.True(group.ShortcutGroupId > 0);
            Assert.Equal(group.ShortcutGroupId, saved.ShortcutGroupId);
            Assert.Equal("Focus", saved.Name);
            Assert.Equal(2, saved.ShortcutGroupOrder);
            Assert.Equal("#FFFF0000", ToHexArgb(saved.Color));
        }

        [Fact]
        public async Task UpsertShortcutGroupAsync_UpdatesExistingGroupCaseInsensitively()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteShortcutService(context);

            var first = await service.UpsertShortcutGroupAsync(new ShortcutGroupModel
            {
                Name = "Focus",
                Color = Colors.Red,
                ShortcutGroupOrder = 1
            });

            var second = await service.UpsertShortcutGroupAsync(new ShortcutGroupModel
            {
                Name = "focus",
                Color = Colors.Blue,
                ShortcutGroupOrder = 5
            });

            var saved = Assert.Single(await service.GetShortcutGroupsAsync());
            Assert.Equal(first.ShortcutGroupId, second.ShortcutGroupId);
            Assert.Equal(first.ShortcutGroupId, saved.ShortcutGroupId);
            Assert.Equal("Focus", saved.Name);
            Assert.Equal(5, saved.ShortcutGroupOrder);
            Assert.Equal("#FF0000FF", ToHexArgb(saved.Color));
        }

        [Fact]
        public async Task SaveShortcutAsync_InsertsAndUpdatesShortcut()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteShortcutService(context);
            var group = await service.UpsertShortcutGroupAsync(new ShortcutGroupModel { Name = "Main" });
            var shortcut = new ShortcutModel
            {
                IconChar = " * ",
                TargetCardId = 42,
                ShortcutGroupId = group.ShortcutGroupId,
                ShortcutOrder = 1
            };

            await service.SaveShortcutAsync(shortcut);
            shortcut.IconChar = "!";
            shortcut.TargetCardId = 84;
            shortcut.ShortcutOrder = 3;

            await service.SaveShortcutAsync(shortcut);

            var saved = Assert.Single(await service.GetDashboardShortcutsAsync());
            Assert.True(shortcut.ShortcutId > 0);
            Assert.Equal(shortcut.ShortcutId, saved.ShortcutId);
            Assert.Equal("!", saved.IconChar);
            Assert.Equal(84, saved.TargetCardId);
            Assert.Equal(3, saved.ShortcutOrder);
            Assert.NotNull(saved.Group);
            Assert.Equal("Main", saved.Group!.Name);
        }

        [Fact]
        public async Task GetDashboardShortcutsAsync_OrdersByGroupThenShortcut()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteShortcutService(context);
            var laterGroup = await service.UpsertShortcutGroupAsync(new ShortcutGroupModel
            {
                Name = "Later",
                ShortcutGroupOrder = 2
            });
            var earlierGroup = await service.UpsertShortcutGroupAsync(new ShortcutGroupModel
            {
                Name = "Earlier",
                ShortcutGroupOrder = 1
            });

            await service.SaveShortcutAsync(new ShortcutModel { IconChar = "B", TargetCardId = 2, ShortcutGroupId = laterGroup.ShortcutGroupId, ShortcutOrder = 1 });
            await service.SaveShortcutAsync(new ShortcutModel { IconChar = "C", TargetCardId = 3, ShortcutGroupId = earlierGroup.ShortcutGroupId, ShortcutOrder = 2 });
            await service.SaveShortcutAsync(new ShortcutModel { IconChar = "A", TargetCardId = 1, ShortcutGroupId = earlierGroup.ShortcutGroupId, ShortcutOrder = 1 });

            var shortcuts = await service.GetDashboardShortcutsAsync();

            Assert.Equal(new[] { "A", "C", "B" }, shortcuts.Select(s => s.IconChar));
        }

        [Fact]
        public async Task DeleteShortcutGroupAsync_CascadesToShortcuts()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteShortcutService(context);
            var group = await service.UpsertShortcutGroupAsync(new ShortcutGroupModel { Name = "Main" });
            await service.SaveShortcutAsync(new ShortcutModel
            {
                IconChar = "*",
                TargetCardId = 42,
                ShortcutGroupId = group.ShortcutGroupId,
                ShortcutOrder = 1
            });

            await service.DeleteShortcutGroupAsync(group.ShortcutGroupId);

            Assert.Empty(await service.GetShortcutGroupsAsync());
            Assert.Empty(await service.GetDashboardShortcutsAsync());
        }

        private static string ToHexArgb(Color color)
        {
            var a = (byte)Math.Round(color.Alpha * 255);
            var r = (byte)Math.Round(color.Red * 255);
            var g = (byte)Math.Round(color.Green * 255);
            var b = (byte)Math.Round(color.Blue * 255);
            return $"#{a:X2}{r:X2}{g:X2}{b:X2}";
        }

        private sealed class TestSqliteConnectionContext : ISqliteConnectionContext, IAsyncDisposable
        {
            private SQLiteAsyncConnection? _db;

            public TestSqliteConnectionContext()
            {
                DatabasePath = Path.Combine(
                    Path.GetTempPath(),
                    $"PointsShortcutServiceTests-{Guid.NewGuid():N}.db");
            }

            public string DatabasePath { get; }

            public SQLiteAsyncConnection Db => _db ?? throw new InvalidOperationException("DB not initialized.");

            public async Task InitializeAsync()
            {
                if (_db != null)
                    return;

                Batteries_V2.Init();

                _db = new SQLiteAsyncConnection(
                    DatabasePath,
                    SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

                await _db.ExecuteAsync("PRAGMA foreign_keys = ON;");
                await _db.ExecuteAsync("""
                    CREATE TABLE IF NOT EXISTS ShortcutGroup (
                        ShortcutGroupId INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Color TEXT NOT NULL DEFAULT '#FF000000',
                        ShortcutGroupOrder INTEGER NOT NULL DEFAULT 0
                    );
                    """);
                await _db.ExecuteAsync("""
                    CREATE UNIQUE INDEX IF NOT EXISTS UX_ShortcutGroup_Name
                    ON ShortcutGroup(Name);
                    """);
                await _db.ExecuteAsync("""
                    CREATE TABLE IF NOT EXISTS Shortcut (
                        ShortcutId INTEGER PRIMARY KEY AUTOINCREMENT,
                        IconChar TEXT NOT NULL DEFAULT '',
                        TargetCardId INTEGER NOT NULL,
                        ShortcutGroupId INTEGER NOT NULL,
                        ShortcutOrder INTEGER NOT NULL DEFAULT 0,
                        FOREIGN KEY (ShortcutGroupId) REFERENCES ShortcutGroup(ShortcutGroupId) ON DELETE CASCADE
                    );
                    """);
            }

            public async Task CloseDatabaseAsync()
            {
                if (_db == null)
                    return;

                await _db.CloseAsync();
                _db = null;
            }

            public async Task ReinitializeDatabaseAsync()
            {
                await CloseDatabaseAsync();
                await InitializeAsync();
            }

            public async Task RunInTransactionAsync(Action<SQLiteConnection> action)
            {
                if (action == null)
                    throw new ArgumentNullException(nameof(action));

                await InitializeAsync();

                await Db.RunInTransactionAsync(conn =>
                {
                    conn.Execute("PRAGMA foreign_keys = ON;");
                    action(conn);
                });
            }

            public async ValueTask DisposeAsync()
            {
                await CloseDatabaseAsync();

                if (File.Exists(DatabasePath))
                    File.Delete(DatabasePath);
            }
        }
    }
}
