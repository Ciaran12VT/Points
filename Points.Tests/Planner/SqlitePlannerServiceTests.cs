using Points.Models;
using Points.Services.Planner;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Time;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.Planner
{
    public sealed class SqlitePlannerServiceTests
    {
        [Fact]
        public async Task SavePlannerAsync_InsertsPlannerTasksAndEvents()
        {
            await using var context = new TestSqliteConnectionContext();
            var cards = new TestPlannerCardSource();
            var service = CreateService(context, cards);
            var planner = new PlannerModel
            {
                PlannerDate = Local(2026, 4, 29),
                Tasks =
                {
                    new PlannerTaskModel
                    {
                        CardId = 10,
                        CardKind = PlannerTaskCardKind.TatCard,
                        PlannedStart = Local(2026, 4, 29, 9),
                        PlannedEnd = Local(2026, 4, 29, 10)
                    },
                    new PlannerTaskModel
                    {
                        CardId = 20,
                        CardKind = PlannerTaskCardKind.ScCard,
                        PlannedStart = Local(2026, 4, 29, 11),
                        PlannedEnd = Local(2026, 4, 29, 12)
                    }
                },
                Events =
                {
                    new PlannerEventModel
                    {
                        EventKind = PlannerEventKind.ScStepRep,
                        CardId = 20,
                        ScCardStepId = 7,
                        PlannedTime = Local(2026, 4, 29, 12, 30),
                        PlannedCount = 0
                    }
                }
            };

            await service.SavePlannerAsync(planner);

            Assert.True(planner.PlannerId > 0);
            Assert.All(planner.Tasks, task => Assert.Equal(planner.PlannerId, task.PlannerId));
            Assert.All(planner.Events, ev => Assert.Equal(planner.PlannerId, ev.PlannerId));

            var row = Assert.Single(await context.GetPlannerRowsAsync());
            Assert.Equal("2026-04-29", row.PlannerDate);
            Assert.Equal("2026-04-29T10:30:00.0000000Z", row.CreatedAt);
            Assert.Equal("2026-04-29T10:30:00.0000000Z", row.UpdatedAt);

            var tasks = await context.GetTaskRowsAsync(planner.PlannerId);
            Assert.Equal(new[] { 10L, 20L }, tasks.Select(task => task.CardID));
            Assert.Equal("2026-04-29T09:00:00.0000000", tasks[0].PlannedStart);
            Assert.Equal("2026-04-29T10:00:00.0000000", tasks[0].PlannedEnd);
            Assert.Equal(PlannerTaskCardKind.ScCard.ToString(), tasks[1].CardKind);

            var ev = Assert.Single(await context.GetEventRowsAsync(planner.PlannerId));
            Assert.Equal(PlannerEventKind.ScStepRep.ToString(), ev.EventKind);
            Assert.Equal(7, ev.ScCardStepID);
            Assert.Equal("2026-04-29T12:30:00.0000000", ev.PlannedTime);
            Assert.Equal(1, ev.PlannedCount);
        }

        [Fact]
        public async Task SavePlannerAsync_UpdatesExistingPlannerAndReplacesChildren()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = CreateService(context);
            var planner = new PlannerModel
            {
                PlannerDate = Local(2026, 4, 29),
                Tasks =
                {
                    new PlannerTaskModel
                    {
                        CardId = 10,
                        CardKind = PlannerTaskCardKind.TatCard,
                        PlannedStart = Local(2026, 4, 29, 9),
                        PlannedEnd = Local(2026, 4, 29, 10)
                    }
                }
            };

            await service.SavePlannerAsync(planner);
            var plannerId = planner.PlannerId;

            planner.Tasks.Clear();
            planner.Tasks.Add(new PlannerTaskModel
            {
                CardId = 30,
                CardKind = PlannerTaskCardKind.Mission,
                PlannedStart = Local(2026, 4, 29, 14),
                PlannedEnd = Local(2026, 4, 29, 15)
            });
            planner.Events.Add(new PlannerEventModel
            {
                EventKind = PlannerEventKind.MissionComplete,
                CardId = 30,
                PlannedTime = Local(2026, 4, 29, 15),
                PlannedCount = 2
            });

            await service.SavePlannerAsync(planner);

            Assert.Equal(plannerId, planner.PlannerId);
            Assert.Single(await context.GetPlannerRowsAsync());
            Assert.Equal(30, Assert.Single(await context.GetTaskRowsAsync(plannerId)).CardID);
            Assert.Equal(30, Assert.Single(await context.GetEventRowsAsync(plannerId)).CardID);
        }

        [Fact]
        public async Task SavePlannerAsync_RejectsOverlappingTasks()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = CreateService(context);
            var planner = new PlannerModel
            {
                PlannerDate = Local(2026, 4, 29),
                Tasks =
                {
                    new PlannerTaskModel
                    {
                        CardId = 10,
                        CardKind = PlannerTaskCardKind.TatCard,
                        PlannedStart = Local(2026, 4, 29, 9),
                        PlannedEnd = Local(2026, 4, 29, 10)
                    },
                    new PlannerTaskModel
                    {
                        CardId = 20,
                        CardKind = PlannerTaskCardKind.ScCard,
                        PlannedStart = Local(2026, 4, 29, 9, 30),
                        PlannedEnd = Local(2026, 4, 29, 10, 30)
                    }
                }
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SavePlannerAsync(planner));
        }

        [Fact]
        public async Task GetPlannerDayDataAsync_LoadsPlannerAndFiltersMissionCardsForPlannerDate()
        {
            await using var context = new TestSqliteConnectionContext();
            var scCard = new ScCardModel();
            var openMission = new MissionCardModel
            {
                CardID = 101,
                Title = "Open",
                CompletedDate = null,
                AvailableFromDate = Local(2026, 4, 20),
                DueDate = Local(2026, 5, 1)
            };
            var completedToday = new MissionCardModel
            {
                CardID = 102,
                Title = "Completed Today",
                CompletedDate = Local(2026, 4, 29, 12),
                AvailableFromDate = Local(2026, 4, 1),
                DueDate = Local(2026, 4, 20)
            };
            var overlapsToday = new MissionCardModel
            {
                CardID = 103,
                Title = "Overlaps",
                CompletedDate = Local(2026, 4, 20, 12),
                AvailableFromDate = Local(2026, 4, 28),
                DueDate = Local(2026, 4, 30)
            };
            var outsideToday = new MissionCardModel
            {
                CardID = 104,
                Title = "Outside",
                CompletedDate = Local(2026, 4, 20, 12),
                AvailableFromDate = Local(2026, 4, 1),
                DueDate = Local(2026, 4, 10)
            };
            var cards = new TestPlannerCardSource
            {
                MainQuestCards = { scCard },
                Missions = { openMission, completedToday, overlapsToday, outsideToday }
            };
            var service = CreateService(context, cards);
            await service.SavePlannerAsync(new PlannerModel
            {
                PlannerDate = Local(2026, 4, 29),
                Tasks =
                {
                    new PlannerTaskModel
                    {
                        CardId = 10,
                        CardKind = PlannerTaskCardKind.TatCard,
                        PlannedStart = Local(2026, 4, 29, 9),
                        PlannedEnd = Local(2026, 4, 29, 10)
                    }
                }
            });

            var data = await service.GetPlannerDayDataAsync(Local(2026, 4, 29, 18));

            Assert.NotNull(data.Planner);
            Assert.Equal(Local(2026, 4, 29), data.Planner!.PlannerDate);
            Assert.Equal(10, Assert.Single(data.Planner.Tasks).CardId);
            Assert.Same(scCard, Assert.Single(data.ScCards));
            Assert.Equal(new[] { 101L, 102L, 103L }, data.MissionCards.Select(mission => mission.CardID));
            Assert.DoesNotContain(data.MissionCards, mission => mission.CardID == 104);
            Assert.Equal(Local(2026, 4, 29), cards.LastMainQuestRangeStart);
            Assert.Equal(Local(2026, 4, 30), cards.LastMainQuestRangeEnd);
        }

        private static SqlitePlannerService CreateService(
            TestSqliteConnectionContext context,
            TestPlannerCardSource? cards = null)
        {
            return new SqlitePlannerService(
                context,
                cards ?? new TestPlannerCardSource(),
                new FixedZoneTimeZoneService(TimeZoneInfo.Utc),
                new FixedClock(new DateTime(2026, 4, 29, 10, 30, 0, DateTimeKind.Utc)));
        }

        private static DateTime Local(int year, int month, int day, int hour = 0, int minute = 0)
        {
            return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        }

        private sealed class TestPlannerCardSource : IPlannerCardSource
        {
            public List<IActiveCardModel> MainQuestCards { get; } = new();
            public List<MissionCardModel> Missions { get; } = new();
            public DateTime LastMainQuestRangeStart { get; private set; }
            public DateTime LastMainQuestRangeEnd { get; private set; }

            public Task<List<IActiveCardModel>> GetMainQuestModelsDataAsync(DateTime rangeStart, DateTime rangeEnd)
            {
                LastMainQuestRangeStart = rangeStart;
                LastMainQuestRangeEnd = rangeEnd;
                return Task.FromResult(MainQuestCards.ToList());
            }

            public Task<List<MissionCardModel>> GetMissionCardModelsDataAsync(string? whereClause = null)
            {
                return Task.FromResult(Missions.ToList());
            }
        }

        private sealed class FixedClock : IClock
        {
            public FixedClock(DateTime utcNow)
            {
                UtcNow = utcNow;
            }

            public DateTime UtcNow { get; }
            public DateTime LocalNow => DateTime.SpecifyKind(UtcNow, DateTimeKind.Unspecified);
            public DateTimeOffset UtcNowOffset => new(UtcNow);
        }

        private sealed class FixedZoneTimeZoneService : ITimeZoneService
        {
            private readonly TimeZoneService _inner = new();

            public FixedZoneTimeZoneService(TimeZoneInfo localTimeZone)
            {
                LocalTimeZone = localTimeZone;
            }

            public TimeZoneInfo LocalTimeZone { get; }

            public DateTime ToLocal(DateTime utcInstant)
            {
                return ToLocal(utcInstant, LocalTimeZone);
            }

            public DateTime ToLocal(DateTime utcInstant, TimeZoneInfo timeZone)
            {
                return _inner.ToLocal(utcInstant, timeZone);
            }

            public DateTime ToUtcFromLocal(
                DateTime localDateTime,
                TimeZoneInfo? timeZone = null,
                InvalidLocalTimeResolution invalidResolution = InvalidLocalTimeResolution.ShiftForward,
                AmbiguousLocalTimeResolution ambiguousResolution = AmbiguousLocalTimeResolution.EarlierInstant)
            {
                return _inner.ToUtcFromLocal(localDateTime, timeZone ?? LocalTimeZone, invalidResolution, ambiguousResolution);
            }

            public UtcDateTimeRange LocalRangeToUtc(
                DateTime localStartInclusive,
                DateTime localEndExclusive,
                TimeZoneInfo? timeZone = null,
                InvalidLocalTimeResolution invalidResolution = InvalidLocalTimeResolution.ShiftForward,
                AmbiguousLocalTimeResolution ambiguousResolution = AmbiguousLocalTimeResolution.EarlierInstant)
            {
                return _inner.LocalRangeToUtc(localStartInclusive, localEndExclusive, timeZone ?? LocalTimeZone, invalidResolution, ambiguousResolution);
            }

            public UtcDateTimeRange LocalDayRangeToUtc(
                DateTime localDate,
                TimeZoneInfo? timeZone = null,
                InvalidLocalTimeResolution invalidResolution = InvalidLocalTimeResolution.ShiftForward,
                AmbiguousLocalTimeResolution ambiguousResolution = AmbiguousLocalTimeResolution.EarlierInstant)
            {
                return _inner.LocalDayRangeToUtc(localDate, timeZone ?? LocalTimeZone, invalidResolution, ambiguousResolution);
            }

            public string SerializeUtc(DateTime utcInstant)
            {
                return _inner.SerializeUtc(utcInstant);
            }

            public DateTime ParseUtc(string value)
            {
                return _inner.ParseUtc(value);
            }

            public bool TryParseUtc(string? value, out DateTime utcInstant)
            {
                return _inner.TryParseUtc(value, out utcInstant);
            }

            public bool IsInvalidLocalTime(DateTime localDateTime, TimeZoneInfo? timeZone = null)
            {
                return _inner.IsInvalidLocalTime(localDateTime, timeZone ?? LocalTimeZone);
            }

            public bool IsAmbiguousLocalTime(DateTime localDateTime, TimeZoneInfo? timeZone = null)
            {
                return _inner.IsAmbiguousLocalTime(localDateTime, timeZone ?? LocalTimeZone);
            }
        }

        private sealed class TestSqliteConnectionContext : ISqliteConnectionContext, IAsyncDisposable
        {
            private SQLiteAsyncConnection? _db;

            public TestSqliteConnectionContext()
            {
                DatabasePath = Path.Combine(
                    Path.GetTempPath(),
                    $"PointsPlannerServiceTests-{Guid.NewGuid():N}.db");
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
                    CREATE TABLE IF NOT EXISTS Planner (
                        PlannerID INTEGER PRIMARY KEY AUTOINCREMENT,
                        PlannerDate TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL
                    );
                    """);
                await _db.ExecuteAsync("""
                    CREATE TABLE IF NOT EXISTS PlannerTask (
                        PlannerTaskID INTEGER PRIMARY KEY AUTOINCREMENT,
                        PlannerID INTEGER NOT NULL,
                        CardID INTEGER NOT NULL,
                        CardKind TEXT NOT NULL,
                        PlannedStart TEXT NOT NULL,
                        PlannedEnd TEXT NOT NULL
                    );
                    """);
                await _db.ExecuteAsync("""
                    CREATE TABLE IF NOT EXISTS PlannerEvent (
                        PlannerEventID INTEGER PRIMARY KEY AUTOINCREMENT,
                        PlannerID INTEGER NOT NULL,
                        EventKind TEXT NOT NULL,
                        CardID INTEGER NOT NULL,
                        ScCardStepID INTEGER NULL,
                        PlannedTime TEXT NOT NULL,
                        PlannedCount INTEGER NOT NULL
                    );
                    """);
            }

            public async Task<List<PlannerRow>> GetPlannerRowsAsync()
            {
                await InitializeAsync();
                return await Db.QueryAsync<PlannerRow>(
                    @"SELECT PlannerID, PlannerDate, CreatedAt, UpdatedAt
                      FROM Planner
                      ORDER BY PlannerID;");
            }

            public async Task<List<PlannerTaskRow>> GetTaskRowsAsync(long plannerId)
            {
                await InitializeAsync();
                return await Db.QueryAsync<PlannerTaskRow>(
                    @"SELECT PlannerTaskID, PlannerID, CardID, CardKind, PlannedStart, PlannedEnd
                      FROM PlannerTask
                      WHERE PlannerID = ?
                      ORDER BY PlannedStart;",
                    plannerId);
            }

            public async Task<List<PlannerEventRow>> GetEventRowsAsync(long plannerId)
            {
                await InitializeAsync();
                return await Db.QueryAsync<PlannerEventRow>(
                    @"SELECT PlannerEventID, PlannerID, EventKind, CardID, ScCardStepID, PlannedTime, PlannedCount
                      FROM PlannerEvent
                      WHERE PlannerID = ?
                      ORDER BY PlannedTime;",
                    plannerId);
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

                try
                {
                    if (File.Exists(DatabasePath))
                        File.Delete(DatabasePath);
                }
                catch
                {
                }
            }

            public sealed class PlannerRow
            {
                public long PlannerID { get; set; }
                public string PlannerDate { get; set; } = "";
                public string CreatedAt { get; set; } = "";
                public string UpdatedAt { get; set; } = "";
            }

            public sealed class PlannerTaskRow
            {
                public long PlannerTaskID { get; set; }
                public long PlannerID { get; set; }
                public long CardID { get; set; }
                public string CardKind { get; set; } = "";
                public string PlannedStart { get; set; } = "";
                public string PlannedEnd { get; set; } = "";
            }

            public sealed class PlannerEventRow
            {
                public long PlannerEventID { get; set; }
                public long PlannerID { get; set; }
                public string EventKind { get; set; } = "";
                public long CardID { get; set; }
                public int? ScCardStepID { get; set; }
                public string PlannedTime { get; set; } = "";
                public int PlannedCount { get; set; }
            }
        }
    }
}
