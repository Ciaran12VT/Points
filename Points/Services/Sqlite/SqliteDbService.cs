using CommunityToolkit.Maui.Core.Extensions;
using Points.Evaluators;
using Points.Global;
using Points.Models;
using Points.Models.DbModels;
using Points.Services;
using Points.Services.Sqlite.Interfaces;
using Points.ViewModels;
using SQLite;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

//Collapse all regiosn: Ctrl + M, L

namespace Points.Services.Sqlite
{
    public class SqliteDbService : IDbService
    {

        #region Initialisation

        private readonly string _dbPath;

        private SQLiteAsyncConnection? _db;
        public SQLiteAsyncConnection Db => _db ?? throw new InvalidOperationException("DB not initialized.");

        public SqliteDbService()
        {
            _dbPath = AppPaths.DatabasePath;
        }

        private readonly SemaphoreSlim _initSemaphore = new(1, 1);

        public async Task InitializeAsync()
        {
            if (_db != null) return;

            await _initSemaphore.WaitAsync();
            try
            {
                if (_db != null) return;

                // Ensures native SQLite is loaded correctly on mobile platforms.
                SQLitePCL.Batteries_V2.Init();

                _db = new SQLiteAsyncConnection(
                    _dbPath,
                    SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

                await _db.ExecuteAsync("PRAGMA foreign_keys = ON;");

                var script = SqlQueryService.GenerateDbCreationScript();
                var statements = script
                    .Split(';')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                await _db.RunInTransactionAsync(conn =>
                {
                    conn.Execute("PRAGMA foreign_keys = ON;");
                    foreach (var stmt in statements)
                    {
                        conn.Execute(stmt);
                    }
                });

                await EnsureGoalSchemaAsync();
                await EnsureAchievementCardSchemaAsync();
                await SaveBuiltInSettingDefinitionsAsync();
            }
            finally
            {
                _initSemaphore.Release();
            }
        }

        #endregion

        #region Backups and DB Maintenance

        public async Task WipeAsync()
        {
            if (_db == null)
                throw new InvalidOperationException("DB not initialized.");

            var script = SqlQueryService.GenerateDbWipeDataScript();

            // Split and filter, but also REMOVE any PRAGMA lines
            var statements = script
                .Split(';')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Where(s => !s.StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Toggle FK outside the transaction using ExecuteAsync (still fine here)
            // If this still ever trips your wrapper, we can remove toggling entirely.
            await _db.ExecuteAsync("PRAGMA foreign_keys = OFF;");

            try
            {
                await _db.RunInTransactionAsync(conn =>
                {
                    foreach (var stmt in statements)
                        conn.Execute(stmt);
                });
            }
            finally
            {
                await _db.ExecuteAsync("PRAGMA foreign_keys = ON;");
            }
        }

        public async Task CloseDatabaseAsync()
        {
            if (_db == null) return;

            try
            {
                await _db.CloseAsync();
            }
            catch
            {
                // Ignore – connection may already be closed
            }

            _db = null;
        }

        public async Task ReinitializeDatabaseAsync()
        {
            var dbFolder = Path.Combine(FileSystem.AppDataDirectory, "db");
            var dbPath = Path.Combine(dbFolder, "points.db3");

            _db = new SQLiteAsyncConnection(dbPath,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache
            );

            await _db.ExecuteAsync("PRAGMA foreign_keys = ON;");
        }

        private async Task EnsureGoalSchemaAsync()
        {
            if (_db == null)
                throw new InvalidOperationException("Database must be initialized before schema migration.");

            await MigrateGoalTableAsync();
            await RenameColumnIfNeededAsync("AchievementCard", "GoalType", "TargetType");
            await RenameColumnIfNeededAsync("LockTaskDependency", "GoalValue", "TargetValue");
            await RenameColumnIfNeededAsync("LockTaskDependency", "GoalValence", "TargetValence");
            await MigrateSettingKeyAsync("PlannersActive", "GoalsActive");
            await MigrateSettingKeyAsync("PlannersScreenOrder", "GoalsScreenOrder");
        }

        private async Task MigrateGoalTableAsync()
        {
            if (!await TableExistsAsync("PlannerGoal"))
                return;

            await Db.ExecuteAsync(@"
                INSERT OR IGNORE INTO Goal
                    (GoalID, CardID, TimeScope, GoalHrs, Enabled, DeFactoStart, DeFactoEnd)
                SELECT
                    PlannerGoalID, CardID, TimeScope, GoalHrs, Enabled, DeFactoStart, DeFactoEnd
                FROM PlannerGoal;
            ");

            await Db.ExecuteAsync("DROP TABLE PlannerGoal;");
            await Db.ExecuteAsync("DROP INDEX IF EXISTS IX_PlannerGoal_CardID;");
            await Db.ExecuteAsync("DROP INDEX IF EXISTS IX_PlannerGoal_Enabled;");
            await Db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Goal_CardID ON Goal(CardID);");
            await Db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Goal_Enabled ON Goal(Enabled);");
        }

        private async Task RenameColumnIfNeededAsync(string tableName, string oldColumnName, string newColumnName)
        {
            var cols = await Db.QueryAsync<PragmaTableInfo>($"PRAGMA table_info({tableName});");
            var existing = cols
                .Select(c => c.name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!existing.Contains(oldColumnName) || existing.Contains(newColumnName))
                return;

            await Db.ExecuteAsync($"ALTER TABLE {tableName} RENAME COLUMN {oldColumnName} TO {newColumnName};");
        }

        private async Task<bool> TableExistsAsync(string tableName)
        {
            var count = await Db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?;",
                tableName);

            return count > 0;
        }

        private async Task MigrateSettingKeyAsync(string oldKey, string newKey)
        {
            var oldExists = await Db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Setting WHERE SettingKey = ?;",
                oldKey);

            if (oldExists == 0)
                return;

            var newExists = await Db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Setting WHERE SettingKey = ?;",
                newKey);

            if (newExists == 0)
            {
                await Db.ExecuteAsync(
                    "UPDATE Setting SET SettingKey = ? WHERE SettingKey = ?;",
                    newKey,
                    oldKey);
                return;
            }

            var oldValue = await Db.ExecuteScalarAsync<string?>(
                "SELECT SettingValue FROM Setting WHERE SettingKey = ?;",
                oldKey);

            await Db.ExecuteAsync(
                "UPDATE Setting SET SettingValue = ? WHERE SettingKey = ?;",
                oldValue ?? "",
                newKey);

            await Db.ExecuteAsync("DELETE FROM Setting WHERE SettingKey = ?;", oldKey);
        }

        private async Task EnsureAchievementCardSchemaAsync()
        {
            if (_db == null)
                throw new InvalidOperationException("Database must be initialized before schema migration.");

            var cols = await Db.QueryAsync<PragmaTableInfo>("PRAGMA table_info(AchievementCard);");
            var existing = cols
                .Select(c => c.name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var alterStatements = new List<string>();

            if (!existing.Contains("DeadlineStart"))
                alterStatements.Add("ALTER TABLE AchievementCard ADD COLUMN DeadlineStart TEXT NULL;");

            if (!existing.Contains("FinalizedAt"))
                alterStatements.Add("ALTER TABLE AchievementCard ADD COLUMN FinalizedAt TEXT NULL;");

            if (!existing.Contains("FrozenCurrentValue"))
                alterStatements.Add("ALTER TABLE AchievementCard ADD COLUMN FrozenCurrentValue REAL NULL;");

            foreach (var sql in alterStatements)
            {
                await Db.ExecuteAsync(sql);
            }
        }


        #endregion

        #region Read

        #region Home Seed

        public async Task<HomeSeedData> GetHomeSeedDataAsync(DateTime rangeStart, DateTime rangeEnd)
        {
            var mainQuest = await GetMainQuestModelsDataAsync(rangeStart, rangeEnd);

            var mission = await GetMissionCardModelsDataAsync("m.CompletedDate IS NULL OR m.CompletedDate >= datetime('now', 'localtime', 'start of day')");

            var budget = await GetBudgetCardModelsDataAsync();

            var achievements = await GetAchievementCardModelsDataAsync();

            await PopulateAchievements(achievements, mainQuest, mission);

            await PopulateLocks(mainQuest, mission);

            var valueTrackers = await GetValueTrackerCardModelsDataAsync();

            var eventTrackers = await GetEventTrackerCardModelsDataAsync();

            var seed = new HomeSeedData
            {
                MainQuestCards = mainQuest,
                MissionCards = mission,
                BudgetCards = budget,
                Achievements = achievements,
                ValueTrackers = valueTrackers,
                EventTrackers = eventTrackers
            };

            return seed;
        }

        #endregion

        #region Achievements

        public async Task<AchievementCardModel> GetAchievementCardModelDataAsync(int id)
        {
            await InitializeAsync();

            const string sql = @"
                SELECT
                    a.AchievementCardID         AS AchievementCardID,
                    a.CardID                    AS CardID,

                    c.Title                     AS Title,
                    c.Tags                      AS Tags,

                    a.Status                    AS Status,
                    a.Description               AS Description,
                    a.TargetType                  AS TargetType,
                    a.DifficultyLevel           AS DifficultyLevel,

                    a.CreatedDate               AS CreatedDate,
                    a.LastEarnedAt              AS LastEarnedAt,

                    a.TargetActiveTimeInSeconds AS TargetActiveTimeInSeconds,
                    a.TargetValue               AS TargetValue,
                    a.ScCardStepID              AS ScCardStepID,

                    a.CompletionType            AS CompletionType,
                    a.RangeUnit                 AS RangeUnit,
                    a.RangeAmount               AS RangeAmount,
                    a.DeadlineStart             AS DeadlineStart,
                    a.Deadline                  AS Deadline,

                    a.FinalizedAt               AS FinalizedAt,
                    a.FrozenCurrentValue        AS FrozenCurrentValue,

                    a.TrophyURLs                AS TrophyURLs,
                    a.IsPinned                  AS IsPinned
                FROM AchievementCard a
                JOIN Card c ON c.CardID = a.CardID
                WHERE a.AchievementCardID = ?
                LIMIT 1;";

            var row = (await Db.QueryAsync<AchievementCardJoinedRow>(sql, id)).FirstOrDefault();
            if (row == null)
                throw new KeyNotFoundException($"AchievementCard not found. AchievementCardID={id}");

            var model = MapAchievementRowToModel(row);
            model = await FinalizeDeadlineAchievementIfNeededAsync(model);

            return model;
        }

        public async Task<List<AchievementCardModel>> GetAchievementCardModelsDataAsync()
        {
            await InitializeAsync();

            var sql = @"
                SELECT
                    a.AchievementCardID         AS AchievementCardID,
                    a.CardID                    AS CardID,

                    c.Title                     AS Title,
                    c.Tags                      AS Tags,

                    a.Status                    AS Status,
                    a.Description               AS Description,
                    a.TargetType                  AS TargetType,
                    a.DifficultyLevel           AS DifficultyLevel,

                    a.CreatedDate               AS CreatedDate,
                    a.LastEarnedAt              AS LastEarnedAt,

                    a.TargetActiveTimeInSeconds AS TargetActiveTimeInSeconds,
                    a.TargetValue               AS TargetValue,
                    a.ScCardStepID              AS ScCardStepID,

                    a.CompletionType            AS CompletionType,
                    a.RangeUnit                 AS RangeUnit,
                    a.RangeAmount               AS RangeAmount,
                    a.DeadlineStart             AS DeadlineStart,
                    a.Deadline                  AS Deadline,

                    a.FinalizedAt               AS FinalizedAt,
                    a.FrozenCurrentValue        AS FrozenCurrentValue,

                    a.TrophyURLs                AS TrophyURLs,
                    a.IsPinned                  AS IsPinned
                FROM AchievementCard a
                JOIN Card c ON c.CardID = a.CardID
            ";


            //Debug: check the table definition to ensure new columns are present
            var pragma = await Db.QueryAsync<PragmaTableInfo>("PRAGMA table_info(AchievementCard);");

            var rows = await Db.QueryAsync<AchievementCardJoinedRow>(sql);
            if (rows.Count == 0)
                return new List<AchievementCardModel>();

            var models = rows.Select(MapAchievementRowToModel).ToList();

            // Allow load-time transitions such as:
            // - instant completion if already over target
            // - failure if deadline has elapsed without success
            models = await FinalizeDeadlineAchievementsIfNeededAsync(models);

            var now = DateTime.Now;

            // Keep:
            // - all non-deadline achievements
            // - all non-finalized deadline achievements
            // - deadline achievements finalized today
            models = models
                .Where(x => ShouldKeepLoadedAfterFinalization(x, now))
                .ToList();

            return models;
        }

        private AchievementCardModel MapAchievementRowToModel(AchievementCardJoinedRow row)
        {
            // Parse enums with safe fallbacks
            var difficulty = AchievementDifficultyLevels.Easy;
            if (!string.IsNullOrWhiteSpace(row.DifficultyLevel))
                Enum.TryParse(row.DifficultyLevel, out difficulty);

            var targetType = AchievementTargetType.ActiveTime;
            if (!string.IsNullOrWhiteSpace(row.TargetType))
                Enum.TryParse(row.TargetType, out targetType);

            var completionType = AchievementCompletionType.Range;
            if (!string.IsNullOrWhiteSpace(row.CompletionType))
                Enum.TryParse(row.CompletionType, out completionType);

            var rangeUnit = AchievementRangeUnit.Days;
            if (!string.IsNullOrWhiteSpace(row.RangeUnit))
                Enum.TryParse(row.RangeUnit, out rangeUnit);

            var model = new AchievementCardModel
            {
                Id = row.AchievementCardID,
                CardID = row.CardID,

                Title = row.Title ?? "",
                Tags = row.Tags ?? "",

                Status = row.Status ?? "",
                Description = row.Description ?? "",

                Difficulty = difficulty,
                TargetType = targetType,
                CompletionType = completionType,
                RangeUnit = rangeUnit,

                CreatedDate = !string.IsNullOrWhiteSpace(row.CreatedDate)
                    ? ParseIsoDateTime(row.CreatedDate)
                    : DateTime.Now,

                RangeAmount = row.RangeAmount ?? 0,
                TargetValue = row.TargetValue ?? 0,

                FrozenCurrentValue = row.FrozenCurrentValue,

                IsPinned = row.IsPinned == 1
            };

            // Active time target (seconds -> "hh:mm:ss" style string)
            if (row.TargetActiveTimeInSeconds.HasValue && row.TargetActiveTimeInSeconds.Value > 0)
            {
                var ts = TimeSpan.FromSeconds(row.TargetActiveTimeInSeconds.Value);
                // Hours can exceed 24; we want total hours
                var hours = (int)ts.TotalHours;
                model.ActiveTimeTargetText = $"{hours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            }

            // Deadline
            if (!string.IsNullOrWhiteSpace(row.CreatedDate))
                model.CreatedDate = ParseIsoDateTime(row.CreatedDate);

            if (!string.IsNullOrWhiteSpace(row.DeadlineStart))
                model.DeadlineStart = ParseIsoDateTime(row.DeadlineStart);

            if (!string.IsNullOrWhiteSpace(row.Deadline))
                model.Deadline = ParseIsoDateTime(row.Deadline);

            // Last earned
            if (!string.IsNullOrWhiteSpace(row.LastEarnedAt))
                model.LastEarnedAt = ParseIsoDateTime(row.LastEarnedAt);

            if (!string.IsNullOrWhiteSpace(row.FinalizedAt))
                model.FinalizedAt = ParseIsoDateTime(row.FinalizedAt);

            // Trophies from newline-separated TrophyURLs
            if (!string.IsNullOrWhiteSpace(row.TrophyURLs))
            {
                foreach (var t in row.TrophyURLs
                                     .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    model.Trophies.Add(t);
                }
            }

            return model;
        }

        // Internal DTO for sqlite-net mapping
        private sealed class AchievementCardJoinedRow
        {
            public int AchievementCardID { get; set; }
            public long CardID { get; set; }

            public string? Title { get; set; }
            public string? Tags { get; set; }

            public string? Status { get; set; }
            public string? Description { get; set; }

            public string? TargetType { get; set; }
            public string? DifficultyLevel { get; set; }

            public string CreatedDate { get; set; } = "";
            public string? LastEarnedAt { get; set; }

            public int? TargetActiveTimeInSeconds { get; set; }
            public double? TargetValue { get; set; }
            public int? ScCardStepID { get; set; }

            public string? CompletionType { get; set; }
            public string? RangeUnit { get; set; }
            public int? RangeAmount { get; set; }

            public string? DeadlineStart { get; set; }
            public string? Deadline { get; set; }

            public string? FinalizedAt { get; set; }
            public double? FrozenCurrentValue { get; set; }

            public string? TrophyURLs { get; set; }
            public int IsPinned { get; set; }

        }

        public async Task<List<TimeValueAchievementEvaluator>> RefreshEvaluatorsAsync(List<TimeValueAchievementEvaluator> evaluators)
        {
            await InitializeAsync();

            if (evaluators == null)
                return new List<TimeValueAchievementEvaluator>();

            var input = evaluators.ToList();
            if (input.Count == 0)
                return new List<TimeValueAchievementEvaluator>();

            var refreshed = new List<TimeValueAchievementEvaluator>(input.Count);

            foreach (var evaluator in input)
            {
                // Preserve tag grouping
                var newEvaluator = new TimeValueAchievementEvaluator
                {
                    Evaluations = new List<TimeValueAchievementEvaluation>()
                };

                // If nothing to refresh, keep empty
                if (evaluator.Evaluations == null || evaluator.Evaluations.Count == 0)
                {
                    refreshed.Add(newEvaluator);
                    continue;
                }

                // Achievements we need to recompute (distinct by Id to be safe)
                var achievementIds = evaluator.Evaluations
                    .Where(e => e?.AchievementCard != null)
                    .Select(e => e!.AchievementCard.Id)
                    .Distinct()
                    .ToList();

                // Re-pull the latest AchievementCardModels from DB (critical for LastEarnedAt / cooldown correctness)
                // Simple approach: fetch one-by-one (small set; cold-start only).
                // If you later want, you can batch this with a WHERE IN query.
                var tasks = achievementIds.Select(async id =>
                {
                    var ach = await GetAchievementCardModelDataAsync(id);
                    return await CreateEvaluation(ach);
                });

                var evaluations = await Task.WhenAll(tasks);

                newEvaluator.Evaluations = evaluations.ToList();
                refreshed.Add(newEvaluator);
            }

            return refreshed;
        }


        public async Task<List<TrophyModel>> GetTrophyModelsDataAsync()
        {
            await InitializeAsync();

            const string sql = @"
                SELECT
                    TrophyID      AS Id,
                    AchievementCardID As AchievementId,
                    Title,
                    EarnedOn,
                    ImageSource
                FROM AchievementTrophy
                ORDER BY datetime(EarnedOn) DESC;
            ";

            var rows = await Db.QueryAsync<TrophyRow>(sql);

            return rows.Select(r => new TrophyModel
            {
                Id = r.Id,
                AchievementId = r.AchievementId,
                Title = r.Title ?? string.Empty,
                EarnedOn = DateTime.Parse(
                    r.EarnedOn,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind
                ),
                ImageSource = string.IsNullOrWhiteSpace(r.ImageSource)
                    ? "trophy.png"
                    : r.ImageSource
            }).ToList();
        }

        private sealed class TrophyRow
        {
            public int Id { get; set; }
            public int AchievementId { get; set; }
            public string Title { get; set; } = "";
            public string EarnedOn { get; set; } = "";
            public string ImageSource { get; set; } = "";
        }

        #region Evaluators

        private async Task PopulateAchievements(List<AchievementCardModel> achievements, List<IActiveCardModel> mainQuest, List<MissionCardModel> mission)
        {
            Dictionary<string, TimeValueAchievementEvaluator> byTag = await BuildEvaluatorsByTag(achievements);
            foreach (var mq in mainQuest)
            {
                var tags = mq.Tags.Split(',').Select(x => x.Trim());
                var evals = byTag.Where(x => tags.Contains(x.Key)).Select(y => y.Value).ToList();
                mq.TimeValueAchievementEvaluators = evals;
            }

            foreach (var ach in achievements)
            {
                if (byTag.TryGetValue(ach.Tags, out TimeValueAchievementEvaluator evaltr))
                {
                    var relevantEvaluations = evaltr.Evaluations?
                        .Where(x => x?.AchievementCard != null && x.AchievementCard.Id == ach.Id)
                        .ToList()
                        ?? new List<TimeValueAchievementEvaluation>();

                    if (ach.TargetType == AchievementTargetType.Value)
                    {
                        ach.CurrentValue = relevantEvaluations.Sum(x => x.CurrentValue);
                    }
                    else if (ach.TargetType == AchievementTargetType.ActiveTime)
                    {
                        ach.CurrentValue = relevantEvaluations.Sum(x => x.CurrentValue);
                    }
                }
                else
                {
                    ach.CurrentValue = 0;
                }

                ach.NotifyTimeChanged();
            }
        }


        private async Task<Dictionary<string, TimeValueAchievementEvaluator>> BuildEvaluatorsByTag(IEnumerable<AchievementCardModel> cards)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));

            var result = new Dictionary<string, TimeValueAchievementEvaluator>();

            foreach (var group in cards.GroupBy(c => c.Tags ?? string.Empty))
            {
                var evaluationTasks = group.Select(card => CreateEvaluation(card)); // Tasks

                var evaluations = await Task.WhenAll(evaluationTasks); // Await all

                result[group.Key] = new TimeValueAchievementEvaluator
                {
                    Evaluations = evaluations.ToList()
                };
            }

            return result;
        }

        private async Task<TimeValueAchievementEvaluation> CreateEvaluation(AchievementCardModel card)
        {
            var now = DateTime.Now;

            // Finalized deadline achievements are inert/frozen.
            if (card.CompletionType == AchievementCompletionType.Deadline &&
                card.FinalizedAt.HasValue)
            {
                return new TimeValueAchievementEvaluation
                {
                    AchievementCard = card,
                    CurrentValue = card.FrozenCurrentValue ?? 0
                };
            }

            if (!card.TryGetEvaluationWindow(now, out var windowStart, out var windowEnd))
            {
                return new TimeValueAchievementEvaluation
                {
                    AchievementCard = card,
                    CurrentValue = 0
                };
            }

            var summary = await GetTagValueSummaryAsync(card.Tags, windowStart, windowEnd);

            return card.TargetType switch
            {
                AchievementTargetType.ActiveTime => new TimeValueAchievementEvaluation
                {
                    AchievementCard = card,
                    CurrentValue = summary.CurrentTotalActiveTimeInSeconds
                },
                AchievementTargetType.Value => new TimeValueAchievementEvaluation
                {
                    AchievementCard = card,
                    CurrentValue = summary.CurrentValue
                },
                _ => throw new NotSupportedException(
                    $"Unsupported TargetType '{card.TargetType}' for AchievementCard '{card}'.")
            };
        }

        public sealed class TagValueSummaryRow
        {
            public double CurrentValue { get; set; }
            public double CurrentTotalActiveTimeInSeconds { get; set; }
        }

        public async Task<TagValueSummaryRow> GetTagValueSummaryAsync(string tagName, DateTime rangeStart, DateTime rangeEnd)
        {
            await InitializeAsync();

            // Convert to ISO-8601 to match how you store datetimes
            var startIso = rangeStart.ToString("o");
            var endIso = rangeEnd.ToString("o");

            const string sql = @"
                    WITH TaggedCards AS (
                        SELECT c.CardID
                        FROM Card c
                        WHERE ',' || REPLACE(c.Tags, ' ', '') || ',' 
                              LIKE '%,' || REPLACE(?, ' ', '') || ',%'
                    ),

                    TimeValued AS (
                        SELECT
                            SUM(
                                ((julianday(a.""End"") - julianday(a.Start)) * 24.0 * 60.0)
                                * a.ValuePerMinute
                            ) AS Value,
                            SUM(
                                (julianday(a.""End"") - julianday(a.Start)) * 86400.0
                            ) AS TotalActiveSeconds
                        FROM Activity a
                        WHERE a.CardID IN (SELECT CardID FROM TaggedCards)
                          AND datetime(a.Start) >= datetime(?)
                          AND datetime(a.""End"")   <= datetime(?)  
                          AND a.""End"" >= a.Start
                    ),

                    StepValued AS (
                        SELECT
                            SUM(rep.StepValue) AS Value
                        FROM ScCard sc
                        JOIN TaggedCards tc     ON tc.CardID      = sc.CardID
                        JOIN ScCardStep st      ON st.ScCardID    = sc.ScCardID
                        JOIN ScCardStepRep rep  ON rep.ScCardStepID = st.ScCardStepID
                        WHERE datetime(rep.TimeStamp) >= datetime(?)
                          AND datetime(rep.TimeStamp) <= datetime(?)
                    ),

                    MissionValued AS (
                        SELECT
                            SUM(mc.Value) AS Value
                        FROM MissionCard mc
                        JOIN TaggedCards tc ON tc.CardID = mc.CardID
                        WHERE datetime(mc.CompletedDate) >= datetime(?)
                          AND datetime(mc.CompletedDate) <= datetime(?)
                    )

                    SELECT
                        COALESCE(TimeValued.Value, 0)
                      + COALESCE(StepValued.Value, 0)
                      + COALESCE(MissionValued.Value, 0) AS CurrentValue,

                        COALESCE(TimeValued.TotalActiveSeconds, 0) AS CurrentTotalActiveTimeInSeconds
                    FROM TimeValued
                    CROSS JOIN StepValued
                    CROSS JOIN MissionValued;
                ";

            // Parameter order:
            //  1: tagName
            //  2: rangeStart (TimeValued)
            //  3: rangeEnd   (TimeValued)
            //  4: rangeStart (StepValued)
            //  5: rangeEnd   (StepValued)
            //  6: rangeStart (MissionValued)
            //  7: rangeEnd   (MissionValued)
            var rows = await Db.QueryAsync<TagValueSummaryRow>(
                sql,
                tagName,
                startIso, endIso,
                startIso, endIso,
                startIso, endIso
            );

            var row = rows.FirstOrDefault() ?? new TagValueSummaryRow();

            // Named tuple elements so the caller can use:
            // result.CurrentValue and result.CurrentTotalActiveTimeInSeconds
            return row;
        }

        public async Task<AchievementCardModel> ReevaluateDeadlineAchievementAsync(AchievementCardModel card)
        {
            if (card == null)
                throw new ArgumentNullException(nameof(card));

            return await FinalizeDeadlineAchievementIfNeededAsync(card);
        }

        #endregion

        #endregion

        #region Budget

        // =======================
        // Budget (READ)
        // =======================

        public async Task<BudgetCardModel> GetBudgetCardModelDataAsync(int id)
        {
            await InitializeAsync();

            // 1) Fetch BudgetCard + base Card
            const string sql = @"
                SELECT
                    b.BudgetCardID     AS BudgetCardID,
                    b.CardID           AS CardID,

                    c.Title            AS Title,
                    c.Tags             AS Tags,

                    b.Status           AS Status,
                    b.Description      AS Description,
                    b.Currency         AS Currency,
                    b.ExchangeRate     AS ExchangeRate,
                    b.StartDate        AS StartDate,
                    b.InitialBalance   AS InitialBalance
                FROM BudgetCard b
                JOIN Card c ON c.CardID = b.CardID
                WHERE b.BudgetCardID = ?
                LIMIT 1;
            ";

            var row = (await Db.QueryAsync<BudgetCardJoinedRow>(sql, id)).FirstOrDefault();
            if (row == null)
                throw new KeyNotFoundException($"BudgetCard not found. BudgetCardID={id}");

            var model = new BudgetCardModel
            {
                Id = row.BudgetCardID,
                CardID = row.CardID,

                Title = row.Title ?? "",
                Tags = row.Tags ?? "",

                Status = row.Status ?? "",
                Description = row.Description ?? "",

                Currency = row.Currency ?? "",
                ExchangeRate = row.ExchangeRate,
                StartDate = ParseIsoDateTime(row.StartDate),
                InitialBalance = row.InitialBalance,

                // Make sure these exist even if empty
                TopUps = new ObservableCollection<ScheduledTopUp>(),
                Transactions = new ObservableCollection<BudgetTransaction>()
            };

            // 2) Load scheduled top-ups
            const string topupsSql = @"
                SELECT
                    BudgetCardScheduledTopUpID AS BudgetCardScheduledTopUpID,
                    BudgetCardID               AS BudgetCardID,
                    Amount                     AS Amount,
                    TimeOfDaySeconds           AS TimeOfDaySeconds
                FROM BudgetCardScheduledTopUp
                WHERE BudgetCardID = ?
                ORDER BY BudgetCardScheduledTopUpID;
            ";

            var topupRows = await Db.QueryAsync<BudgetTopUpRow>(topupsSql, row.BudgetCardID);

            model.TopUps = topupRows.Select(t => new ScheduledTopUp
            {
                Id = t.BudgetCardScheduledTopUpID,
                Amount = t.Amount,
                TimeOfDay = TimeSpan.FromSeconds(t.TimeOfDaySeconds)
            }).ToObservableCollection();

            // 3) Load transactions
            const string transSql = @"
                SELECT
                    BudgetCardTransactionID AS BudgetCardTransactionID,
                    BudgetCardID            AS BudgetCardID,
                    Amount                  AS Amount,
                    Type                    AS Type,
                    TimeStamp               AS TimeStamp
                FROM BudgetCardTransaction
                WHERE BudgetCardID = ?
                ORDER BY TimeStamp;
            ";

            var transRows = await Db.QueryAsync<BudgetTransactionRow>(transSql, row.BudgetCardID);

            model.Transactions = transRows.Select(t => new BudgetTransaction
            {
                Id = t.BudgetCardTransactionID,
                CurrencyAmount = t.Amount,
                Type = BudgetTransactionTypeParse(t.Type),
                Timestamp = ParseIsoDateTime(t.TimeStamp)
            }).ToObservableCollection();

            return model;
        }

        private BudgetTransactionType BudgetTransactionTypeParse(string? type)
        {
            if (Enum.TryParse<BudgetTransactionType>(type ?? "", true, out BudgetTransactionType btt))
            {
                return btt;
            }

            return BudgetTransactionType.Spend;
        }

        public async Task<List<BudgetCardModel>> GetBudgetCardModelsDataAsync(string whereClause = null)
        {
            await InitializeAsync();

            // 1) Fetch all BudgetCards + base Card
            var sql = @"
                SELECT
                    b.BudgetCardID     AS BudgetCardID,
                    b.CardID           AS CardID,

                    c.Title            AS Title,
                    c.Tags             AS Tags,

                    b.Status           AS Status,
                    b.Description      AS Description,
                    b.Currency         AS Currency,
                    b.ExchangeRate     AS ExchangeRate,
                    b.StartDate        AS StartDate,
                    b.InitialBalance   AS InitialBalance
                FROM BudgetCard b
                JOIN Card c ON c.CardID = b.CardID
            ";

            if (!string.IsNullOrWhiteSpace(whereClause))
            {
                var wc = whereClause.Trim();

                if (wc.StartsWith("WHERE", StringComparison.OrdinalIgnoreCase) ||
                    wc.StartsWith("ORDER BY", StringComparison.OrdinalIgnoreCase) ||
                    wc.StartsWith("LIMIT", StringComparison.OrdinalIgnoreCase))
                {
                    sql += " " + wc;
                }
                else
                {
                    sql += " WHERE " + wc;
                }
            }

            var rows = await Db.QueryAsync<BudgetCardJoinedRow>(sql);
            if (rows.Count == 0) return new List<BudgetCardModel>();

            // 2) Materialize models (no children yet)
            var models = rows.Select(r => new BudgetCardModel
            {
                Id = r.BudgetCardID,
                CardID = r.CardID,

                Title = r.Title ?? "",
                Tags = r.Tags ?? "",

                Status = r.Status ?? "",
                Description = r.Description ?? "",

                Currency = r.Currency ?? "",
                ExchangeRate = r.ExchangeRate,
                StartDate = ParseIsoDateTime(r.StartDate),
                InitialBalance = r.InitialBalance,

                TopUps = new ObservableCollection<ScheduledTopUp>(),
                Transactions = new ObservableCollection<BudgetTransaction>()
            }).ToList();

            var byBudgetId = models.ToDictionary(m => m.Id);

            // 3) Bulk-load all top-ups for these budgets
            var budgetIds = rows.Select(r => r.BudgetCardID).Distinct().ToList();

            if (budgetIds.Count > 0)
            {
                var placeholders = string.Join(", ", budgetIds.Select(_ => "?"));

                var topupsSql = $@"
                        SELECT
                            BudgetCardScheduledTopUpID AS BudgetCardScheduledTopUpID,
                            BudgetCardID               AS BudgetCardID,
                            Amount                     AS Amount,
                            TimeOfDaySeconds           AS TimeOfDaySeconds
                        FROM BudgetCardScheduledTopUp
                        WHERE BudgetCardID IN ({placeholders})
                        ORDER BY BudgetCardID, BudgetCardScheduledTopUpID;
                    ";

                var topupRows = await Db.QueryAsync<BudgetTopUpRow>(topupsSql, budgetIds.Cast<object>().ToArray());

                foreach (var t in topupRows)
                {
                    if (!byBudgetId.TryGetValue(t.BudgetCardID, out var parent))
                        continue;

                    parent.TopUps.Add(new ScheduledTopUp
                    {
                        Id = t.BudgetCardScheduledTopUpID,
                        Amount = t.Amount,
                        TimeOfDay = TimeSpan.FromSeconds(t.TimeOfDaySeconds)
                    });
                }

                // 4) Bulk-load all transactions for these budgets
                var transSql = $@"
                    SELECT
                        BudgetCardTransactionID AS BudgetCardTransactionID,
                        BudgetCardID            AS BudgetCardID,
                        Amount                  AS Amount,
                        Type                    AS Type,
                        TimeStamp               AS TimeStamp
                    FROM BudgetCardTransaction
                    WHERE BudgetCardID IN ({placeholders})
                    ORDER BY BudgetCardID, TimeStamp;
                ";

                var transRows = await Db.QueryAsync<BudgetTransactionRow>(transSql, budgetIds.Cast<object>().ToArray());

                foreach (var t in transRows)
                {
                    if (!byBudgetId.TryGetValue(t.BudgetCardID, out var parent))
                        continue;

                    parent.Transactions.Add(new BudgetTransaction
                    {
                        Id = t.BudgetCardTransactionID,
                        CurrencyAmount = t.Amount,
                        Type = BudgetTransactionTypeParse(t.Type),
                        Timestamp = ParseIsoDateTime(t.TimeStamp)
                    });
                }
            }

            return models;
        }

        // -------------------------
        // Internal DTOs for sqlite-net mapping
        // -------------------------
        private sealed class BudgetCardJoinedRow
        {
            public int BudgetCardID { get; set; }
            public long CardID { get; set; }

            public string? Title { get; set; }
            public string? Tags { get; set; }

            public string? Status { get; set; }
            public string? Description { get; set; }

            public string? Currency { get; set; }
            public double ExchangeRate { get; set; }

            // Stored as TEXT (ISO-8601)
            public string StartDate { get; set; } = "";

            public double InitialBalance { get; set; }
        }

        private sealed class BudgetTopUpRow
        {
            public int BudgetCardScheduledTopUpID { get; set; }
            public int BudgetCardID { get; set; }

            public double Amount { get; set; }
            public double TimeOfDaySeconds { get; set; }
        }

        private sealed class BudgetTransactionRow
        {
            public int BudgetCardTransactionID { get; set; }
            public int BudgetCardID { get; set; }

            public double Amount { get; set; }
            public string? Type { get; set; }

            // Stored as TEXT (ISO-8601)
            public string TimeStamp { get; set; } = "";
        }

        #endregion

        #region Main Quest

        //Main Quest
        public async Task<List<IActiveCardModel>> GetMainQuestModelsDataAsync(DateTime rangeStart, DateTime rangeEnd)
        {
            var tats = await GetTatModelsDataAsync(rangeStart, rangeEnd);
            var scs = await GetScModelsDataAsync(rangeStart, rangeEnd);

            var mainQuest = new List<IActiveCardModel>();
            mainQuest.AddRange(tats);
            mainQuest.AddRange(scs);

            return mainQuest;
        }

        #region SC Cards

        //SC
        public async Task<ScCardModel> GetScModelDataAsync(int id)
        {
            // 1) Fetch the ScCard + base Card in one go
            const string sql = @"
                SELECT
                    s.ScCardID     AS ScCardID,
                    s.CardID       AS CardID,

                    c.Title        AS Title,
                    c.Tags         AS Tags,

                    s.Status       AS Status,
                    s.Description  AS Description
                FROM ScCard s
                JOIN Card c ON c.CardID = s.CardID
                WHERE s.ScCardID = ?
                LIMIT 1;
            ";



            var row = (await Db.QueryAsync<ScCardJoinedRow>(sql, id)).FirstOrDefault();
            if (row == null)
                throw new KeyNotFoundException($"ScCard not found. ScCardID={id}");

            // 2) Materialize the model
            var model = new ScCardModel
            {
                Id = row.ScCardID,
                CardID = row.CardID,

                Title = row.Title ?? "",
                Tags = row.Tags ?? "",

                Status = row.Status ?? "",
                Description = row.Description ?? "",
            };

            // 2.5) Load activity by CardID (same pattern as TAT)
            const string actSql = @"
                SELECT
                    ActivityID       AS ActivityID,
                    CardID           AS CardID,
                    Start            AS Start,
                    ""End""          AS End,
                    ValueRateName    AS ValueRateName,
                    ValuePerMinute   AS ValuePerMinute
                FROM Activity
                WHERE CardID = ?
                ORDER BY Start;
            ";

            var actRows = await Db.QueryAsync<ActivityRow>(actSql, row.CardID);

            model.Activity = actRows
                .Select(a => ActivityMapper.ToModel(a, ParseIsoDateTime))
                .ToList();


            // 3) Load steps
            const string stepsSql = @"
                SELECT
                    ScCardStepID AS ScCardStepID,
                    ScCardID     AS ScCardID,
                    SortOrder    AS SortOrder,
                    Title        AS Title,
                    StepValue    AS StepValue
                FROM ScCardStep
                WHERE ScCardID = ?
                ORDER BY SortOrder;
            ";

            var stepRows = await Db.QueryAsync<ScCardStepRow>(stepsSql, row.ScCardID);

            // 4) Load reps for each step
            const string repsSql = @"
                SELECT
                    ScCardStepID AS ScCardStepID,
                    TimeStamp    AS TimeStamp,
                    StepValue    AS StepValue
                FROM ScCardStepRep
                WHERE ScCardStepID = ?
                ORDER BY TimeStamp;
            ";

            foreach (var s in stepRows)
            {
                var step = new ScStepModel
                {
                    Id = s.ScCardStepID,
                    SortOrder = s.SortOrder,
                    Title = s.Title ?? "",
                    StepValue = s.StepValue,
                };

                var repRows = await Db.QueryAsync<ScCardStepRepRow>(repsSql, step.Id);

                // If your ScStepModel.Reps is List<DateTime> (as per your earlier pattern)
                step.Reps = repRows
                    .Select(r => ParseIsoDateTime(r.TimeStamp))
                    .ToList();

                // If you rely on a "version bump" to refresh converters, you can optionally:
                // step.BumpRepsVersion();  (only if you have such a method)

                model.Steps.Add(step);
            }

            return model;
        }

        public async Task<List<ScCardModel>> GetScModelsDataAsync(DateTime rangeStart, DateTime rangeEnd)
        {
            // 1) Fetch all SC rows + base Card
            var sql = @"
                SELECT
                    s.ScCardID     AS ScCardID,
                    s.CardID       AS CardID,

                    c.Title        AS Title,
                    c.Tags         AS Tags,

                    s.Status       AS Status,
                    s.Description  AS Description
                FROM ScCard s
                JOIN Card c ON c.CardID = s.CardID
            ";

            sql += ";";

            var rows = await Db.QueryAsync<ScCardJoinedRow>(sql);
            if (rows.Count == 0) return new List<ScCardModel>();

            // 2) Materialize models (without children yet)
            var models = rows
                .Select(r => new ScCardModel
                {
                    Id = r.ScCardID,
                    CardID = r.CardID,
                    Title = r.Title ?? "",
                    Tags = r.Tags ?? "",
                    Status = r.Status ?? "",
                    Description = r.Description ?? "",
                    Activity = new List<ActivityModel>()
                })
                .ToList();

            var byScId = models.ToDictionary(m => m.Id);

            // 2.5) Bulk-load Activity for all CardIDs (overlap window)
            var cardIds = rows.Select(r => r.CardID).Distinct().ToList();
            var actByCardId = new Dictionary<long, List<ActivityModel>>();

            if (cardIds.Count > 0)
            {
                var placeholders = string.Join(", ", cardIds.Select(_ => "?"));

                var actSql = $@"
                        SELECT
                            ActivityID       AS ActivityID,
                            CardID           AS CardID,
                            Start            AS Start,
                            ""End""          AS End,
                            ValueRateName    AS ValueRateName,
                            ValuePerMinute   AS ValuePerMinute
                        FROM Activity
                        WHERE CardID IN ({placeholders})
                          AND Start < ?
                          AND (""End"" IS NULL OR ""End"" > ?)
                        ORDER BY CardID, Start;
                    ";

                var args = cardIds.Cast<object>()
                    .Append(rangeEnd.ToString("o"))
                    .Append(rangeStart.ToString("o"))
                    .ToArray();

                var actRows = await Db.QueryAsync<ActivityRow>(actSql, args);

                actByCardId = actRows
                    .GroupBy(a => a.CardID)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(a => ActivityMapper.ToModel(a, ParseIsoDateTime)).ToList()
                    );
            }


            // Attach activity to each SC model using the row's CardID
            foreach (var r in rows)
            {
                if (!byScId.TryGetValue(r.ScCardID, out var m))
                    continue;

                m.Activity = actByCardId.TryGetValue(r.CardID, out var acts)
                    ? acts
                    : new List<ActivityModel>();
            }


            // 3) Load all steps for these SC cards
            var scIds = rows.Select(r => r.ScCardID).Distinct().ToList();

            var scPlaceholders = string.Join(", ", scIds.Select(_ => "?"));
            var stepsSql = $@"
                SELECT
                    ScCardStepID AS ScCardStepID,
                    ScCardID     AS ScCardID,
                    SortOrder    AS SortOrder,
                    Title        AS Title,
                    StepValue    AS StepValue
                FROM ScCardStep
                WHERE ScCardID IN ({scPlaceholders})
                ORDER BY ScCardID, SortOrder;
            ";

            var stepRows = await Db.QueryAsync<ScCardStepRow>(stepsSql, scIds.Cast<object>().ToArray());

            // Create steps + attach to models, and gather step IDs
            var stepIdToStep = new Dictionary<int, ScStepModel>();
            var stepIds = new List<int>();

            foreach (var s in stepRows)
            {
                if (!byScId.TryGetValue(s.ScCardID, out var parent))
                    continue;

                var step = new ScStepModel
                {
                    Id = s.ScCardStepID,
                    SortOrder = s.SortOrder,
                    Title = s.Title ?? "",
                    StepValue = s.StepValue,
                    Reps = new List<DateTime>()
                };

                parent.Steps.Add(step);

                stepIdToStep[step.Id] = step;
                stepIds.Add(step.Id);
            }

            // 4) Load all reps for these steps (if any)
            if (stepIds.Count > 0)
            {
                var stepPlaceholders = string.Join(", ", stepIds.Select(_ => "?"));
                var repsSql = $@"
                    SELECT
                        ScCardStepID AS ScCardStepID,
                        TimeStamp    AS TimeStamp,
                        StepValue    AS StepValue
                    FROM ScCardStepRep
                    WHERE ScCardStepID IN ({stepPlaceholders})
                      AND TimeStamp >= ?
                      AND TimeStamp <= ?
                    ORDER BY ScCardStepID, TimeStamp;
                ";

                var repRows = await Db.QueryAsync<ScCardStepRepRow>(
                    repsSql,
                    stepIds.Cast<object>()
                    .Append(rangeStart.ToString("o"))
                    .Append(rangeEnd.ToString("o"))
                    .ToArray());

                foreach (var r in repRows)
                {
                    if (!stepIdToStep.TryGetValue(r.ScCardStepID, out var step))
                        continue;

                    step.Reps.Add(ParseIsoDateTime(r.TimeStamp));
                }
            }

            return models;
        }

        private sealed class ScCardJoinedRow
        {
            public int ScCardID { get; set; }
            public long CardID { get; set; }

            public string? Title { get; set; }
            public string? Tags { get; set; }

            public string? Status { get; set; }
            public string? Description { get; set; }
        }

        private sealed class ScCardStepRow
        {
            public int ScCardStepID { get; set; }
            public int ScCardID { get; set; }

            public int SortOrder { get; set; }
            public string? Title { get; set; }
            public double StepValue { get; set; }
        }

        private sealed class ScCardStepRepRow
        {
            public int ScCardStepID { get; set; }
            public string TimeStamp { get; set; } = "";
            public double StepValue { get; set; }
        }

        #endregion

        #region TAT Cards

        //TAT
        public async Task<TatCardModel> GetTatModelDataAsync(int id)
        {
            // 1) Fetch TatCard + base Card
            const string sql = @"
                SELECT
                    t.TatCardID      AS TatCardID,
                    t.CardID         AS CardID,

                    c.Title          AS Title,
                    c.Tags           AS Tags,

                    t.ValuePerMinute AS ValuePerMinute,
                    t.Status         AS Status,
                    t.Description    AS Description,
                    t.TargetActiveTimeSeconds AS TargetActiveTimeSeconds,
                FROM TatCard t
                JOIN Card c ON c.CardID = t.CardID
                WHERE t.TatCardID = ?
                LIMIT 1;
            ";

            var row = (await Db.QueryAsync<TatCardJoinedRow>(sql, id)).FirstOrDefault();
            if (row == null) throw new KeyNotFoundException($"TatCard not found. TatCardID={id}");

            var model = new TatCardModel
            {
                Id = row.TatCardID,
                CardID = row.CardID,
                Title = row.Title ?? "",
                Tags = row.Tags ?? "",
                ValuePerMinute = row.ValuePerMinute,
                Status = row.Status ?? "",
                Description = row.Description ?? "",
                Activity = new List<ActivityModel>(),
                ValueRates = new List<ValueRateModel>(),
                TargetActiveTime = row.TargetActiveTimeSeconds == null ? null : TimeSpan.FromSeconds(row.TargetActiveTimeSeconds.Value)
            };

            // 2) Load activity by CardID
            const string actSql = @"
                SELECT
                    ActivityID       AS ActivityID,
                    CardID           AS CardID,
                    Start            AS Start,
                    ""End""          AS End,
                    ValueRateName    AS ValueRateName,
                    ValuePerMinute   AS ValuePerMinute
                FROM Activity
                WHERE CardID = ?
                ORDER BY Start;
            ";

            var actRows = await Db.QueryAsync<ActivityRow>(actSql, row.CardID);

            model.Activity = actRows
                .Select(a => ActivityMapper.ToModel(a, ParseIsoDateTime))
                .ToList();


            // 3) Load value rates by TatCardID
            const string vrSql = @"
                SELECT
                    TatCardValueRateID AS TatCardValueRateID,
                    TatCardID          AS TatCardID,
                    RateName           AS RateName,
                    ValuePerMinute     AS ValuePerMinute
                FROM TatCardValueRate
                WHERE TatCardID = ?
                ORDER BY TatCardValueRateID;
            ";

            var vrRows = await Db.QueryAsync<TatValueRateRow>(vrSql, row.TatCardID);

            model.ValueRates = vrRows.Select(v => new ValueRateModel
            {
                Id = v.TatCardValueRateID,
                RateName = v.RateName ?? "",
                ValuePerMinute = v.ValuePerMinute
            }).ToList();

            var scheduleModels = await GetCardSchedulesData(row.CardID);

            model.SetSchedules(scheduleModels);

            return model;
        }

        public async Task<List<TatCardModel>> GetTatModelsDataAsync(DateTime rangeStart, DateTime rangeEnd)
        {
            // 1) Fetch all TatCards + base Card (optionally filtered)
            var sql = @"
                SELECT
                    t.TatCardID      AS TatCardID,
                    t.CardID         AS CardID,

                    c.Title          AS Title,
                    c.Tags           AS Tags,

                    t.ValuePerMinute AS ValuePerMinute,
                    t.Status         AS Status,
                    t.Description    AS Description,
                    t.TargetActiveTimeSeconds AS TargetActiveTimeSeconds
                FROM TatCard t
                JOIN Card c ON c.CardID = t.CardID
            ";

            sql += ";";

            var cols = await Db.QueryAsync<PragmaTableInfo>("PRAGMA table_info(TatCard);");

            var rows = await Db.QueryAsync<TatCardJoinedRow>(sql);
            if (rows.Count == 0) return new List<TatCardModel>();

            // 2) Bulk-load Activity for all CardIDs (overlap window)
            var cardIds = rows.Select(r => r.CardID).Distinct().ToList();
            var actByCardId = new Dictionary<long, List<ActivityModel>>();

            if (cardIds.Count > 0)
            {
                var placeholders = string.Join(",", cardIds.Select(_ => "?"));
                var actSql = $@"
                    SELECT
                        ActivityID       AS ActivityID,
                        CardID           AS CardID,
                        Start            AS Start,
                        ""End""          AS End,
                        ValueRateName    AS ValueRateName,
                        ValuePerMinute   AS ValuePerMinute
                    FROM Activity
                    WHERE CardID IN ({placeholders})
                      AND Start < ?
                      AND (""End"" IS NULL OR ""End"" > ?)
                    ORDER BY CardID, Start;
                ";

                var args = cardIds.Cast<object>()
                    .Append(rangeEnd.ToString("o"))
                    .Append(rangeStart.ToString("o"))
                    .ToArray();

                var actRows = await Db.QueryAsync<ActivityRow>(actSql, args);

                actByCardId = actRows
                    .GroupBy(a => a.CardID)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(a => ActivityMapper.ToModel(a, ParseIsoDateTime)).ToList()
                    );
            }


            // 3) Bulk-load ValueRates for all TatCardIDs
            var tatIds = rows.Select(r => r.TatCardID).Distinct().ToList();
            var vrByTatId = new Dictionary<int, List<ValueRateModel>>();
            {
                var placeholders = string.Join(",", tatIds.Select(_ => "?"));
                var vrSql = $@"
                    SELECT
                        TatCardValueRateID AS TatCardValueRateID,
                        TatCardID          AS TatCardID,
                        RateName           AS RateName,
                        ValuePerMinute     AS ValuePerMinute
                    FROM TatCardValueRate
                    WHERE TatCardID IN ({placeholders})
                    ORDER BY TatCardID, TatCardValueRateID;
                ";

                var vrRows = await Db.QueryAsync<TatValueRateRow>(vrSql, tatIds.Cast<object>().ToArray());

                vrByTatId = vrRows
                    .GroupBy(v => v.TatCardID)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(v => new ValueRateModel
                        {
                            Id = v.TatCardValueRateID,
                            RateName = v.RateName ?? "",
                            ValuePerMinute = v.ValuePerMinute
                        }).ToList()
                    );
            }

            // 4) Materialize models
            var result = new List<TatCardModel>(rows.Count);

            foreach (var r in rows)
            {
                var model = new TatCardModel
                {
                    Id = r.TatCardID,
                    CardID = r.CardID,
                    Title = r.Title ?? "",
                    Tags = r.Tags ?? "",
                    ValuePerMinute = r.ValuePerMinute,
                    Status = r.Status ?? "",
                    Description = r.Description ?? "",
                    TargetActiveTime = r.TargetActiveTimeSeconds == null ? null : TimeSpan.FromSeconds(r.TargetActiveTimeSeconds.Value),
                    Activity = actByCardId.TryGetValue(r.CardID, out var acts) ? acts : new List<ActivityModel>(),
                    ValueRates = vrByTatId.TryGetValue(r.TatCardID, out var vrs) ? vrs : new List<ValueRateModel>()
                };

                var scheduleModels = await GetCardSchedulesData(r.CardID);

                model.SetSchedules(scheduleModels);

                result.Add(model);
            }

            return result;
        }

        // Internal DTOs for sqlite-net mapping
        private sealed class TatCardJoinedRow
        {
            public int TatCardID { get; set; }
            public long CardID { get; set; }

            public string? Title { get; set; }
            public string? Tags { get; set; }

            public double ValuePerMinute { get; set; }
            public string? Status { get; set; }
            public string? Description { get; set; }

            public int? TargetActiveTimeSeconds { get; set; }
        }

        private sealed class TatValueRateRow
        {
            public int TatCardValueRateID { get; set; }
            public int TatCardID { get; set; }
            public string? RateName { get; set; }
            public double ValuePerMinute { get; set; }
        }

        #endregion

        #endregion

        #region Missions

        //Mission
        public async Task<MissionCardModel> GetMissionCardModelDataAsync(int id)
        {
            // 1) Fetch the MissionCard + base Card in one go
            const string sql = @"
                SELECT
                    m.MissionCardID      AS MissionCardID,
                    m.CardID             AS CardID,

                    c.Title              AS Title,
                    c.Tags               AS Tags,

                    m.Status             AS Status,
                    m.Description        AS Description,
                    m.SubType            AS SubType,
                    m.Value              AS Value,

                    m.CreatedDate        AS CreatedDate,
                    m.AvailableFromDate  AS AvailableFromDate,
                    m.DueDate            AS DueDate,
                    m.CompletedDate      AS CompletedDate,
                    m.EventDate          AS EventDate,

                    m.EstCompletionTimeText AS EstCompletionTimeText,
                    m.IsFailed           AS IsFailed,
                    m.ValuePerMinute     AS ValuePerMinute
                FROM MissionCard m
                JOIN Card c ON c.CardID = m.CardID
                WHERE m.MissionCardID = ?
                LIMIT 1;
            ";

            var row = (await Db.QueryAsync<MissionCardJoinedRow>(sql, id)).FirstOrDefault();
            if (row == null) throw new KeyNotFoundException($"MissionCard not found. MissionCardID={id}");

            Enum.TryParse<MissionSubType>(row.SubType, ignoreCase: true, out var subType);

            // 2) Materialize the model
            var model = new MissionCardModel
            {
                Id = row.MissionCardID,
                CardID = row.CardID,

                Title = row.Title ?? "",
                Tags = row.Tags ?? "",

                Status = row.Status ?? "",
                Description = row.Description ?? "",
                SubType = subType,

                Value = row.Value,
                ValuePerMinute = row.ValuePerMinute,

                CreatedDate = ParseIsoDateTime(row.CreatedDate),
                AvailableFromDate = ParseIsoDateTime(row.AvailableFromDate),
                DueDate = ParseIsoDateTime(row.DueDate),
                CompletedDate = string.IsNullOrWhiteSpace(row.CompletedDate) ? (DateTime?)null : ParseIsoDateTime(row.CompletedDate),
                EventDate = string.IsNullOrWhiteSpace(row.EventDate) ? (DateTime?)null : ParseIsoDateTime(row.EventDate),

                EstCompletionTime = StringToTimeSpan(row.EstCompletionTimeText),
                IsFailed = row.IsFailed != 0,
            };

            // 3) Load activity slices by CardID
            const string actSql = @"
                SELECT
                    ActivityID       AS ActivityID,
                    CardID           AS CardID,
                    Start            AS Start,
                    ""End""          AS End,
                    ValueRateName    AS ValueRateName,
                    ValuePerMinute   AS ValuePerMinute
                FROM Activity
                WHERE CardID = ?
                ORDER BY Start;
            ";

            var actRows = await Db.QueryAsync<ActivityRow>(actSql, row.CardID);

            model.Activity = actRows
                .Select(a => ActivityMapper.ToModel(a, ParseIsoDateTime))
                .ToList();


            return model;
        }

        // Internal DTOs for sqlite-net mapping
        private sealed class MissionCardJoinedRow
        {
            public int MissionCardID { get; set; }
            public long CardID { get; set; }

            public string? Title { get; set; }
            public string? Tags { get; set; }

            public string? Status { get; set; }
            public string? Description { get; set; }
            public string? SubType { get; set; }

            public double Value { get; set; }

            // Stored as TEXT (ISO-8601)
            public string CreatedDate { get; set; } = "";
            public string AvailableFromDate { get; set; } = "";
            public string DueDate { get; set; } = "";
            public string? CompletedDate { get; set; }
            public string? EventDate { get; set; }

            public string? EstCompletionTimeText { get; set; }

            // Stored as INTEGER (0/1)
            public int IsFailed { get; set; }

            public double ValuePerMinute { get; set; }
        }

        public async Task<List<MissionCardModel>> GetMissionCardModelsDataAsync(string whereClause = null)
        {
            // Base query: MissionCard + Card
            var sql = @"
                SELECT
                    m.MissionCardID         AS MissionCardID,
                    m.CardID                AS CardID,

                    c.Title                 AS Title,
                    c.Tags                  AS Tags,

                    m.Status                AS Status,
                    m.Description           AS Description,
                    m.SubType               AS SubType,
                    m.Value                 AS Value,

                    m.CreatedDate           AS CreatedDate,
                    m.AvailableFromDate     AS AvailableFromDate,
                    m.DueDate               AS DueDate,
                    m.CompletedDate         AS CompletedDate,
                    m.EventDate             AS EventDate,

                    m.EstCompletionTimeText AS EstCompletionTimeText,
                    m.IsFailed              AS IsFailed,
                    m.ValuePerMinute        AS ValuePerMinute
                FROM MissionCard m
                JOIN Card c ON c.CardID = m.CardID
            ";

            // Support callers passing either:
            //  - null/empty
            //  - "WHERE ..."
            //  - "m.Status = 'In-Progress' AND m.IsFailed = 0"   (we'll add WHERE)
            //  - "ORDER BY ..." (if you do this, you should pass a full clause starting with ORDER BY)
            if (!string.IsNullOrWhiteSpace(whereClause))
            {
                var wc = whereClause.Trim();

                if (wc.StartsWith("WHERE", StringComparison.OrdinalIgnoreCase) ||
                    wc.StartsWith("ORDER BY", StringComparison.OrdinalIgnoreCase) ||
                    wc.StartsWith("LIMIT", StringComparison.OrdinalIgnoreCase))
                {
                    sql += " " + wc;
                }
                else
                {
                    sql += " WHERE " + wc;
                }
            }

            var rows = await Db.QueryAsync<MissionCardJoinedRow>(sql);

            if (rows.Count == 0)
                return new List<MissionCardModel>();

            // Materialize mission models and keep a lookup by CardID (Activity is stored by CardID)
            var byCardId = new Dictionary<long, MissionCardModel>();

            foreach (var row in rows)
            {
                Enum.TryParse<MissionSubType>(row.SubType, ignoreCase: true, out var subType);

                var model = new MissionCardModel
                {
                    Id = row.MissionCardID,
                    CardID = row.CardID,

                    Title = row.Title ?? "",
                    Tags = row.Tags ?? "",

                    Status = row.Status ?? "",
                    Description = row.Description ?? "",
                    SubType = subType,

                    Value = row.Value,
                    ValuePerMinute = row.ValuePerMinute,

                    CreatedDate = ParseIsoDateTime(row.CreatedDate),
                    AvailableFromDate = ParseIsoDateTime(row.AvailableFromDate),
                    DueDate = ParseIsoDateTime(row.DueDate),

                    // We'll restore IsComplete via Complete()/Fail() below.
                    CompletedDate = null,

                    EventDate = string.IsNullOrWhiteSpace(row.EventDate) ? (DateTime?)null : ParseIsoDateTime(row.EventDate),

                    EstCompletionTime = StringToTimeSpan(row.EstCompletionTimeText),

                    // We'll restore IsFailed via Fail() below if needed.
                    IsFailed = row.IsFailed != 0,

                    Activity = new List<ActivityModel>()
                };

                // Restore completion state correctly (IsComplete has a private setter)
                if (!string.IsNullOrWhiteSpace(row.CompletedDate))
                {
                    var completedAt = ParseIsoDateTime(row.CompletedDate!);

                    if (row.IsFailed != 0)
                        model.Fail(completedAt);
                    else
                        model.Complete(completedAt);
                }

                byCardId[row.CardID] = model;
            }

            // Load all Activity rows in one go
            var cardIds = byCardId.Keys.ToList();
            var placeholders = string.Join(", ", Enumerable.Repeat("?", cardIds.Count));

            var actSql = $@"
                SELECT
                    ActivityID       AS ActivityID,
                    CardID           AS CardID,
                    Start            AS Start,
                    ""End""          AS End,
                    ValueRateName    AS ValueRateName,
                    ValuePerMinute   AS ValuePerMinute
                FROM Activity
                WHERE CardID IN ({placeholders})
                ORDER BY CardID, Start;
            ";

            var actRows = await Db.QueryAsync<ActivityRow>(actSql, cardIds.Cast<object>().ToArray());

            foreach (var a in actRows)
            {
                if (!byCardId.TryGetValue(a.CardID, out var mission))
                    continue;

                mission.Activity.Add(ActivityMapper.ToModel(a, ParseIsoDateTime));
            }

            // Return in the same order as the base query result set // (Dictionary doesn't preserve ordering reliably).
            var result = new List<MissionCardModel>(rows.Count);
            foreach (var row in rows) result.Add(byCardId[row.CardID]);

            return result;
        }

        #endregion

        #region Trackers

        private sealed class TrackerValueRow
        {
            public int TrackerValueID { get; set; }
            public long CardID { get; set; }
            public string TimeStamp { get; set; } = "";
            public double Value { get; set; }
        }

        #region Value Trackers

        private sealed class ValueTrackerJoinedRow
        {
            public int ValueTrackerCardID { get; set; }
            public long CardID { get; set; }
            public string? Title { get; set; }
            public string? Tags { get; set; }

            public string? Unit { get; set; }
            public string CreatedDate { get; set; } = "";
            public string RangeStart { get; set; } = "";

            public int ScheduleEvery { get; set; }
            public string? ScheduleUnit { get; set; }
        }

        public async Task<ValueTrackerCardModel> GetValueTrackerCardModelDataAsync(int id)
        {
            await InitializeAsync();

            const string sql = @"
                    SELECT
                        vt.ValueTrackerCardID AS ValueTrackerCardID,
                        vt.CardID             AS CardID,

                        c.Title               AS Title,
                        c.Tags                AS Tags,

                        vt.Unit               AS Unit,
                        vt.CreatedDate        AS CreatedDate,
                        vt.RangeStart         AS RangeStart,

                        vt.ScheduleEvery      AS ScheduleEvery,
                        vt.ScheduleUnit       AS ScheduleUnit
                    FROM ValueTrackerCard vt
                    JOIN Card c ON c.CardID = vt.CardID
                    WHERE vt.ValueTrackerCardID = ?
                    LIMIT 1;
                ";

            var row = (await Db.QueryAsync<ValueTrackerJoinedRow>(sql, id)).FirstOrDefault();
            if (row == null)
                throw new KeyNotFoundException($"ValueTrackerCard not found. ValueTrackerCardID={id}");

            var model = new ValueTrackerCardModel
            {
                Id = row.ValueTrackerCardID,
                CardID = row.CardID,
                Title = row.Title ?? "",
                Tags = row.Tags ?? "",
                Unit = row.Unit ?? "",
                CreatedDate = ParseIsoDateTime(row.CreatedDate),
                RangeStart = ParseIsoDateTime(row.RangeStart),
                ScheduleEvery = row.ScheduleEvery,
                ScheduleUnit = row.ScheduleUnit ?? "Week"
            };

            // Load values
            const string valuesSql = @"
                    SELECT
                        TrackerValueID AS TrackerValueID,
                        CardID         AS CardID,
                        TimeStamp      AS TimeStamp,
                        Value          AS Value
                    FROM TrackerValue
                    WHERE CardID = ?
                    ORDER BY TimeStamp;
                ";

            var valueRows = await Db.QueryAsync<TrackerValueRow>(valuesSql, row.CardID);

            model.SetValues(valueRows.Select(v => new TrackerValueModel
            {
                Id = v.TrackerValueID,
                Timestamp = ParseIsoDateTime(v.TimeStamp),
                Value = v.Value
            }).ToList());

            // Load schedules
            var scheduleModels = await GetCardSchedulesData(row.CardID);

            model.SetSchedules(scheduleModels);

            return model;
        }

        public async Task<List<ValueTrackerCardModel>> GetValueTrackerCardModelsDataAsync(string whereClause = null)
        {
            await InitializeAsync();

            const string sql = @"
                SELECT
                    vt.ValueTrackerCardID AS ValueTrackerCardID,
                    vt.CardID             AS CardID,

                    c.Title               AS Title,
                    c.Tags                AS Tags,

                    vt.Unit               AS Unit,
                    vt.CreatedDate        AS CreatedDate,
                    vt.RangeStart         AS RangeStart,

                    vt.ScheduleEvery      AS ScheduleEvery,
                    vt.ScheduleUnit       AS ScheduleUnit
                FROM ValueTrackerCard vt
                JOIN Card c ON c.CardID = vt.CardID
                ORDER BY vt.ValueTrackerCardID;
            ";

            var rows = await Db.QueryAsync<ValueTrackerJoinedRow>(sql);
            if (rows.Count == 0) return new List<ValueTrackerCardModel>();

            // Materialize models (without values yet)
            var models = new List<ValueTrackerCardModel>();
            foreach (var r in rows)
            {
                var vt = new ValueTrackerCardModel
                {
                    Id = r.ValueTrackerCardID,
                    CardID = r.CardID,
                    Title = r.Title ?? "",
                    Tags = r.Tags ?? "",
                    Unit = r.Unit ?? "",
                    CreatedDate = ParseIsoDateTime(r.CreatedDate),
                    RangeStart = ParseIsoDateTime(r.RangeStart),
                    ScheduleEvery = r.ScheduleEvery,
                    ScheduleUnit = r.ScheduleUnit ?? "Week"
                };

                var scheduleModels = await GetCardSchedulesData(r.CardID);

                vt.SetSchedules(scheduleModels);

                models.Add(vt);
            }

            // Bulk-load values for all trackers
            var cardIds = rows.Select(r => r.CardID).Distinct().ToList();
            var byCardId = models.ToDictionary(m => m.CardID);

            var placeholders = string.Join(", ", cardIds.Select(_ => "?"));
            var valuesSql = $@"
                SELECT
                    TrackerValueID AS TrackerValueID,
                    CardID         AS CardID,
                    TimeStamp      AS TimeStamp,
                    Value          AS Value
                FROM TrackerValue
                WHERE CardID IN ({placeholders})
                ORDER BY CardID, TimeStamp;
            ";

            var valueRows = await Db.QueryAsync<TrackerValueRow>(valuesSql, cardIds.Cast<object>().ToArray());

            foreach (var v in valueRows)
            {
                if (!byCardId.TryGetValue(v.CardID, out var parent))
                    continue;

                parent.Values.Add(new TrackerValueModel
                {
                    Id = v.TrackerValueID,
                    Timestamp = ParseIsoDateTime(v.TimeStamp),
                    Value = v.Value
                });
            }

            return models;
        }



        #endregion

        #region Event Trackers
        private sealed class EventTrackerJoinedRow
        {
            public int EventTrackerCardID { get; set; }
            public long CardID { get; set; }
            public string? Title { get; set; }
            public string? Tags { get; set; }

            public string? Unit { get; set; }
            public string CreatedDate { get; set; } = "";
            public string RangeStart { get; set; } = "";

            public string? GroupByPeriod { get; set; }
        }

        public async Task<EventTrackerCardModel> GetEventTrackerCardModelDataAsync(int id)
        {
            await InitializeAsync();

            const string sql = @"
                SELECT
                    et.EventTrackerCardID AS EventTrackerCardID,
                    et.CardID             AS CardID,

                    c.Title               AS Title,
                    c.Tags                AS Tags,

                    et.Unit               AS Unit,
                    et.CreatedDate        AS CreatedDate,
                    et.RangeStart         AS RangeStart,

                    et.GroupByPeriod      AS GroupByPeriod
                FROM EventTrackerCard et
                JOIN Card c ON c.CardID = et.CardID
                WHERE et.EventTrackerCardID = ?
                LIMIT 1;
            ";

            var row = (await Db.QueryAsync<EventTrackerJoinedRow>(sql, id)).FirstOrDefault();
            if (row == null)
                throw new KeyNotFoundException($"EventTrackerCard not found. EventTrackerCardID={id}");

            var model = new EventTrackerCardModel
            {
                Id = row.EventTrackerCardID,
                CardID = row.CardID,
                Title = row.Title ?? "",
                Tags = row.Tags ?? "",
                Unit = row.Unit ?? "",
                CreatedDate = ParseIsoDateTime(row.CreatedDate),
                RangeStart = ParseIsoDateTime(row.RangeStart),
                GroupByPeriod = row.GroupByPeriod ?? "Day"
            };

            const string valuesSql = @"
                SELECT
                    TrackerValueID AS TrackerValueID,
                    CardID         AS CardID,
                    TimeStamp      AS TimeStamp,
                    Value          AS Value
                FROM TrackerValue
                WHERE CardID = ?
                ORDER BY TimeStamp;
            ";

            var valueRows = await Db.QueryAsync<TrackerValueRow>(valuesSql, row.CardID);

            model.SetValues(valueRows.Select(v => ParseIsoDateTime(v.TimeStamp)).ToList());

            // Also keep IDs if you ever need them later:
            // (optional) you can load as TrackerValueModel as well,
            // but your EventTrackerCardModel currently exposes only SetValues(List<DateTime>)

            return model;
        }

        public async Task<List<EventTrackerCardModel>> GetEventTrackerCardModelsDataAsync(string whereClause = null)
        {
            await InitializeAsync();

            const string sql = @"
                SELECT
                    et.EventTrackerCardID AS EventTrackerCardID,
                    et.CardID             AS CardID,

                    c.Title               AS Title,
                    c.Tags                AS Tags,

                    et.Unit               AS Unit,
                    et.CreatedDate        AS CreatedDate,
                    et.RangeStart         AS RangeStart,

                    et.GroupByPeriod      AS GroupByPeriod
                FROM EventTrackerCard et
                JOIN Card c ON c.CardID = et.CardID
                ORDER BY et.EventTrackerCardID;
            ";

            var rows = await Db.QueryAsync<EventTrackerJoinedRow>(sql);
            if (rows.Count == 0) return new List<EventTrackerCardModel>();

            var models = rows.Select(r => new EventTrackerCardModel
            {
                Id = r.EventTrackerCardID,
                CardID = r.CardID,
                Title = r.Title ?? "",
                Tags = r.Tags ?? "",
                Unit = r.Unit ?? "",
                CreatedDate = ParseIsoDateTime(r.CreatedDate),
                RangeStart = ParseIsoDateTime(r.RangeStart),
                GroupByPeriod = r.GroupByPeriod ?? "Day"
            }).ToList();

            var cardIds = rows.Select(r => r.CardID).Distinct().ToList();
            var byCardId = models.ToDictionary(m => m.CardID);

            var placeholders = string.Join(", ", cardIds.Select(_ => "?"));
            var valuesSql = $@"
                SELECT
                    TrackerValueID AS TrackerValueID,
                    CardID         AS CardID,
                    TimeStamp      AS TimeStamp,
                    Value          AS Value
                FROM TrackerValue
                WHERE CardID IN ({placeholders})
                ORDER BY CardID, TimeStamp;
            ";

            var valueRows = await Db.QueryAsync<TrackerValueRow>(valuesSql, cardIds.Cast<object>().ToArray());

            foreach (var v in valueRows)
            {
                if (!byCardId.TryGetValue(v.CardID, out var parent))
                    continue;

                // For Event trackers, each row represents an event
                parent.Values.Add(new TrackerValueModel
                {
                    Id = v.TrackerValueID,
                    Timestamp = ParseIsoDateTime(v.TimeStamp),
                    Value = 1
                });
            }

            return models;
        }

        #endregion

        #endregion

        #region Card Schedules

        private async Task<List<CardSchedule>> GetCardSchedulesData(long cardID)
        {
            // Load schedules
            var scheduleRows = await Db.QueryAsync<CardScheduleRow>(
                @"SELECT
                      ScheduleID   AS ScheduleID,
                      CardID       AS CardID,
                      FrequencyType AS FrequencyType,
                      FrequencyValue AS FrequencyValue,
                      FromDateTime AS FromDateTime,
                      ToDateTime   AS ToDateTime,
                      IsEnabled    AS IsEnabled,
                      Note         AS Note
                  FROM CardSchedule
                  WHERE CardID = ?
                  ORDER BY datetime(FromDateTime);",
                            cardID);

            var scheduleModels = scheduleRows.Select(r =>
            {
                Enum.TryParse<FrequencyType>(r.FrequencyType ?? "", out var ft);

                return new CardSchedule
                {
                    ScheduleId = r.ScheduleID,
                    CardId = r.CardID,
                    FrequencyType = ft,
                    FrequencyValue = r.FrequencyValue,
                    FromDateTime = ParseIso(r.FromDateTime),
                    ToDateTime = string.IsNullOrWhiteSpace(r.ToDateTime) ? null : ParseIso(r.ToDateTime),
                    IsEnabled = r.IsEnabled != 0,
                    Note = r.Note ?? ""
                };
            }).ToList();

            return scheduleModels;
        }

        public async Task<List<CardSchedule>> GetEnabledCardSchedulesAsync()
        {
            await InitializeAsync();

            var rows = await Db.QueryAsync<CardScheduleRow>(
                @"SELECT
                      ScheduleID     AS ScheduleID,
                      CardID         AS CardID,
                      FrequencyType  AS FrequencyType,
                      FrequencyValue AS FrequencyValue,
                      FromDateTime   AS FromDateTime,
                      ToDateTime     AS ToDateTime,
                      IsEnabled      AS IsEnabled,
                      Note           AS Note
                  FROM CardSchedule
                  WHERE IsEnabled = 1
                  ORDER BY datetime(FromDateTime);");

            return rows.Select(CardScheduleMapper.ToDomain).ToList();
        }

        public async Task<CardSchedule?> GetCardScheduleByIdAsync(long scheduleId)
        {
            await InitializeAsync();

            var rows = await Db.QueryAsync<CardScheduleRow>(
                @"SELECT ScheduleId, CardId, IsEnabled, Note, FrequencyType, FrequencyValue, FromDateTime, ToDateTime
                  FROM CardSchedule
                  WHERE ScheduleId = ?",
                scheduleId);

            var row = rows.FirstOrDefault();
            return row == null ? null : CardScheduleMapper.ToDomain(row);
        }

        #endregion

        #region Notification Logs

        private static readonly TimeSpan NotificationSentMatchWindow = TimeSpan.FromMinutes(5);

        public async Task<IReadOnlyList<NotificationLogModel>> GetNotificationLogsAsync(int limit = 250)
        {
            await InitializeAsync();

            var take = Math.Clamp(limit, 1, 1000);
            var rows = await Db.QueryAsync<NotificationLogRow>(
                @"SELECT
                      NotificationLogId AS NotificationLogId,
                      ScheduleId        AS ScheduleId,
                      CardId            AS CardId,
                      CardTitle         AS CardTitle,
                      Note              AS Note,
                      Status            AS Status,
                      CreatedAt         AS CreatedAt,
                      ScheduledAt       AS ScheduledAt,
                      ScheduleFor       AS ScheduleFor,
                      SentAt            AS SentAt,
                      UpdatedAt         AS UpdatedAt,
                      Error             AS Error
                  FROM NotificationLog
                  ORDER BY datetime(ScheduleFor) DESC, NotificationLogId DESC
                  LIMIT ?;",
                take);

            return rows.Select(NotificationLogMapper.ToDomain).ToList();
        }

        public async Task<NotificationLogModel> UpsertNotificationLogCreatedAsync(
            CardSchedule schedule,
            string? cardTitle,
            DateTime scheduleFor,
            DateTime createdAt)
        {
            await InitializeAsync();

            var scheduleForIso = ToIso(scheduleFor);
            var existing = (await Db.QueryAsync<NotificationLogRow>(
                @"SELECT *
                  FROM NotificationLog
                  WHERE ScheduleId = ?
                    AND ScheduleFor = ?
                  LIMIT 1;",
                schedule.ScheduleId,
                scheduleForIso)).FirstOrDefault();

            if (existing == null)
            {
                await Db.ExecuteAsync(
                    @"INSERT INTO NotificationLog
                      (ScheduleId, CardId, CardTitle, Note, Status, CreatedAt, ScheduleFor, UpdatedAt)
                      VALUES (?, ?, ?, ?, ?, ?, ?, ?);",
                    schedule.ScheduleId,
                    schedule.CardId,
                    cardTitle ?? "",
                    schedule.Note ?? "",
                    NotificationLogStatuses.Created,
                    ToIso(createdAt),
                    scheduleForIso,
                    ToIso(createdAt));

                var id = await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                return await GetNotificationLogByIdAsync(id)
                    ?? throw new InvalidOperationException("Notification log row was inserted but could not be read.");
            }

            await Db.ExecuteAsync(
                @"UPDATE NotificationLog
                  SET CardId = ?,
                      CardTitle = ?,
                      Note = ?,
                      UpdatedAt = ?
                  WHERE NotificationLogId = ?;",
                schedule.CardId,
                cardTitle ?? "",
                schedule.Note ?? "",
                ToIso(createdAt),
                existing.NotificationLogId);

            return await GetNotificationLogByIdAsync(existing.NotificationLogId)
                ?? throw new InvalidOperationException("Notification log row could not be read.");
        }

        public async Task MarkNotificationLogScheduledAsync(long notificationLogId, DateTime scheduledAt)
        {
            await InitializeAsync();

            await Db.ExecuteAsync(
                @"UPDATE NotificationLog
                  SET Status = ?,
                      ScheduledAt = ?,
                      UpdatedAt = ?,
                      Error = NULL
                  WHERE NotificationLogId = ?
                    AND Status <> ?;",
                NotificationLogStatuses.Scheduled,
                ToIso(scheduledAt),
                ToIso(scheduledAt),
                notificationLogId,
                NotificationLogStatuses.Sent);
        }

        public async Task MarkNotificationLogScheduleErrorAsync(long notificationLogId, string error, DateTime updatedAt)
        {
            await InitializeAsync();

            await Db.ExecuteAsync(
                @"UPDATE NotificationLog
                  SET Error = ?,
                      UpdatedAt = ?
                  WHERE NotificationLogId = ?;",
                error,
                ToIso(updatedAt),
                notificationLogId);
        }

        public async Task MarkNotificationLogSentAsync(
            CardSchedule schedule,
            string? cardTitle,
            DateTime firedAt,
            DateTime sentAt)
        {
            await InitializeAsync();

            var matchCutoff = firedAt.Add(NotificationSentMatchWindow);
            var existing = (await Db.QueryAsync<NotificationLogRow>(
                @"SELECT *
                  FROM NotificationLog
                  WHERE ScheduleId = ?
                    AND Status IN (?, ?, ?)
                    AND datetime(ScheduleFor) <= datetime(?)
                  ORDER BY datetime(ScheduleFor) DESC, NotificationLogId DESC
                  LIMIT 1;",
                schedule.ScheduleId,
                NotificationLogStatuses.Created,
                NotificationLogStatuses.Scheduled,
                NotificationLogStatuses.Missed,
                ToIso(matchCutoff))).FirstOrDefault();

            var logId = existing?.NotificationLogId;
            if (logId == null)
            {
                var created = await UpsertNotificationLogCreatedAsync(schedule, cardTitle, firedAt, sentAt);
                logId = created.NotificationLogId;
            }

            await Db.ExecuteAsync(
                @"UPDATE NotificationLog
                  SET CardId = ?,
                      CardTitle = ?,
                      Note = ?,
                      Status = ?,
                      SentAt = ?,
                      UpdatedAt = ?,
                      Error = NULL
                  WHERE NotificationLogId = ?;",
                schedule.CardId,
                cardTitle ?? "",
                schedule.Note ?? "",
                NotificationLogStatuses.Sent,
                ToIso(sentAt),
                ToIso(sentAt),
                logId.Value);
        }

        public async Task MarkOverdueNotificationLogsMissedAsync(DateTime now, TimeSpan gracePeriod)
        {
            await InitializeAsync();

            var cutoff = now.Subtract(gracePeriod);
            await Db.ExecuteAsync(
                @"UPDATE NotificationLog
                  SET Status = ?,
                      UpdatedAt = ?
                  WHERE SentAt IS NULL
                    AND Status IN (?, ?)
                    AND datetime(ScheduleFor) < datetime(?);",
                NotificationLogStatuses.Missed,
                ToIso(now),
                NotificationLogStatuses.Created,
                NotificationLogStatuses.Scheduled,
                ToIso(cutoff));
        }

        private async Task<NotificationLogModel?> GetNotificationLogByIdAsync(long notificationLogId)
        {
            var row = (await Db.QueryAsync<NotificationLogRow>(
                @"SELECT *
                  FROM NotificationLog
                  WHERE NotificationLogId = ?
                  LIMIT 1;",
                notificationLogId)).FirstOrDefault();

            return row == null ? null : NotificationLogMapper.ToDomain(row);
        }

        public sealed class NotificationLogRow
        {
            public long NotificationLogId { get; set; }
            public long ScheduleId { get; set; }
            public long CardId { get; set; }
            public string CardTitle { get; set; } = "";
            public string Note { get; set; } = "";
            public string Status { get; set; } = "";
            public string CreatedAt { get; set; } = "";
            public string? ScheduledAt { get; set; }
            public string ScheduleFor { get; set; } = "";
            public string? SentAt { get; set; }
            public string UpdatedAt { get; set; } = "";
            public string? Error { get; set; }
        }

        public static class NotificationLogMapper
        {
            public static NotificationLogModel ToDomain(NotificationLogRow row)
            {
                return new NotificationLogModel
                {
                    NotificationLogId = row.NotificationLogId,
                    ScheduleId = row.ScheduleId,
                    CardId = row.CardId,
                    CardTitle = row.CardTitle ?? "",
                    Note = row.Note ?? "",
                    Status = row.Status ?? NotificationLogStatuses.Created,
                    CreatedAt = ParseIso(row.CreatedAt),
                    ScheduledAt = string.IsNullOrWhiteSpace(row.ScheduledAt) ? null : ParseIso(row.ScheduledAt!),
                    ScheduleFor = ParseIso(row.ScheduleFor),
                    SentAt = string.IsNullOrWhiteSpace(row.SentAt) ? null : ParseIso(row.SentAt!),
                    UpdatedAt = ParseIso(row.UpdatedAt),
                    Error = row.Error
                };
            }
        }

        #endregion

        #region Goals

        public async Task<List<GoalDetailsModel>> GetGoalModelsDataAsync()
        {
            await InitializeAsync();

            const string sql = @"
                SELECT
                    CardID       AS CardID,
                    TimeScope    AS TimeScope,
                    GoalHrs      AS GoalHrs,
                    Enabled      AS Enabled,
                    DeFactoStart AS DeFactoStart,
                    DeFactoEnd   AS DeFactoEnd
                FROM Goal
                ORDER BY CardID, TimeScope;
            ";


            var rows = await Db.QueryAsync<GoalRow>(sql);
            if (rows.Count == 0)
                return new List<GoalDetailsModel>();

            return rows.Select(r => new GoalDetailsModel
            {
                CardId = r.CardID,
                TimeScope = Enum.TryParse<TimeScope>(r.TimeScope, out var ts) ? ts : TimeScope.Daily,
                GoalHrs = r.GoalHrs,
                Enabled = r.Enabled != 0,
                DeFactoStart = ParseNullableTimeOnly(r.DeFactoStart),
                DeFactoEnd = ParseNullableTimeOnly(r.DeFactoEnd)
            }).ToList();
        }

        private sealed class GoalRow
        {
            public long CardID { get; set; }
            public string TimeScope { get; set; } = "";
            public double GoalHrs { get; set; }
            public int Enabled { get; set; }
            public string? DeFactoStart { get; set; }
            public string? DeFactoEnd { get; set; }
        }

        #endregion

        #region Activity

        public async Task<bool> HasActivityOverlapAsync(int excludeActivityId, DateTime candidateStart, DateTime? candidateEnd)
        {
            await InitializeAsync();

            var startIso = DateTime.SpecifyKind(candidateStart, DateTimeKind.Utc).ToString("o");
            var endIso = candidateEnd.HasValue
                ? DateTime.SpecifyKind(candidateEnd.Value, DateTimeKind.Utc).ToString("o")
                : null;

            // Global overlap rule:
            // (candidateEnd IS NULL OR db.Start < candidateEnd)
            // AND (db.End IS NULL OR candidateStart < db.End)
            //
            // Exclude the row being edited.
            const string sql = @"
                    SELECT 1
                    FROM Activity db
                    WHERE (? <= 0 OR db.ActivityID <> ?)
                      AND (
                            (? IS NULL OR db.Start < ?)
                            AND
                            (db.""End"" IS NULL OR ? < db.""End"")
                          )
                    LIMIT 1;
                ";

            var hit = await Db.ExecuteScalarAsync<int?>(
                sql,
                excludeActivityId, excludeActivityId,
                endIso, endIso,
                startIso
            );

            return hit.HasValue;
        }

        public async Task<DateTime?> GetCurrentOpenActivityStartUtcAsync(long cardId)
        {
            await InitializeAsync();

            const string sql = @"
                SELECT Start
                FROM Activity
                WHERE CardID = ?
                  AND ""End"" IS NULL
                ORDER BY Start DESC
                LIMIT 1;
            ";

            var startIso = await Db.ExecuteScalarAsync<string?>(sql, cardId);
            if (string.IsNullOrWhiteSpace(startIso))
                return null;

            // RoundtripKind respects the +00:00 offset in your stored text
            return DateTime.Parse(startIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        public async Task<DateTime?> GetLastClosedActivityEndUtcAsync()
        {
            await InitializeAsync();

            const string sql = @"
                SELECT ""End""
                FROM Activity
                WHERE ""End"" IS NOT NULL
                ORDER BY ""End"" DESC
                LIMIT 1;
            ";

            var endIso = await Db.ExecuteScalarAsync<string?>(sql);
            if (string.IsNullOrWhiteSpace(endIso))
                return null;

            return DateTime.Parse(endIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        public async Task<ActivityModel?> GetCurrentActiveActivityAsync()
        {
            await InitializeAsync();

            var row = await GetCurrentActiveRowAsync(); // private row-level method

            if (row == null)
                return null;

            return ActivityMapper.ToModel(row, ParseIsoDateTime);
        }

        private async Task<ActivityRow?> GetCurrentActiveRowAsync()
        {
            await InitializeAsync();

            const string sql = @"
                SELECT ActivityID, CardID, Start, ""End"", ValueRateName, ValuePerMinute
                FROM Activity
                WHERE ""End"" IS NULL
                ORDER BY Start DESC
                LIMIT 1;
            ";

            var rows = await Db.QueryAsync<ActivityRow>(sql);
            var row = rows.FirstOrDefault();

            if (row == null)
                return null;

            // Defensive: if DB NULL was mapped into End, normalize to empty string for now.
            row.End ??= "";

            return row;
        }

        private sealed class AdjacentActivityDatesRow
        {
            public string? PreviousEnd { get; set; }
            public string? NextStart { get; set; }
        }

        private sealed class ActivityRow
        {
            public int ActivityID { get; set; }
            public long CardID { get; set; }

            // Stored as TEXT (ISO-8601)
            public string Start { get; set; } = "";
            public string? End { get; set; }
            public string ValueRateName { get; set; } = "";

            public double ValuePerMinute { get; set; }
        }

        private static class ActivityMapper
        {
            public static ActivityModel ToModel(ActivityRow row, Func<string, DateTime> parseIsoDateTime)
            {
                if (row == null) throw new ArgumentNullException(nameof(row));
                if (string.IsNullOrWhiteSpace(row.Start))
                    throw new InvalidOperationException("ActivityRow.Start is required.");

                // End is NULL for open; also treat whitespace as open to be resilient to legacy data
                DateTime? end = null;
                if (!string.IsNullOrWhiteSpace(row.End))
                    end = parseIsoDateTime(row.End!);

                return new ActivityModel
                {
                    Id = row.ActivityID,
                    CardID = row.CardID,
                    StartDate = parseIsoDateTime(row.Start),
                    EndDate = end,
                    RateName = row.ValueRateName ?? "",
                    ValuePerMinute = row.ValuePerMinute
                };
            }
        }

        /* Toggle Activity */

        public async Task<ToggleActivityModelResult> ToggleActivityAsync(long cardId, DateTime utcNow, string valueRateName, double valuePerMinute)
        {
            // Call the internal row-level method
            var rowResult = await ToggleActivityInternalAsync(cardId, utcNow, valueRateName, valuePerMinute);

            return new ToggleActivityModelResult
            {
                Closed = rowResult.Closed != null
                    ? ActivityMapper.ToModel(rowResult.Closed, ParseIsoDateTime)
                    : null,

                Opened = rowResult.Opened != null
                    ? ActivityMapper.ToModel(rowResult.Opened, ParseIsoDateTime)
                    : null
            };
        }

        private async Task<ToggleActivityRowResult> ToggleActivityInternalAsync(long cardId, DateTime utcNow, string valueRateName, double valuePerMinute)
        {
            await InitializeAsync();

            var nowIso = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc).ToString("o");

            ActivityRow? closed = null;
            ActivityRow? opened = null;

            await Db.RunInTransactionAsync(tran =>
            {
                // 1) Fetch current open activity (at most one due to UX_Activity_OneOpen)
                closed = tran.Query<ActivityRow>(@"
                    SELECT
                        ActivityID       AS ActivityID,
                        CardID           AS CardID,
                        Start            AS Start,
                        ""End""          AS End,
                        ValueRateName    AS ValueRateName,
                        ValuePerMinute   AS ValuePerMinute
                    FROM Activity
                    WHERE ""End"" IS NULL
                    ORDER BY Start DESC
                    LIMIT 1;
                ").FirstOrDefault();

                // 2) If there's an open activity, close it
                if (closed != null)
                {
                    tran.Execute(@"
                        UPDATE Activity
                        SET ""End"" = ?
                        WHERE ActivityID = ?;
                    ", nowIso, closed.ActivityID);

                    // reflect in returned row
                    closed.End = nowIso;

                    // If it's the same card, we stop here (toggle off)
                    if (closed.CardID == cardId)
                        return;
                }

                // 3) Otherwise open a new activity for cardId
                // (If a different card was open, we closed it above.)
                tran.Execute(@"
                    INSERT INTO Activity (CardID, Start, ""End"", ValueRateName, ValuePerMinute)
                    VALUES (?, ?, NULL, ?, ?);
                ", cardId, nowIso, valueRateName, valuePerMinute);

                // Get inserted row id and return the full row
                var newId = tran.ExecuteScalar<long>("SELECT last_insert_rowid();");

                opened = new ActivityRow
                {
                    ActivityID = (int)newId,
                    CardID = cardId,
                    Start = nowIso,
                    End = null,
                    ValueRateName = valueRateName ?? "",
                    ValuePerMinute = valuePerMinute
                };
            });

            return new ToggleActivityRowResult { Closed = closed, Opened = opened };
        }

        //public sealed class ToggleActivityModelResult
        //{
        //    public ActivityModel? Closed { get; init; }
        //    public ActivityModel? Opened { get; init; }
        //}

        private sealed class ToggleActivityRowResult
        {
            public ActivityRow? Closed { get; init; }
            public ActivityRow? Opened { get; init; }
        }

        /* UPSERT */

        private static bool Overlaps(string aStart, string? aEnd, string bStart, string? bEnd)
        {
            // overlap if:
            // aStart < bEnd (or bEnd is null)
            // and bStart < aEnd (or aEnd is null)
            return (bEnd == null || string.CompareOrdinal(aStart, bEnd) < 0)
                && (aEnd == null || string.CompareOrdinal(bStart, aEnd) < 0);
        }

        private static bool HasInternalOverlap(List<ActivityModel> activities)
        {
            var ordered = activities
                .OrderBy(a => a.StartDate)
                .ToList();

            for (int i = 0; i < ordered.Count - 1; i++)
            {
                var a = ordered[i];
                var b = ordered[i + 1];

                var aStart = DateTime.SpecifyKind(a.StartDate, DateTimeKind.Utc).ToString("o");
                var aEnd = a.EndDate.HasValue ? DateTime.SpecifyKind(a.EndDate.Value, DateTimeKind.Utc).ToString("o") : null;

                var bStart = DateTime.SpecifyKind(b.StartDate, DateTimeKind.Utc).ToString("o");
                var bEnd = b.EndDate.HasValue ? DateTime.SpecifyKind(b.EndDate.Value, DateTimeKind.Utc).ToString("o") : null;

                if (Overlaps(aStart, aEnd, bStart, bEnd))
                    return true;
            }

            return false;
        }

        public sealed class ActivityUpdateResult
        {
            public bool Success { get; init; }
            public string Message { get; init; } = "";
        }

        public async Task<ActivityUpdateResult> UpsertActivitiesAsync(List<ActivityModel> activities)
        {
            await InitializeAsync();

            if (activities == null) throw new ArgumentNullException(nameof(activities));
            if (activities.Count == 0)
                return new ActivityUpdateResult { Success = true, Message = "Activities updated." };

            // 1) Incoming overlap check
            if (HasInternalOverlap(activities))
            {
                return new ActivityUpdateResult
                {
                    Success = false,
                    Message = "Overlapping Activities cannot be written to the database"
                };
            }

            try
            {
                await Db.RunInTransactionAsync(tran =>
                {
                    // TEMP table for incoming set
                    tran.Execute(@"
                        CREATE TEMP TABLE IF NOT EXISTS _IncomingActivity (
                            ActivityID     INTEGER NULL,
                            CardID         INTEGER NOT NULL,
                            Start          TEXT    NOT NULL,
                            ""End""        TEXT    NULL,
                            ValueRateName  TEXT    NOT NULL,
                            ValuePerMinute REAL    NOT NULL
                        );
                    ");
                    tran.Execute("DELETE FROM _IncomingActivity;");

                    // Fill temp table
                    foreach (var a in activities)
                    {
                        var startIso = DateTime.SpecifyKind(a.StartDate, DateTimeKind.Utc).ToString("o");
                        var endIso = a.EndDate.HasValue
                            ? DateTime.SpecifyKind(a.EndDate.Value, DateTimeKind.Utc).ToString("o")
                            : null;

                        tran.Execute(@"
                            INSERT INTO _IncomingActivity (ActivityID, CardID, Start, ""End"", ValueRateName, ValuePerMinute)
                            VALUES (?, ?, ?, ?, ?, ?);
                        ",
                        a.Id > 0 ? a.Id : (object?)null,
                        a.CardID,
                        startIso,
                        endIso,
                        a.RateName ?? "",
                        a.ValuePerMinute);
                    }

                    // 2) Forbidden overlap check:
                    // Find any DB activity that overlaps an incoming activity
                    // where the DB activity is NOT part of the incoming update set.
                    //
                    // overlap rule:
                    // db.Start < incoming.End OR incoming.End IS NULL
                    // AND incoming.Start < db.End OR db.End IS NULL
                    //
                    // and ensure DB row not in incoming IDs
                    var forbidden = tran.ExecuteScalar<long?>(@"
                            SELECT db.ActivityID
                            FROM Activity db
                            JOIN _IncomingActivity inc
                              ON (
                                   (inc.""End"" IS NULL OR db.Start < inc.""End"")
                                   AND
                                   (db.""End"" IS NULL OR inc.Start < db.""End"")
                                 )
                            WHERE db.ActivityID NOT IN (
                                SELECT ActivityID FROM _IncomingActivity WHERE ActivityID IS NOT NULL
                            )
                            LIMIT 1;
                        ");

                    if (forbidden != null)
                        throw new InvalidOperationException("Cannot overlap with existing Activities in the database.");

                    // 3) Apply upserts (update existing, insert new)
                    foreach (var a in activities)
                    {
                        var startIso = DateTime.SpecifyKind(a.StartDate, DateTimeKind.Utc).ToString("o");
                        var endIso = a.EndDate.HasValue
                            ? DateTime.SpecifyKind(a.EndDate.Value, DateTimeKind.Utc).ToString("o")
                            : null;

                        if (a.Id > 0)
                        {
                            tran.Execute(@"
                                UPDATE Activity
                                SET CardID = ?,
                                    Start = ?,
                                    ""End"" = ?,
                                    ValueRateName = ?,
                                    ValuePerMinute = ?
                                WHERE ActivityID = ?;
                            ",
                                    a.CardID, startIso, endIso, a.RateName ?? "", a.ValuePerMinute, a.Id);
                        }
                        else
                        {
                            tran.Execute(@"
                                INSERT INTO Activity (CardID, Start, ""End"", ValueRateName, ValuePerMinute)
                                VALUES (?, ?, ?, ?, ?);
                            ",
                    a.CardID, startIso, endIso, a.RateName ?? "", a.ValuePerMinute);

                            // Optional: if you want to write IDs back into the passed objects:
                            // a.Id = (int)tran.ExecuteScalar<long>("SELECT last_insert_rowid();");
                        }
                    }
                });

                return new ActivityUpdateResult { Success = true, Message = "Activities updated." };
            }
            catch (InvalidOperationException ex)
            {
                return new ActivityUpdateResult { Success = false, Message = ex.Message };
            }
        }

        #endregion

        #region Planner

        public async Task<PlannerDayData> GetPlannerDayDataAsync(DateTime plannerDate)
        {
            await InitializeAsync();

            var start = plannerDate.Date;
            var end = start.AddDays(1);

            var planner = await GetPlannerForDateAsync(start);
            var mainQuest = await GetMainQuestModelsDataAsync(start, end);

            var startIso = start.ToString("o", CultureInfo.InvariantCulture);
            var endIso = end.ToString("o", CultureInfo.InvariantCulture);

            var missions = await GetMissionCardModelsDataAsync($@"
                m.CompletedDate IS NULL
                OR (m.CompletedDate >= '{startIso}' AND m.CompletedDate < '{endIso}')
                OR (m.AvailableFromDate < '{endIso}' AND m.DueDate >= '{startIso}')
            ");

            return new PlannerDayData
            {
                Planner = planner,
                TaskCards = mainQuest.Concat(missions.Cast<IActiveCardModel>()).ToList(),
                ScCards = mainQuest.OfType<ScCardModel>().ToList(),
                MissionCards = missions
            };
        }

        public async Task SavePlannerAsync(PlannerModel planner)
        {
            await InitializeAsync();

            if (planner == null)
                throw new ArgumentNullException(nameof(planner));

            ValidatePlannerTasks(planner.Tasks);

            var plannerDate = planner.PlannerDate.Date;
            var dateKey = ToPlannerDateKey(plannerDate);
            var nowIso = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            var plannerId = planner.PlannerId;

            await Db.RunInTransactionAsync(tran =>
            {
                var existing = tran.Query<PlannerIdRow>(
                    "SELECT PlannerID FROM Planner WHERE PlannerDate = ? LIMIT 1;",
                    dateKey).FirstOrDefault();

                if (existing == null)
                {
                    tran.Execute(
                        "INSERT INTO Planner (PlannerDate, CreatedAt, UpdatedAt) VALUES (?, ?, ?);",
                        dateKey,
                        nowIso,
                        nowIso);

                    plannerId = tran.ExecuteScalar<long>("SELECT last_insert_rowid();");
                }
                else
                {
                    plannerId = existing.PlannerID;
                    tran.Execute(
                        "UPDATE Planner SET UpdatedAt = ? WHERE PlannerID = ?;",
                        nowIso,
                        plannerId);
                }

                tran.Execute("DELETE FROM PlannerTask WHERE PlannerID = ?;", plannerId);
                tran.Execute("DELETE FROM PlannerEvent WHERE PlannerID = ?;", plannerId);

                foreach (var task in planner.Tasks.OrderBy(t => t.PlannedStart))
                {
                    tran.Execute(@"
                        INSERT INTO PlannerTask
                            (PlannerID, CardID, CardKind, PlannedStart, PlannedEnd)
                        VALUES (?, ?, ?, ?, ?);
                    ",
                    plannerId,
                    task.CardId,
                    task.CardKind.ToString(),
                    task.PlannedStart.ToString("o", CultureInfo.InvariantCulture),
                    task.PlannedEnd.ToString("o", CultureInfo.InvariantCulture));
                }

                foreach (var ev in planner.Events.OrderBy(e => e.PlannedTime))
                {
                    tran.Execute(@"
                        INSERT INTO PlannerEvent
                            (PlannerID, EventKind, CardID, ScCardStepID, PlannedTime, PlannedCount)
                        VALUES (?, ?, ?, ?, ?, ?);
                    ",
                    plannerId,
                    ev.EventKind.ToString(),
                    ev.CardId,
                    ev.ScCardStepId,
                    ev.PlannedTime.ToString("o", CultureInfo.InvariantCulture),
                    Math.Max(1, ev.PlannedCount));
                }
            });

            planner.PlannerId = plannerId;
            foreach (var task in planner.Tasks)
                task.PlannerId = plannerId;
            foreach (var ev in planner.Events)
                ev.PlannerId = plannerId;
        }

        private async Task<PlannerModel?> GetPlannerForDateAsync(DateTime plannerDate)
        {
            var dateKey = ToPlannerDateKey(plannerDate.Date);

            var row = (await Db.QueryAsync<PlannerRow>(
                "SELECT PlannerID, PlannerDate FROM Planner WHERE PlannerDate = ? LIMIT 1;",
                dateKey)).FirstOrDefault();

            if (row == null)
                return null;

            var planner = new PlannerModel
            {
                PlannerId = row.PlannerID,
                PlannerDate = DateTime.ParseExact(row.PlannerDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            };

            var tasks = await Db.QueryAsync<PlannerTaskRow>(@"
                SELECT PlannerTaskID, PlannerID, CardID, CardKind, PlannedStart, PlannedEnd
                FROM PlannerTask
                WHERE PlannerID = ?
                ORDER BY PlannedStart;
            ", row.PlannerID);

            foreach (var task in tasks)
            {
                if (!Enum.TryParse<PlannerTaskCardKind>(task.CardKind, true, out var kind))
                    kind = PlannerTaskCardKind.TatCard;

                planner.Tasks.Add(new PlannerTaskModel
                {
                    PlannerTaskId = task.PlannerTaskID,
                    PlannerId = task.PlannerID,
                    CardId = task.CardID,
                    CardKind = kind,
                    PlannedStart = ParseIsoDateTime(task.PlannedStart),
                    PlannedEnd = ParseIsoDateTime(task.PlannedEnd)
                });
            }

            var events = await Db.QueryAsync<PlannerEventRow>(@"
                SELECT PlannerEventID, PlannerID, EventKind, CardID, ScCardStepID, PlannedTime, PlannedCount
                FROM PlannerEvent
                WHERE PlannerID = ?
                ORDER BY PlannedTime;
            ", row.PlannerID);

            foreach (var ev in events)
            {
                if (!Enum.TryParse<PlannerEventKind>(ev.EventKind, true, out var kind))
                    kind = PlannerEventKind.ScStepRep;

                planner.Events.Add(new PlannerEventModel
                {
                    PlannerEventId = ev.PlannerEventID,
                    PlannerId = ev.PlannerID,
                    EventKind = kind,
                    CardId = ev.CardID,
                    ScCardStepId = ev.ScCardStepID,
                    PlannedTime = ParseIsoDateTime(ev.PlannedTime),
                    PlannedCount = Math.Max(1, ev.PlannedCount)
                });
            }

            return planner;
        }

        private static string ToPlannerDateKey(DateTime plannerDate) =>
            plannerDate.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static void ValidatePlannerTasks(IEnumerable<PlannerTaskModel> tasks)
        {
            var ordered = tasks
                .OrderBy(t => t.PlannedStart)
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].PlannedEnd <= ordered[i].PlannedStart)
                    throw new InvalidOperationException("Planner task end time must be after start time.");

                if (i > 0 && ordered[i].PlannedStart < ordered[i - 1].PlannedEnd)
                    throw new InvalidOperationException("Planner task blocks cannot overlap.");
            }
        }

        private sealed class PlannerIdRow
        {
            public long PlannerID { get; set; }
        }

        private sealed class PlannerRow
        {
            public long PlannerID { get; set; }
            public string PlannerDate { get; set; } = "";
        }

        private sealed class PlannerTaskRow
        {
            public long PlannerTaskID { get; set; }
            public long PlannerID { get; set; }
            public long CardID { get; set; }
            public string CardKind { get; set; } = "";
            public string PlannedStart { get; set; } = "";
            public string PlannedEnd { get; set; } = "";
        }

        private sealed class PlannerEventRow
        {
            public long PlannerEventID { get; set; }
            public long PlannerID { get; set; }
            public string EventKind { get; set; } = "";
            public long CardID { get; set; }
            public int? ScCardStepID { get; set; }
            public string PlannedTime { get; set; } = "";
            public int PlannedCount { get; set; }
        }

        #endregion

        #region Common Classes and Methods

        public async Task<string?> GetCardTitleByIdAsync(long cardId)
        {
            await InitializeAsync();

            var rows = await Db.QueryAsync<CardTitle>(
                @"SELECT  Title
                  FROM Card
                  WHERE CardId = ?",
                cardId);

            var row = rows.FirstOrDefault();
            return row?.Title ?? "";
        }

        public class CardTitle { public string Title { get; set; } };

        private static string? ToDbTimeOnly(TimeOnly? t) => t?.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        private static TimeOnly? ParseNullableTimeOnly(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;

            if (TimeOnly.TryParseExact(
                    s,
                    "HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var t))
                return t;

            return TimeOnly.Parse(s, CultureInfo.InvariantCulture);
        }

        private TimeSpan? StringToTimeSpan(string? estCompletionTimeText)
        {
            if (string.IsNullOrEmpty(estCompletionTimeText)) return null;

            var parts = estCompletionTimeText.Split(':');

            var hours = parts[0];
            var minutes = parts[1];
            var seconds = parts[2];

            int hoursInt = int.Parse(hours);
            var minutesInt = int.Parse(minutes);
            var secondsInt = int.Parse(seconds);

            return new TimeSpan(hoursInt, minutesInt, secondsInt);
        }

        private static DateTime ParseIsoDateTime(string value) => DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);

        public sealed class PragmaTableInfo
        {
            public int cid { get; set; }
            public string name { get; set; } = "";
            public string type { get; set; } = "";
            public int notnull { get; set; }
            public string? dflt_value { get; set; }
            public int pk { get; set; }
        }

        /// <summary>
        /// Helper to do: SELECT * FROM {table} WHERE {idColumn} IN (?,?,?) ORDER BY ...
        /// Avoids N+1 queries.
        /// </summary>
        private async Task<List<T>> QueryByIdsAsync<T>(
            string tableName,
            string idColumn,
            long[] ids,
            string selectColumns,
            string? orderBy = null)
            where T : new()
        {
            if (ids.Length == 0) return new List<T>();

            var placeholders = string.Join(",", Enumerable.Repeat("?", ids.Length));
            var sql = $@"SELECT {selectColumns}
                 FROM {tableName}
                 WHERE {idColumn} IN ({placeholders})
                 {(string.IsNullOrWhiteSpace(orderBy) ? "" : $"ORDER BY {orderBy}")};";

            object[] args = ids.Cast<object>().ToArray();
            return await Db.QueryAsync<T>(sql, args);
        }



        #endregion

        #endregion

        #region Write

        #region Card Save
        //Card
        public async Task SaveCardModelAsync(ICardModel model)
        {
            await SaveCardModelsAsync(new List<ICardModel>() { model });
        }

        public async Task SaveCardModelsAsync(List<ICardModel> models)
        {
            foreach (var model in models)
            {
                //Check if model has CardID and that CardID exists in the DB already
                long? cardId = await CheckForCardID(model);

                if (cardId == null)
                {
                    // Insert a base Card
                    await Db.ExecuteAsync("INSERT INTO Card (Title, Tags) VALUES (?, ?);", model.Title, model.Tags);

                    // Get the new CardID
                    cardId = await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                }
                else
                {
                    await Db.ExecuteAsync("UPDATE Card SET Title = ?, Tags = ? WHERE CardID = ?", model.Title, model.Tags, cardId);
                }

                model.CardID = cardId.Value;

                if (model is ScCardModel sc)
                {
                    await SaveScModelDataAsync(sc, cardId.Value);
                }
                else if (model is TatCardModel tat)
                {
                    await SaveTatModelDataAsync(tat, cardId.Value);
                }
                else if (model is MissionCardModel mcm)
                {
                    await SaveMissionCardModelDataAsync(mcm, cardId.Value);
                }
                else if (model is BudgetCardModel bcm)
                {
                    await SaveBudgetCardModelDataAsync(bcm, cardId.Value);
                }
                else if (model is AchievementCardModel acm)
                {
                    await SaveAchievementCardModelDataAsync(acm, cardId.Value);
                }
                else if (model is ValueTrackerCardModel vtc)
                {
                    await SaveValueTrackerCardModelDataAsync(vtc, cardId.Value);
                }
                else if (model is EventTrackerCardModel etc)
                {
                    await SaveEventTrackerCardModelDataAsync(etc, cardId.Value);
                }

            }
        }

        private async Task<long?> CheckForCardID(ICardModel model)
        {
            if (model is ScCardModel sc)
            {
                var ids = await Db.QueryScalarsAsync<long>("SELECT CardID FROM ScCard WHERE ScCardID = ? LIMIT 1", model.Id);

                return ids.FirstOrDefault() == 0 ? (long?)null : ids.First();

            }
            else if (model is TatCardModel tat)
            {
                var ids = await Db.QueryScalarsAsync<long>("SELECT CardID FROM TatCard WHERE TatCardID  = ? LIMIT 1", model.Id);

                return ids.FirstOrDefault() == 0 ? (long?)null : ids.First();
            }
            else if (model is MissionCardModel mcm)
            {
                var ids = await Db.QueryScalarsAsync<long>("SELECT CardID FROM MissionCard WHERE MissionCardID = ? LIMIT 1", model.Id);

                return ids.FirstOrDefault() == 0 ? (long?)null : ids.First();
            }
            else if (model is BudgetCardModel bcm)
            {
                var ids = await Db.QueryScalarsAsync<long>("SELECT CardID FROM BudgetCard WHERE BudgetCardID = ? LIMIT 1", model.Id);

                return ids.FirstOrDefault() == 0 ? (long?)null : ids.First();
            }
            else if (model is AchievementCardModel acm)
            {
                var ids = await Db.QueryScalarsAsync<long>("SELECT CardID FROM AchievementCard WHERE AchievementCardID = ? LIMIT 1", model.Id);

                return ids.FirstOrDefault() == 0 ? (long?)null : ids.First();
            }
            else if (model is ValueTrackerCardModel vtc)
            {
                var ids = await Db.QueryScalarsAsync<long>(
                    "SELECT CardID FROM ValueTrackerCard WHERE ValueTrackerCardID = ? LIMIT 1",
                    model.Id);

                return ids.FirstOrDefault() == 0 ? (long?)null : ids.First();
            }
            else if (model is EventTrackerCardModel etc)
            {
                var ids = await Db.QueryScalarsAsync<long>(
                    "SELECT CardID FROM EventTrackerCard WHERE EventTrackerCardID = ? LIMIT 1",
                    model.Id);

                return ids.FirstOrDefault() == 0 ? (long?)null : ids.First();
            }

            return null;
        }

        public async Task DeleteCardModelAsync(ICardModel model)
        {
            await InitializeAsync();

            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var cardId = model.CardID;
            if (cardId <= 0)
            {
                var resolvedCardId = await CheckForCardID(model);
                if (resolvedCardId == null)
                    return;

                cardId = resolvedCardId.Value;
            }

            await Db.RunInTransactionAsync(conn =>
            {
                conn.Execute("DELETE FROM Shortcut WHERE TargetCardId = ?;", cardId);
                conn.Execute("DELETE FROM NotificationLog WHERE CardId = ?;", cardId);
                conn.Execute("DELETE FROM CardSchedule WHERE CardId = ?;", cardId);
                conn.Execute("DELETE FROM LockTaskDependency WHERE TaskDependencyCardId = ?;", cardId);

                var lockIds = conn.QueryScalars<long>("SELECT LockId FROM Lock WHERE CardId = ?;", cardId);
                DeleteByIds(conn, "LockSchedule", "LockId", lockIds.ToList());
                DeleteByIds(conn, "LockTaskDependency", "LockId", lockIds.ToList());
                conn.Execute("DELETE FROM Lock WHERE CardId = ?;", cardId);

                conn.Execute("DELETE FROM Card WHERE CardID = ?;", cardId);
            });

            UdmdImageFileStore.TryDeleteCardFolder(cardId);

            model.Id = 0;
            model.CardID = 0;
        }

        #endregion

        #region Achievements
        public async Task SaveAchievementCardModelDataAsync(AchievementCardModel acm, long cardId)
        {
            await InitializeAsync();

            if(acm == null) throw new ArgumentNullException(nameof(acm));

            ValidateAchievementForPersistence(acm);

            // --- Common values ---
            var now = DateTime.Now;

            // Map enums to TEXT
            var targetTypeText = acm.TargetType.ToString();
            var difficultyText = acm.Difficulty.ToString();
            var completionTypeText = acm.CompletionType.ToString();

            // Target active time (only for ActiveTime target)
            int? targetActiveTimeSeconds = null;
            if (acm.TargetType == AchievementTargetType.ActiveTime)
            {
                // Uses your helper that parses ActiveTimeTargetText "hh:mm:ss" to seconds
                var seconds = acm.GetTargetSecondsSpent();
                targetActiveTimeSeconds = (int)Math.Round(seconds);
            }

            // Target value (only for Value / Steps / etc); safe to store whenever
            double? targetValue = null;
            if (acm.TargetType == AchievementTargetType.Value ||
                acm.TargetType == AchievementTargetType.Steps ||
                acm.TargetType == AchievementTargetType.Achievements ||
                acm.TargetType == AchievementTargetType.Custom)
            {
                targetValue = acm.TargetValue;
            }

            // Completion range fields (only meaningful in Range mode)
            string? rangeUnitText = null;
            int? rangeAmount = null;
            if (acm.CompletionType == AchievementCompletionType.Range)
            {
                rangeUnitText = acm.RangeUnit.ToString();
                rangeAmount = acm.RangeAmount;
            }

            // Deadline (only really meaningful in Deadline mode, but harmless to store when set)
            var deadlineStartText = acm.DeadlineStart?.ToString("o");
            var deadlineText = acm.Deadline?.ToString("o");

            var finalizedAtText = acm.FinalizedAt?.ToString("o");
            double? frozenCurrentValue = acm.FrozenCurrentValue;

            // LastEarnedAt (nullable)
            var lastEarnedAtText = acm.LastEarnedAt?.ToString("o");

            // For now, still persist trophies as newline-separated URLs/paths in TrophyURLs
            var trophyUrls = acm.Trophies.Count == 0
                ? ""
                : string.Join("\n",
                    acm.Trophies
                       .Where(t => !string.IsNullOrWhiteSpace(t))
                       .Select(t => t.Trim()));
           

            if (acm.Id == 0)
            {
                // INSERT
                await Db.ExecuteAsync(
                    @"INSERT INTO AchievementCard
                      (CardID,
                       Status,
                       Description,
                       TargetType,
                       DifficultyLevel,
                       CreatedDate,
                       LastEarnedAt,
                       TargetActiveTimeInSeconds,
                       TargetValue,
                       ScCardStepID,
                       CompletionType,
                       RangeUnit,
                       RangeAmount,
                       DeadlineStart,
                       Deadline,
                        FinalizedAt,
                        FrozenCurrentValue,
                       TrophyURLs,
                       IsPinned)
                      VALUES
                      (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);",
                    cardId,
                    acm.Status ?? "",
                    acm.Description ?? "",
                    targetTypeText,
                    difficultyText,
                    (acm.CreatedDate == default ? now : acm.CreatedDate).ToString("o"),
                    lastEarnedAtText,
                    targetActiveTimeSeconds,
                    targetValue,
                    null,                       // ScCardStepID - model does not expose a step ID yet
                    completionTypeText,
                    rangeUnitText,
                    rangeAmount,
                    deadlineStartText,
                    deadlineText,
                    finalizedAtText,
                    frozenCurrentValue,
                    trophyUrls,
                    acm.IsPinned ? 1 : 0
                );

                acm.Id = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            }
            else
            {
                // UPDATE - leave CreatedDate alone
                await Db.ExecuteAsync(
                    @"UPDATE AchievementCard
                      SET Status                   = ?,
                          Description              = ?,
                          TargetType                 = ?,
                          DifficultyLevel          = ?,
                          LastEarnedAt             = ?,
                          TargetActiveTimeInSeconds= ?,
                          TargetValue              = ?,
                          ScCardStepID             = ?,
                          CompletionType           = ?,
                          RangeUnit                = ?,
                          RangeAmount              = ?,
                          DeadlineStart            = ?,   
                          Deadline                 = ?,
                          FinalizedAt              = ?,
                          FrozenCurrentValue       = ?,
                          TrophyURLs               = ?,
                          IsPinned                 = ?
                      WHERE CardID = ?;",
                    acm.Status ?? "",
                    acm.Description ?? "",
                    targetTypeText,
                    difficultyText,
                    lastEarnedAtText,
                    targetActiveTimeSeconds,
                    targetValue,
                    null,                       // ScCardStepID – still null for now
                    completionTypeText,
                    rangeUnitText,
                    rangeAmount,
                    deadlineStartText,
                    deadlineText,
                    finalizedAtText,
                    frozenCurrentValue,
                    trophyUrls,
                    acm.IsPinned ? 1 : 0,
                    cardId
                );
            }
        }

        public async Task MarkAchievementEarnedAsync(long achievementId, DateTime earnedAtUtc)
        {
            await InitializeAsync();

            var earnedIso = earnedAtUtc.Kind == DateTimeKind.Utc
                ? earnedAtUtc.ToString("o", CultureInfo.InvariantCulture)
                : earnedAtUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

            await Db.RunInTransactionAsync(tran =>
            {
                tran.Execute(
                    @"UPDATE AchievementCard
                      SET LastEarnedAt = ?
                      WHERE AchievementCardID = ?;",
                    earnedIso,
                    achievementId
                );

                // Try to award ONE trophy. If none eligible, do nothing.
                TryAwardRandomTrophyInTransaction(tran, achievementId, earnedIso);
            });
        }

        [Table("AchievementTrophy")]
        public sealed class AchievementTrophyRow
        {
            [PrimaryKey, AutoIncrement]
            public long TrophyID { get; set; }

            [Indexed]
            public long AchievementCardID { get; set; }

            public string Title { get; set; } = "";

            // stored as ISO-8601 text
            public string EarnedOn { get; set; } = "";

            // I recommend storing just the file name (e.g. "Bulbasaur.png")
            // and combining with AppPaths.GetAchievementTrophiesPath(id) at render time.
            public string ImageSource { get; set; } = "";
        }

        private void TryAwardRandomTrophyInTransaction(SQLiteConnection tran, long achievementId, string earnedIso)
        {
            // 1) Get earnable files on disk
            var folder = Points.Global.AppPaths.GetAchievementTrophiesPath((int)achievementId);
            if (!Directory.Exists(folder)) return;

            var earnableFileNames = Directory.EnumerateFiles(folder)
                .Select(Path.GetFileName)
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .ToList();

            if (earnableFileNames.Count == 0) return;

            // 2) Get already-earned files from DB
            var earned = tran.Query<AchievementTrophyRow>(
                    @"SELECT TrophyID, AchievementCardID, Title, EarnedOn, ImageSource
                      FROM AchievementTrophy
                      WHERE AchievementCardID = ?;",
                    achievementId)
                .Select(x => x.ImageSource)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 3) Build eligible candidates:
            //    - not already earned
            //    - prerequisite rule satisfied
            var earnableSet = earnableFileNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var candidates = earnableFileNames
                .Where(f => !earned.Contains(f))
                .Where(f => PrerequisiteSatisfied(f, earnableSet, earned))
                .ToList();

            if (candidates.Count == 0) return;

            // 4) Pick randomly
            var idx = RandomNumberGenerator.GetInt32(candidates.Count);
            var chosen = candidates[idx];

            // 5) Insert row
            var title = Path.GetFileNameWithoutExtension(chosen) ?? "";

            tran.Execute(
                @"INSERT INTO AchievementTrophy (AchievementCardID, Title, EarnedOn, ImageSource)
                  VALUES (?, ?, ?, ?);",
                achievementId,
                title,
                earnedIso,
                chosen
            );
        }

        private static bool PrerequisiteSatisfied(string fileName, HashSet<string> earnableFiles, HashSet<string> earnedFiles)
        {
            // Find the *longest* suffix that:
            //  - starts immediately after an underscore
            //  - is itself an earnable file name
            //
            // If found, that suffix must already be earned.
            // If none found, no prerequisite.

            string? prerequisite = null;

            for (int i = 0; i < fileName.Length; i++)
            {
                if (fileName[i] != '_') continue;

                var suffix = fileName.Substring(i + 1); // everything after this underscore
                if (string.IsNullOrWhiteSpace(suffix)) continue;

                if (!earnableFiles.Contains(suffix)) continue;

                // prefer the longest (most specific) prerequisite
                if (prerequisite == null || suffix.Length > prerequisite.Length)
                    prerequisite = suffix;
            }

            if (prerequisite == null)
                return true; // no prereq pattern found

            return earnedFiles.Contains(prerequisite);
        }

        private static void ValidateAchievementForPersistence(AchievementCardModel acm)
        {
            if (acm == null)
                throw new ArgumentNullException(nameof(acm));

            if (acm.CompletionType == AchievementCompletionType.Deadline)
            {
                if (!acm.Deadline.HasValue)
                    throw new InvalidOperationException(
                        "Deadline achievements must have a deadline.");

                var effectiveStart = acm.DeadlineStart ?? acm.CreatedDate;

                if (effectiveStart > acm.Deadline.Value)
                    throw new InvalidOperationException(
                        "DeadlineStart cannot be later than Deadline.");
            }
        }


        private enum DeadlineTransitionResult
        {
            None = 0,
            Complete = 1,
            Fail = 2
        }

        private static DateTime StartOfTodayLocal() => DateTime.Now.Date;

        private static DateTime EndOfTodayLocal() => DateTime.Now.Date.AddDays(1);

        private static bool ShouldKeepLoadedAfterFinalization(AchievementCardModel card, DateTime now)
        {
            if (card == null)
                return false;

            if (card.CompletionType != AchievementCompletionType.Deadline)
                return true;

            if (!card.FinalizedAt.HasValue)
                return true;

            var todayStart = now.Date;
            var tomorrowStart = todayStart.AddDays(1);

            return card.FinalizedAt.Value >= todayStart && card.FinalizedAt.Value < tomorrowStart;
        }

        private static DeadlineTransitionResult GetDeadlineTransitionResult(AchievementCardModel card, double currentValue, DateTime now)
        {
            if (card == null)
                throw new ArgumentNullException(nameof(card));

            if (card.CompletionType != AchievementCompletionType.Deadline)
                return DeadlineTransitionResult.None;

            // Already finalized => inert
            if (card.FinalizedAt.HasValue)
                return DeadlineTransitionResult.None;

            // If the evaluation window is not valid, do nothing here.
            if (!card.TryGetEvaluationWindow(now, out var start, out var end))
                return DeadlineTransitionResult.None;

            // Pending: start is in the future
            if (start > now)
                return DeadlineTransitionResult.None;

            // Completion always wins if we are at/over target within the effective window.
            if (currentValue >= card.TargetValue && card.TargetValue > 0)
                return DeadlineTransitionResult.Complete;

            // If the real deadline has elapsed and target was not reached, fail.
            if (card.Deadline.HasValue && now > card.Deadline.Value)
                return DeadlineTransitionResult.Fail;

            return DeadlineTransitionResult.None;
        }

        public async Task FinalizeDeadlineAchievementCompletedAsync(long achievementId, double frozenCurrentValue, DateTime finalizedAtLocal)
        {
            await InitializeAsync();

            var finalizedIso = finalizedAtLocal.ToString("o", CultureInfo.InvariantCulture);

            await Db.RunInTransactionAsync(tran =>
            {
                tran.Execute(
                    @"UPDATE AchievementCard
                              SET Status = ?,
                                  FinalizedAt = ?,
                                  FrozenCurrentValue = ?,
                                  LastEarnedAt = ?
                              WHERE AchievementCardID = ?;",
                    "Completed",
                    finalizedIso,
                    frozenCurrentValue,
                    finalizedIso,
                    achievementId
                );

                TryAwardRandomTrophyInTransaction(tran, achievementId, finalizedIso);
            });
        }

        public async Task FinalizeDeadlineAchievementFailedAsync(long achievementId, double frozenCurrentValue, DateTime finalizedAtLocal)
        {
            await InitializeAsync();

            var finalizedIso = finalizedAtLocal.ToString("o", CultureInfo.InvariantCulture);

            await Db.RunInTransactionAsync(tran =>
            {
                tran.Execute(
                    @"UPDATE AchievementCard
                              SET Status = ?,
                                  FinalizedAt = ?,
                                  FrozenCurrentValue = ?
                              WHERE AchievementCardID = ?;",
                    "Failed",
                    finalizedIso,
                    frozenCurrentValue,
                    achievementId
                );
            });
        }

        private async Task<AchievementCardModel> ReloadAchievementAfterFinalizationAsync(long achievementId)
        {
            return await GetAchievementCardModelDataAsync((int)achievementId);
        }

        private async Task<AchievementCardModel> FinalizeDeadlineAchievementIfNeededAsync(AchievementCardModel card)
        {
            if (card == null)
                throw new ArgumentNullException(nameof(card));

            if (card.CompletionType != AchievementCompletionType.Deadline)
                return card;

            // Already finalized => nothing to do
            if (card.FinalizedAt.HasValue)
                return card;

            var now = DateTime.Now;

            if (!card.TryGetEvaluationWindow(now, out var windowStart, out var windowEnd))
                return card;

            double currentValue;

            switch (card.TargetType)
            {
                case AchievementTargetType.Value:
                    {
                        var summary = await GetTagValueSummaryAsync(card.Tags, windowStart, windowEnd);
                        currentValue = summary.CurrentValue;
                        break;
                    }

                case AchievementTargetType.ActiveTime:
                    {
                        var summary = await GetTagValueSummaryAsync(card.Tags, windowStart, windowEnd);
                        currentValue = summary.CurrentTotalActiveTimeInSeconds;
                        break;
                    }

                default:
                    // This finalization engine is only intended for the currently-supported
                    // deadline-capable goal types. Others stay unchanged for now.
                    return card;
            }

            var transition = GetDeadlineTransitionResult(card, currentValue, now);

            switch (transition)
            {
                case DeadlineTransitionResult.Complete:
                    await FinalizeDeadlineAchievementCompletedAsync(card.Id, currentValue, now);
                    return await ReloadAchievementAfterFinalizationAsync(card.Id);

                case DeadlineTransitionResult.Fail:
                    await FinalizeDeadlineAchievementFailedAsync(card.Id, currentValue, now);
                    return await ReloadAchievementAfterFinalizationAsync(card.Id);

                default:
                    return card;
            }
        }

        private async Task<List<AchievementCardModel>> FinalizeDeadlineAchievementsIfNeededAsync(IEnumerable<AchievementCardModel> cards)
        {
            if (cards == null)
                return new List<AchievementCardModel>();

            var result = new List<AchievementCardModel>();

            foreach (var card in cards)
            {
                var updated = await FinalizeDeadlineAchievementIfNeededAsync(card);
                result.Add(updated);
            }

            return result;
        }

        #endregion

        #region Budgets

        //Budget
        private async Task SaveBudgetCardModelDataAsync(BudgetCardModel model, long cardId)
        {
            if (model.Id == 0)
            {
                // Insert the “typed” row (e.g. ScCard)
                await Db.ExecuteAsync(
                    "INSERT INTO BudgetCard (CardID, Status, Description, Currency, ExchangeRate, StartDate, InitialBalance) VALUES (?, ?, ?, ?, ?, ?, ?);",
                    cardId, model.Status, model.Description, model.Currency, model.ExchangeRate, model.StartDate.ToString("o"), model.InitialBalance);

                model.Id = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            }
            else
            {
                await Db.ExecuteAsync(
                    "UPDATE BudgetCard SET Status = ?, Description = ?, Currency = ?, ExchangeRate = ?, StartDate = ?, InitialBalance = ? WHERE CardID = ?",
                    model.Status, model.Description, model.Currency, model.ExchangeRate, model.StartDate.ToString("o"), model.InitialBalance, cardId);
            }

            //Do a query to get all of the ValueRates for this Tat in the datbase
            var existingTopUpsForThisBudgetModel = await Db.QueryAsync<BudgetCardScheduledTopUpRow>("SELECT * FROM BudgetCardScheduledTopUp WHERE BudgetCardID = ?", model.Id);

            foreach (var tu in model.TopUps)
            {
                if (tu.Id == 0)
                {
                    await Db.ExecuteAsync(
                        "INSERT INTO BudgetCardScheduledTopUp (BudgetCardID, Amount, TimeOfDaySeconds) VALUES (?, ?, ?);",
                        model.Id, tu.Amount, tu.TimeOfDay.TotalSeconds);

                    tu.Id = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                }
                else
                {
                    await Db.ExecuteAsync(
                        "UPDATE BudgetCardScheduledTopUp SET Amount = ?, TimeOfDaySeconds = ? WHERE BudgetCardScheduledTopUpID = ?",
                        tu.Amount, tu.TimeOfDay.TotalSeconds, tu.Id);

                    //Remove the ValueRate with this Id form the ValueRates list you got fomr the db
                    var tuToLeaveInDb = existingTopUpsForThisBudgetModel.FirstOrDefault(x => x.BudgetCardScheduledTopUpID == tu.Id);
                    if (tuToLeaveInDb != null) existingTopUpsForThisBudgetModel.Remove(tuToLeaveInDb);
                }
            }

            //For any remainging Value rates in the list, remove them form the db
            foreach (var vrToDelete in existingTopUpsForThisBudgetModel)
            {
                await Db.ExecuteAsync("DELETE FROM BudgetCardScheduledTopUp WHERE BudgetCardScheduledTopUpID = ?", vrToDelete.BudgetCardScheduledTopUpID);
            }

            foreach (var trans in model.Transactions)
            {
                if (trans.Id == 0)
                {
                    await Db.ExecuteAsync(
                        "INSERT INTO BudgetCardTransaction (BudgetCardID, Amount, Type, TimeStamp) VALUES (?, ?, ?, ?);",
                        model.Id, trans.CurrencyAmount, trans.Type, trans.Timestamp.ToString("o"));

                    trans.Id = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                }
                else
                {
                    await Db.ExecuteAsync(
                        "UPDATE BudgetCardTransaction SET Amount = ?, Type = ?, TimeStamp = ? WHERE BudgetCardTransactionID = ?",
                        trans.CurrencyAmount, trans.Type, trans.Timestamp.ToString("o"), trans.Id);
                }
            }
        }

        private sealed class BudgetCardScheduledTopUpRow
        {
            public int BudgetCardScheduledTopUpID { get; set; }
            public int BudgetCardID { get; set; }
            public double Amount { get; set; }
            public int TimeOfDaySeconds { get; set; }
        }


        #endregion

        #region SC Cards

        // This should Add a new entity to the ScCardStepRep table for the ScCardStepID
        public async Task AddRepForStep(int scCardStepID, DateTime repTime, double stepValue)
        {
            // Composite PK (ScCardStepID, TimeStamp) so use OR REPLACE to avoid crashing
            // if the same timestamp is re-used (e.g. rapid taps / same tick).
            await Db.ExecuteAsync(
                @"INSERT OR REPLACE INTO ScCardStepRep (ScCardStepID, TimeStamp, StepValue)  VALUES (?, ?, ?);",
                scCardStepID,
                repTime.ToString("o"),
                stepValue);
        }

        // This should remove the last rep before (or at) the datetime passed to the method
        // NOTE: your signature says `int ScCardStepRep` but the table has no RepID.
        // This must be the ScCardStepID.
        public async Task RemoveRepForStep(int scCardStepID, DateTime repTime)
        {
            // Find the latest rep at-or-before the provided time.
            var ts = await Db.ExecuteScalarAsync<string?>(
                @"SELECT TimeStamp
                  FROM ScCardStepRep
                  WHERE ScCardStepID = ?
                    AND TimeStamp <= ?
                  ORDER BY TimeStamp DESC
                  LIMIT 1;",
                scCardStepID,
                repTime.ToString("o"));

            if (string.IsNullOrWhiteSpace(ts))
                return;

            await Db.ExecuteAsync(
                @"DELETE FROM ScCardStepRep
                  WHERE ScCardStepID = ?
                    AND TimeStamp = ?;",
                scCardStepID,
                ts);
        }


        //SC
        private async Task SaveScModelDataAsync(ScCardModel model, long cardId)
        {
            if (model.Id == 0)
            {
                // Insert the “typed” row (e.g. ScCard)
                await Db.ExecuteAsync(
                    "INSERT INTO ScCard (CardID, Status, Description) VALUES (?, ?, ?);",
                    cardId, model.Status, model.Description);

                model.Id = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            }
            else
            {
                await Db.ExecuteAsync(
                    "UPDATE ScCard SET Status = ?, Description = ? WHERE CardID = ?",
                    model.Status, model.Description, cardId);
            }

            foreach (var step in model.Steps)
            {
                if (step.Id == 0)
                {
                    await Db.ExecuteAsync(
                        "INSERT INTO ScCardStep (ScCardID, SortOrder, Title, StepValue) VALUES (?, ?, ?, ?);",
                        model.Id, step.SortOrder, step.Title, step.StepValue);

                    step.Id = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                }
                else
                {
                    await Db.ExecuteAsync(
                        "UPDATE ScCardStep SET SortOrder = ?, Title = ?, StepValue = ? WHERE ScCardStepID = ?",
                        step.SortOrder, step.Title, step.StepValue, step.Id);
                }

                const string insertRepSql = @"INSERT OR IGNORE INTO ScCardStepRep (ScCardStepID, TimeStamp, StepValue) VALUES (?, ?, ?);";

                foreach (var rep in step.Reps)
                {
                    await Db.ExecuteAsync(insertRepSql, step.Id, rep.ToString("o"), step.StepValue);
                }

            }

            await SaveCardSchedules(cardId, model.Schedules);
        }

        #endregion

        #region Missions
        //Mission
        private async Task SaveMissionCardModelDataAsync(MissionCardModel model, long cardId)
        {
            // Convert nullable dates to ISO-8601 strings (or null)
            var createdDateText = model.CreatedDate.ToString("o");
            var availableFromText = model.AvailableFromDate.ToString("o");
            var dueDateText = model.DueDate.ToString("o");
            var completedDateText = model.CompletedDate?.ToString("o");
            var eventDateText = model.EventDate?.ToString("o");
            var estCompletionTimeText = model.EstCompletionTimeText ?? "";

            if (model.Id == 0)
            {
                await Db.ExecuteAsync(
                    @"INSERT INTO MissionCard
              (CardID,
               Status,
               Description,
               SubType,
               Value,
               CreatedDate,
               AvailableFromDate,
               DueDate,
               CompletedDate,
               EventDate,
               EstCompletionTimeText,
               IsFailed,
               ValuePerMinute)
              VALUES
              (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);",
                    cardId,
                    model.Status ?? "",
                    model.Description ?? "",
                    model.SubType.ToString(),
                    model.Value,
                    createdDateText,
                    availableFromText,
                    dueDateText,
                    completedDateText,
                    eventDateText,
                    estCompletionTimeText,
                    model.IsFailed ? 1 : 0,
                    model.ValuePerMinute
                );

                model.Id = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            }
            else
            {
                await Db.ExecuteAsync(
                    @"UPDATE MissionCard
              SET Status                 = ?,
                  Description            = ?,
                  SubType                = ?,
                  Value                  = ?,
                  AvailableFromDate      = ?,
                  DueDate                = ?,
                  CompletedDate          = ?,
                  EventDate              = ?,
                  EstCompletionTimeText  = ?,
                  IsFailed               = ?,
                  ValuePerMinute         = ?
              WHERE CardID = ?;",
                    model.Status ?? "",
                    model.Description ?? "",
                    model.SubType.ToString(),
                    model.Value,
                    availableFromText,
                    dueDateText,
                    completedDateText,
                    eventDateText,
                    estCompletionTimeText,
                    model.IsFailed ? 1 : 0,
                    model.ValuePerMinute,
                    cardId
                );
            }
        }


        #endregion

        #region TAT Cards

        //TAT
        private async Task SaveTatModelDataAsync(TatCardModel model, long cardId)
        {
            if (model.Id == 0)
            {
                // Insert the “typed” row (e.g. ScCard)
                await Db.ExecuteAsync(
                    "INSERT INTO TatCard (CardID, ValuePerMinute, Status, Description, TargetActiveTimeSeconds) VALUES (?, ?, ?, ?, ?);",
                    cardId, model.ValuePerMinute, model.Status, model.Description, (model.TargetActiveTime.HasValue ? model.TargetActiveTime.Value.TotalSeconds : null));

                model.Id = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            }
            else
            {
                await Db.ExecuteAsync(
                    "UPDATE TatCard SET ValuePerMinute = ?, Status = ?, Description = ?, TargetActiveTimeSeconds = ? WHERE CardID = ?",
                    model.ValuePerMinute, model.Status, model.Description, (model.TargetActiveTime.HasValue ? model.TargetActiveTime.Value.TotalSeconds : null), cardId);
            }

            //Do a query to get all of the ValueRates for this Tat in the datbase
            var existingValueRateForThisTatModel = await Db.QueryAsync<TatValueRateRow>("SELECT * FROM TatCardValueRate WHERE TatCardID = ?", model.Id);

            foreach (var vr in model.ValueRates)
            {
                if (vr.Id == 0)
                {
                    await Db.ExecuteAsync(
                        "INSERT INTO TatCardValueRate (TatCardID, RateName, ValuePerMinute) VALUES (?, ?, ?);", model.Id, vr.RateName, vr.ValuePerMinute);

                    vr.Id = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                }
                else
                {
                    await Db.ExecuteAsync(
                        "UPDATE TatCardValueRate SET TatCardID = ?, RateName = ?, ValuePerMinute = ? WHERE TatCardValueRateID = ?",
                        model.Id, vr.RateName, vr.ValuePerMinute, vr.Id);

                    //Remove the ValueRate with this Id form the ValueRates list you got fomr the db
                    var vrToLeaveInDb = existingValueRateForThisTatModel.FirstOrDefault(x => x.TatCardValueRateID == vr.Id);
                    if (vrToLeaveInDb != null) existingValueRateForThisTatModel.Remove(vrToLeaveInDb);
                }
            }

            //For any remainging Value rates in the list, remove them form the db
            foreach (var vrToDelete in existingValueRateForThisTatModel)
            {
                await Db.ExecuteAsync("DELETE FROM TatCardValueRate WHERE TatCardValueRateID = ?", vrToDelete.TatCardValueRateID);
            }

            await SaveCardSchedules(cardId, model.Schedules);
        }


        #endregion

        #region Trackers

        #region Value Trackers

        private async Task SaveValueTrackerCardModelDataAsync(ValueTrackerCardModel model, long cardId)
        {
            if (model.Id == 0)
            {
                await Db.ExecuteAsync(
                    @"INSERT INTO ValueTrackerCard
              (CardID, Unit, CreatedDate, RangeStart, ScheduleEvery, ScheduleUnit)
              VALUES (?, ?, ?, ?, ?, ?);",
                    cardId,
                    model.Unit ?? "",
                    model.CreatedDate.ToString("o"),
                    model.RangeStart.ToString("o"),
                    model.ScheduleEvery,
                    model.ScheduleUnit ?? "Week"
                );

                model.Id = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            }
            else
            {
                await Db.ExecuteAsync(
                    @"UPDATE ValueTrackerCard
              SET Unit = ?, CreatedDate = ?, RangeStart = ?, ScheduleEvery = ?, ScheduleUnit = ?
              WHERE CardID = ?;",
                    model.Unit ?? "",
                    model.CreatedDate.ToString("o"),
                    model.RangeStart.ToString("o"),
                    model.ScheduleEvery,
                    model.ScheduleUnit ?? "Week",
                    cardId
                );
            }

            await SaveCardSchedules(cardId, model.Schedules);

            await SaveTrackerValuesAsync(cardId, model.Values);
        }


        private async Task SaveTrackerValuesAsync(long cardId, IEnumerable<TrackerValueModel> values)
        {
            // Existing rows in DB for this card
            var existing = await Db.QueryAsync<TrackerValueRow>(
                "SELECT TrackerValueID AS TrackerValueID, CardID AS CardID, TimeStamp AS TimeStamp, Value AS Value FROM TrackerValue WHERE CardID = ?;",
                cardId);

            // Track what remains after processing (anything left gets deleted)
            var remaining = existing.ToList();

            foreach (var v in values)
            {
                if (v.Id == 0)
                {
                    await Db.ExecuteAsync(
                        @"INSERT INTO TrackerValue (CardID, TimeStamp, Value) VALUES (?, ?, ?);",
                        cardId,
                        v.Timestamp.ToString("o"),
                        v.Value
                    );

                    v.Id = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                }
                else
                {
                    await Db.ExecuteAsync(
                        @"UPDATE TrackerValue SET TimeStamp = ?, Value = ? WHERE TrackerValueID = ?;",
                        v.Timestamp.ToString("o"),
                        v.Value,
                        v.Id
                    );

                    var keep = remaining.FirstOrDefault(x => x.TrackerValueID == v.Id);
                    if (keep != null) remaining.Remove(keep);
                }
            }

            // Delete anything not present in the model anymore
            foreach (var toDelete in remaining)
            {
                await Db.ExecuteAsync("DELETE FROM TrackerValue WHERE TrackerValueID = ?;", toDelete.TrackerValueID);
            }
        }

        #endregion

        #region Event Trackers
        private async Task SaveEventTrackerCardModelDataAsync(EventTrackerCardModel model, long cardId)
        {
            if (model.Id == 0)
            {
                await Db.ExecuteAsync(
                    @"INSERT INTO EventTrackerCard
              (CardID, Unit, CreatedDate, RangeStart, GroupByPeriod)
              VALUES (?, ?, ?, ?, ?);",
                    cardId,
                    model.Unit ?? "",
                    model.CreatedDate.ToString("o"),
                    model.RangeStart.ToString("o"),
                    model.GroupByPeriod ?? "Day"
                );

                model.Id = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            }
            else
            {
                await Db.ExecuteAsync(
                    @"UPDATE EventTrackerCard
              SET Unit = ?, CreatedDate = ?, RangeStart = ?, GroupByPeriod = ?
              WHERE CardID = ?;",
                    model.Unit ?? "",
                    model.CreatedDate.ToString("o"),
                    model.RangeStart.ToString("o"),
                    model.GroupByPeriod ?? "Day",
                    cardId
                );
            }

            // Events are still persisted via the same TrackerValue table
            await SaveTrackerValuesAsync(cardId, model.Values);
        }


        #endregion

        #region UDMD

        public async Task<List<UdmdConfigModel>> GetUdmdConfigsForCardAsync(long cardId)
        {
            await InitializeAsync();

            return await Db.QueryAsync<UdmdConfigModel>(
                @"SELECT UdmdConfigID, CardID, FieldName, FieldType, IsRequired, DisplayOrder, IsActive
                  FROM UdmdConfig
                  WHERE CardID = ?
                  ORDER BY DisplayOrder, FieldName;",
                cardId);
        }

        public async Task<List<UdmdConfigModel>> GetActiveUdmdConfigsForCardAsync(long cardId)
        {
            await InitializeAsync();

            return await Db.QueryAsync<UdmdConfigModel>(
                @"SELECT UdmdConfigID, CardID, FieldName, FieldType, IsRequired, DisplayOrder, IsActive
                  FROM UdmdConfig
                  WHERE CardID = ?
                    AND IsActive = 1
                  ORDER BY DisplayOrder, FieldName;",
                cardId);
        }

        public async Task<UdmdConfigModel> SaveUdmdConfigAsync(UdmdConfigModel config)
        {
            await InitializeAsync();

            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (config.CardID <= 0)
                throw new InvalidOperationException("UDMD config must be attached to a saved card.");

            await EnsureUdmdCardExistsAsync(config.CardID);

            config.FieldName = (config.FieldName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(config.FieldName))
                throw new InvalidOperationException("UDMD field name is required.");

            config.FieldType = NormalizeUdmdFieldType(config.FieldType).ToString();

            if (config.UdmdConfigID == 0)
            {
                await Db.ExecuteAsync(
                    @"INSERT INTO UdmdConfig (CardID, FieldName, FieldType, IsRequired, DisplayOrder, IsActive)
                      VALUES (?, ?, ?, ?, ?, ?);",
                    config.CardID,
                    config.FieldName,
                    config.FieldType,
                    config.IsRequired ? 1 : 0,
                    config.DisplayOrder,
                    config.IsActive ? 1 : 0);

                config.UdmdConfigID = await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            }
            else
            {
                await Db.ExecuteAsync(
                    @"UPDATE UdmdConfig
                      SET CardID = ?,
                          FieldName = ?,
                          FieldType = ?,
                          IsRequired = ?,
                          DisplayOrder = ?,
                          IsActive = ?
                      WHERE UdmdConfigID = ?;",
                    config.CardID,
                    config.FieldName,
                    config.FieldType,
                    config.IsRequired ? 1 : 0,
                    config.DisplayOrder,
                    config.IsActive ? 1 : 0,
                    config.UdmdConfigID);
            }

            return config;
        }

        public async Task DeleteOrDeactivateUdmdConfigAsync(long udmdConfigId)
        {
            await InitializeAsync();

            await Db.ExecuteAsync(
                "UPDATE UdmdConfig SET IsActive = 0 WHERE UdmdConfigID = ?;",
                udmdConfigId);
        }

        public async Task<List<UdmdDropdownModel>> GetDropdownValuesAsync(long udmdConfigId)
        {
            await InitializeAsync();

            return await Db.QueryAsync<UdmdDropdownModel>(
                @"SELECT UdmdDropdownID, UdmdConfigID, DropdownValue, DisplayOrder, IsActive
                  FROM UdmdDropdown
                  WHERE UdmdConfigID = ?
                    AND IsActive = 1
                  ORDER BY DisplayOrder, DropdownValue;",
                udmdConfigId);
        }

        public async Task SaveDropdownValuesAsync(long udmdConfigId, IEnumerable<string> values)
        {
            await InitializeAsync();

            var config = await GetUdmdConfigByIdAsync(udmdConfigId);
            if (config == null)
                throw new InvalidOperationException("UDMD dropdown config was not found.");

            var normalizedValues = (values ?? Enumerable.Empty<string>())
                .Select(v => (v ?? "").Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existing = await Db.QueryAsync<UdmdDropdownModel>(
                @"SELECT UdmdDropdownID, UdmdConfigID, DropdownValue, DisplayOrder, IsActive
                  FROM UdmdDropdown
                  WHERE UdmdConfigID = ?;",
                udmdConfigId);

            await Db.RunInTransactionAsync(conn =>
            {
                for (var i = 0; i < normalizedValues.Count; i++)
                {
                    var value = normalizedValues[i];
                    var match = existing.FirstOrDefault(x =>
                        string.Equals(x.DropdownValue, value, StringComparison.OrdinalIgnoreCase));

                    if (match == null)
                    {
                        conn.Execute(
                            @"INSERT INTO UdmdDropdown (UdmdConfigID, DropdownValue, DisplayOrder, IsActive)
                              VALUES (?, ?, ?, 1);",
                            udmdConfigId,
                            value,
                            i);
                    }
                    else
                    {
                        conn.Execute(
                            @"UPDATE UdmdDropdown
                              SET DropdownValue = ?, DisplayOrder = ?, IsActive = 1
                              WHERE UdmdDropdownID = ?;",
                            value,
                            i,
                            match.UdmdDropdownID);
                    }
                }

                foreach (var stale in existing.Where(x =>
                             !normalizedValues.Contains(x.DropdownValue, StringComparer.OrdinalIgnoreCase)))
                {
                    conn.Execute(
                        "UPDATE UdmdDropdown SET IsActive = 0 WHERE UdmdDropdownID = ?;",
                        stale.UdmdDropdownID);
                }
            });
        }

        public async Task SaveMetadataForEntityAsync(
            long cardId,
            string relatedEntityType,
            long relatedEntityId,
            IEnumerable<UdmdValueInput> values)
        {
            await InitializeAsync();

            if (!UdmdRelatedEntityTypes.IsSupported(relatedEntityType))
                throw new InvalidOperationException($"Unsupported UDMD related entity type: {relatedEntityType}");

            if (cardId <= 0)
                throw new InvalidOperationException("UDMD metadata must be attached to a saved card.");

            if (relatedEntityId <= 0)
                throw new InvalidOperationException("UDMD metadata must be attached to a saved parent row.");

            await EnsureUdmdCardExistsAsync(cardId);
            await EnsureUdmdRelatedParentExistsAsync(cardId, relatedEntityType, relatedEntityId);

            var configs = await GetUdmdConfigsForCardAsync(cardId);
            var configById = configs.ToDictionary(x => x.UdmdConfigID);
            var distinctInputs = new Dictionary<long, UdmdValueInput>();

            foreach (var input in values ?? Enumerable.Empty<UdmdValueInput>())
            {
                if (input == null)
                    continue;

                if (distinctInputs.ContainsKey(input.UdmdConfigID))
                    throw new InvalidOperationException("UDMD metadata contains duplicate values for the same field.");

                distinctInputs[input.UdmdConfigID] = input;
            }

            foreach (var required in configs.Where(x => x.IsActive && x.IsRequired))
            {
                if (!distinctInputs.TryGetValue(required.UdmdConfigID, out var input) ||
                    string.IsNullOrWhiteSpace(input.FieldValue))
                {
                    throw new InvalidOperationException($"Required metadata field '{required.FieldName}' is missing.");
                }
            }

            var normalizedRows = new List<(UdmdConfigModel Config, string FieldValue)>();

            foreach (var input in distinctInputs.Values)
            {
                if (!configById.TryGetValue(input.UdmdConfigID, out var config))
                    throw new InvalidOperationException("UDMD metadata references a field that does not belong to this card.");

                if (!config.IsActive)
                    throw new InvalidOperationException($"UDMD field '{config.FieldName}' is inactive.");

                if (string.IsNullOrWhiteSpace(input.FieldValue))
                    continue;

                var normalizedValue = await NormalizeUdmdFieldValueAsync(config, input.FieldValue);
                normalizedRows.Add((config, normalizedValue));
            }

            if (normalizedRows.Count == 0)
                return;

            await Db.RunInTransactionAsync(conn =>
            {
                foreach (var row in normalizedRows)
                {
                    var existingId = conn.ExecuteScalar<long>(
                        @"SELECT UdmdTransID
                          FROM UdmdTrans
                          WHERE RelatedEntityType = ?
                            AND RelatedEntityId = ?
                            AND UdmdConfigID = ?
                          LIMIT 1;",
                        relatedEntityType,
                        relatedEntityId,
                        row.Config.UdmdConfigID);

                    if (existingId > 0)
                    {
                        conn.Execute(
                            @"UPDATE UdmdTrans
                              SET CardID = ?, FieldValue = ?
                              WHERE UdmdTransID = ?;",
                            cardId,
                            row.FieldValue,
                            existingId);
                    }
                    else
                    {
                        conn.Execute(
                            @"INSERT INTO UdmdTrans (CardID, UdmdConfigID, RelatedEntityType, RelatedEntityId, FieldValue)
                              VALUES (?, ?, ?, ?, ?);",
                            cardId,
                            row.Config.UdmdConfigID,
                            relatedEntityType,
                            relatedEntityId,
                            row.FieldValue);
                    }
                }
            });
        }

        public async Task<List<UdmdTransModel>> GetMetadataForEntityAsync(string relatedEntityType, long relatedEntityId)
        {
            await InitializeAsync();

            if (!UdmdRelatedEntityTypes.IsSupported(relatedEntityType))
                throw new InvalidOperationException($"Unsupported UDMD related entity type: {relatedEntityType}");

            return await Db.QueryAsync<UdmdTransModel>(
                @"SELECT t.UdmdTransID,
                         t.CardID,
                         t.UdmdConfigID,
                         t.RelatedEntityType,
                         t.RelatedEntityId,
                         t.FieldValue,
                         c.FieldName,
                         c.FieldType
                  FROM UdmdTrans t
                  JOIN UdmdConfig c ON c.UdmdConfigID = t.UdmdConfigID
                  WHERE t.RelatedEntityType = ?
                    AND t.RelatedEntityId = ?
                  ORDER BY c.DisplayOrder, c.FieldName;",
                relatedEntityType,
                relatedEntityId);
        }

        public async Task<List<UdmdTransModel>> GetMetadataForCardAsync(long cardId)
        {
            await InitializeAsync();

            return await Db.QueryAsync<UdmdTransModel>(
                @"SELECT t.UdmdTransID,
                         t.CardID,
                         t.UdmdConfigID,
                         t.RelatedEntityType,
                         t.RelatedEntityId,
                         t.FieldValue,
                         c.FieldName,
                         c.FieldType
                  FROM UdmdTrans t
                  JOIN UdmdConfig c ON c.UdmdConfigID = t.UdmdConfigID
                  WHERE t.CardID = ?
                  ORDER BY t.RelatedEntityType, t.RelatedEntityId, c.DisplayOrder, c.FieldName;",
                cardId);
        }

        public Task SaveActivityMetadataAsync(long cardId, long activityId, IEnumerable<UdmdValueInput> values) =>
            SaveMetadataForEntityAsync(cardId, UdmdRelatedEntityTypes.Activity, activityId, values);

        public Task SaveBudgetTransactionMetadataAsync(long cardId, long budgetTransactionId, IEnumerable<UdmdValueInput> values) =>
            SaveMetadataForEntityAsync(cardId, UdmdRelatedEntityTypes.BudgetTransaction, budgetTransactionId, values);

        public Task SaveTrackerValueMetadataAsync(long cardId, long trackerValueId, IEnumerable<UdmdValueInput> values) =>
            SaveMetadataForEntityAsync(cardId, UdmdRelatedEntityTypes.TrackerValue, trackerValueId, values);

        private async Task EnsureUdmdCardExistsAsync(long cardId)
        {
            var exists = await Db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Card WHERE CardID = ?;",
                cardId);

            if (exists <= 0)
                throw new InvalidOperationException("UDMD card was not found.");
        }

        private async Task<UdmdConfigModel?> GetUdmdConfigByIdAsync(long udmdConfigId)
        {
            return (await Db.QueryAsync<UdmdConfigModel>(
                    @"SELECT UdmdConfigID, CardID, FieldName, FieldType, IsRequired, DisplayOrder, IsActive
                      FROM UdmdConfig
                      WHERE UdmdConfigID = ?
                      LIMIT 1;",
                    udmdConfigId))
                .FirstOrDefault();
        }

        private static UdmdFieldType NormalizeUdmdFieldType(string? fieldType)
        {
            return Enum.TryParse<UdmdFieldType>(fieldType ?? "", true, out var parsed)
                ? parsed
                : UdmdFieldType.Text;
        }

        private async Task<string> NormalizeUdmdFieldValueAsync(UdmdConfigModel config, string rawValue)
        {
            var value = (rawValue ?? "").Trim();
            var fieldType = NormalizeUdmdFieldType(config.FieldType);

            switch (fieldType)
            {
                case UdmdFieldType.Dropdown:
                    var dropdowns = await GetDropdownValuesAsync(config.UdmdConfigID);
                    var match = dropdowns.FirstOrDefault(d =>
                        string.Equals(d.DropdownValue, value, StringComparison.Ordinal));

                    if (match == null)
                        throw new InvalidOperationException($"'{value}' is not an allowed value for '{config.FieldName}'.");

                    return match.DropdownValue;

                case UdmdFieldType.Number:
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantNumber) &&
                        !double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out invariantNumber))
                    {
                        throw new InvalidOperationException($"'{config.FieldName}' must be a number.");
                    }

                    return invariantNumber.ToString("G17", CultureInfo.InvariantCulture);

                case UdmdFieldType.Date:
                    if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateValue) &&
                        !DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out dateValue))
                    {
                        throw new InvalidOperationException($"'{config.FieldName}' must be a date.");
                    }

                    return dateValue.ToString("o", CultureInfo.InvariantCulture);

                case UdmdFieldType.Boolean:
                    if (TryParseUdmdBoolean(value, out var boolValue))
                        return boolValue ? "true" : "false";

                    throw new InvalidOperationException($"'{config.FieldName}' must be true or false.");

                case UdmdFieldType.Image:
                    if (!UdmdImageFileStore.IsSafeStoredFileName(value))
                        throw new InvalidOperationException($"'{config.FieldName}' must be a stored image filename.");

                    if (!UdmdImageFileStore.ImageExists(config.CardID, value))
                        throw new InvalidOperationException($"The image for '{config.FieldName}' could not be found.");

                    return value;

                case UdmdFieldType.Text:
                default:
                    return value;
            }
        }

        private static bool TryParseUdmdBoolean(string value, out bool result)
        {
            if (bool.TryParse(value, out result))
                return true;

            switch ((value ?? "").Trim().ToLowerInvariant())
            {
                case "1":
                case "yes":
                case "y":
                    result = true;
                    return true;
                case "0":
                case "no":
                case "n":
                    result = false;
                    return true;
                default:
                    result = false;
                    return false;
            }
        }

        private async Task EnsureUdmdRelatedParentExistsAsync(long cardId, string relatedEntityType, long relatedEntityId)
        {
            var count = relatedEntityType switch
            {
                UdmdRelatedEntityTypes.Activity => await Db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM Activity WHERE ActivityID = ? AND CardID = ?;",
                    relatedEntityId,
                    cardId),

                UdmdRelatedEntityTypes.BudgetTransaction => await Db.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(*)
                      FROM BudgetCardTransaction t
                      JOIN BudgetCard b ON b.BudgetCardID = t.BudgetCardID
                      WHERE t.BudgetCardTransactionID = ?
                        AND b.CardID = ?;",
                    relatedEntityId,
                    cardId),

                UdmdRelatedEntityTypes.TrackerValue => await Db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM TrackerValue WHERE TrackerValueID = ? AND CardID = ?;",
                    relatedEntityId,
                    cardId),

                _ => 0
            };

            if (count <= 0)
                throw new InvalidOperationException("UDMD related parent row was not found for this card.");
        }

        #endregion

        #endregion

        #region Reports

        // -------------------------
        // Reports / Ad-hoc SQL
        // -------------------------

        /// <summary>
        /// Executes an arbitrary SELECT (or WITH...SELECT) and returns the result set as display lines
        /// suitable for the ReportDetailsPage Results CollectionView (1 string per row).
        /// </summary>
        public async Task<IReadOnlyList<string>> ExecuteSelectForReportAsync(string sql, bool includeHeaderRow = true, params object?[] args)
        {
            await InitializeAsync();

            if (string.IsNullOrWhiteSpace(sql))
                return Array.Empty<string>();

            var trimmed = sql.TrimStart();
            if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only SELECT statements are allowed.");
            }

            return await Task.Run(() =>
            {
                sqlite3? db = null;
                sqlite3_stmt? stmt = null;

                try
                {
                    var rc = raw.sqlite3_open_v2(
                        _dbPath,
                        out db,
                        raw.SQLITE_OPEN_READONLY,
                        null);

                    if (rc != raw.SQLITE_OK || db == null)
                        throw new InvalidOperationException($"Failed to open SQLite database. rc={rc}");

                    rc = raw.sqlite3_prepare_v2(db, sql, out stmt);
                    if (rc != raw.SQLITE_OK || stmt == null)
                        throw new InvalidOperationException($"sqlite3_prepare_v2 failed. rc={rc}. {raw.sqlite3_errmsg(db).utf8_to_string()}");

                    // Bind params (?, ?, ?)
                    if (args is { Length: > 0 })
                    {
                        for (int i = 0; i < args.Length; i++)
                            BindParameter(stmt, i + 1, args[i]);
                    }

                    var results = new List<string>();
                    int colCount = raw.sqlite3_column_count(stmt);

                    if (includeHeaderRow && colCount > 0)
                    {
                        var headers = new string[colCount];
                        for (int c = 0; c < colCount; c++)
                        {
                            var name = raw.sqlite3_column_name(stmt, c).utf8_to_string();
                            headers[c] = string.IsNullOrEmpty(name) ? $"Col{c + 1}" : name;
                        }
                        results.Add(string.Join(" | ", headers));
                    }

                    while (true)
                    {
                        rc = raw.sqlite3_step(stmt);

                        if (rc == raw.SQLITE_ROW)
                        {
                            var row = new string[colCount];
                            for (int c = 0; c < colCount; c++)
                                row[c] = ReadColumnAsText(stmt, c);

                            results.Add(string.Join(" | ", row));
                            continue;
                        }

                        if (rc == raw.SQLITE_DONE)
                            break;

                        throw new InvalidOperationException($"sqlite3_step failed. rc={rc}. {raw.sqlite3_errmsg(db).utf8_to_string()}");
                    }

                    if (results.Count == 0)
                        results.Add("(no rows)");

                    return (IReadOnlyList<string>)results;
                }
                finally
                {
                    if (stmt != null) raw.sqlite3_finalize(stmt);
                    if (db != null) raw.sqlite3_close(db);
                }
            });
        }


        #endregion

        #region Card Schedules

        //Card Schedules
        private async Task SaveCardSchedules(long cardId, ObservableCollection<CardSchedule> schedules)
        {
            await InitializeAsync();

            // Existing rows in DB for this card
            var existing = await Db.QueryAsync<CardScheduleRow>(
                @"SELECT
                      ScheduleID   AS ScheduleID,
                      CardID       AS CardID,
                      FrequencyType AS FrequencyType,
                      FrequencyValue AS FrequencyValue,
                      FromDateTime AS FromDateTime,
                      ToDateTime   AS ToDateTime,
                      IsEnabled    AS IsEnabled,
                      Note         AS Note
                  FROM CardSchedule
                  WHERE CardID = ?;",
                cardId);

            var remaining = existing.ToList();

            foreach (var s in schedules)
            {
                // Ensure FK is correct
                s.CardId = cardId;

                var ftText = s.FrequencyType.ToString();
                var toIso = s.ToDateTime.HasValue ? ToIso(s.ToDateTime.Value) : null;

                if (s.ScheduleId == 0)
                {
                    await Db.ExecuteAsync(
                        @"INSERT INTO CardSchedule
                          (CardID, FrequencyType, FrequencyValue, FromDateTime, ToDateTime, IsEnabled, Note)
                          VALUES (?, ?, ?, ?, ?, ?, ?);",
                        cardId,
                        ftText,
                        s.FrequencyValue,
                        ToIso(s.FromDateTime),
                        toIso,
                        s.IsEnabled ? 1 : 0,
                        s.Note ?? ""
                    );

                    s.ScheduleId = await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                }
                else
                {
                    await Db.ExecuteAsync(
                        @"UPDATE CardSchedule
                          SET FrequencyType = ?,
                              FrequencyValue = ?,
                              FromDateTime = ?,
                              ToDateTime = ?,
                              IsEnabled = ?,
                              Note = ?
                          WHERE ScheduleID = ? AND CardID = ?;",
                        ftText,
                        s.FrequencyValue,
                        ToIso(s.FromDateTime),
                        toIso,
                        s.IsEnabled ? 1 : 0,
                        s.Note ?? "",
                        s.ScheduleId,
                        cardId
                    );

                    var keep = remaining.FirstOrDefault(x => x.ScheduleID == s.ScheduleId);
                    if (keep != null) remaining.Remove(keep);
                }
            }

            // Delete any schedules removed in-memory
            foreach (var del in remaining)
            {
                await Db.ExecuteAsync(
                    @"DELETE FROM CardSchedule WHERE ScheduleID = ? AND CardID = ?;",
                    del.ScheduleID,
                    cardId);
            }
        }
        public sealed class CardScheduleRow
        {
            public long ScheduleID { get; set; }
            public long CardID { get; set; }
            public string FrequencyType { get; set; } = "";
            public int FrequencyValue { get; set; }
            public string FromDateTime { get; set; } = "";
            public string? ToDateTime { get; set; }
            public int IsEnabled { get; set; }
            public string Note { get; set; } = "";
        }
        public static class CardScheduleMapper
        {
            public static CardSchedule ToDomain(CardScheduleRow row)
            {
                return new CardSchedule
                {
                    ScheduleId = row.ScheduleID,
                    CardId = row.CardID,
                    IsEnabled = row.IsEnabled != 0,
                    Note = row.Note ?? "",
                    FrequencyType = (FrequencyType)Enum.Parse(typeof(FrequencyType), row.FrequencyType),
                    FrequencyValue = row.FrequencyValue,
                    FromDateTime = DateTime.Parse(row.FromDateTime, null, System.Globalization.DateTimeStyles.RoundtripKind),
                    ToDateTime = string.IsNullOrWhiteSpace(row.ToDateTime)
                        ? null
                        : DateTime.Parse(row.ToDateTime!, null, System.Globalization.DateTimeStyles.RoundtripKind),
                };
            }
        }

        #endregion

        #region Common Methods

        private static string ToIso(DateTime dt) => dt.ToString("o");

        private static DateTime ParseIso(string s) => DateTime.Parse(s, null, DateTimeStyles.RoundtripKind);

        private static void BindParameter(sqlite3_stmt stmt, int index, object? value)
        {
            if (value == null)
            {
                raw.sqlite3_bind_null(stmt, index);
                return;
            }

            switch (value)
            {
                case string s:
                    raw.sqlite3_bind_text(stmt, index, s);
                    return;

                case bool b:
                    raw.sqlite3_bind_int(stmt, index, b ? 1 : 0);
                    return;

                case byte or short or int or long:
                    raw.sqlite3_bind_int64(stmt, index, Convert.ToInt64(value, CultureInfo.InvariantCulture));
                    return;

                case float or double or decimal:
                    raw.sqlite3_bind_double(stmt, index, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                    return;

                case DateTime dt:
                    raw.sqlite3_bind_text(stmt, index, dt.ToString("o"));
                    return;

                default:
                    raw.sqlite3_bind_text(stmt, index, value.ToString() ?? "");
                    return;
            }
        }

        private static string ReadColumnAsText(sqlite3_stmt stmt, int colIndex)
        {
            var t = raw.sqlite3_column_type(stmt, colIndex);

            return t switch
            {
                raw.SQLITE_NULL => "NULL",
                raw.SQLITE_INTEGER => raw.sqlite3_column_int64(stmt, colIndex).ToString(CultureInfo.InvariantCulture),
                raw.SQLITE_FLOAT => raw.sqlite3_column_double(stmt, colIndex).ToString(CultureInfo.InvariantCulture),

                // NOTE: column_text may also be utf8z depending on package version; this works reliably:
                raw.SQLITE_TEXT => raw.sqlite3_column_text(stmt, colIndex).utf8_to_string() ?? "",

                raw.SQLITE_BLOB => $"[BLOB {raw.sqlite3_column_bytes(stmt, colIndex)} bytes]",
                _ => ""
            };
        }

        #endregion

        #region Goals

        public async Task SaveGoalModelsDataAsync(List<GoalDetailsModel> goalModelsToSave)
        {
            await InitializeAsync();

            if (goalModelsToSave == null)
                throw new ArgumentNullException(nameof(goalModelsToSave));

            // normalize + de-dupe by (CardId, TimeScope)
            var normalized = goalModelsToSave
                .Where(x => x != null)
                .GroupBy(x => new { x.CardId, x.TimeScope })
                .Select(g => g.First())
                .ToList();

            // If nothing passed, we can't know which scope to "mirror".
            // If you want "clear this scope", pass the scope explicitly (see overload below).
            if (normalized.Count == 0)
                return;

            // Expect a single TimeScope per save call (your described behavior)
            var scope = normalized[0].TimeScope;
            if (normalized.Any(x => x.TimeScope != scope))
                throw new InvalidOperationException("SaveGoalModelsDataAsync expects a single TimeScope per call.");

            await Db.RunInTransactionAsync(conn =>
            {
                // OPTIONAL: mirror semantics for THIS scope only:
                // remove rows in DB for (TimeScope == scope) whose CardID is no longer present in the incoming set.
                // If you do NOT want deletions at all, delete this whole block.
                {
                    conn.Execute("DROP TABLE IF EXISTS _GoalCardKeys;");
                    conn.Execute(@"
                                CREATE TEMP TABLE _GoalCardKeys
                                (
                                    CardID INTEGER NOT NULL PRIMARY KEY
                                );
                        ");

                    const string insertKeySql = @"INSERT OR IGNORE INTO _GoalCardKeys (CardID) VALUES (?);";
                    foreach (var m in normalized)
                        conn.Execute(insertKeySql, m.CardId);

                    conn.Execute(@"
                        DELETE FROM Goal
                        WHERE TimeScope = ?
                          AND NOT EXISTS (
                              SELECT 1
                              FROM _GoalCardKeys k
                              WHERE k.CardID = Goal.CardID
                          );
                    ", scope.ToString());

                    conn.Execute("DROP TABLE IF EXISTS _GoalCardKeys;");
                }

                // Upsert (CardID, TimeScope)
                const string upsertSql = @"
                    INSERT INTO Goal (CardID, TimeScope, GoalHrs, Enabled, DeFactoStart, DeFactoEnd)
                    VALUES (?, ?, ?, ?, ?, ?)
                    ON CONFLICT(CardID, TimeScope) DO UPDATE SET
                        GoalHrs = excluded.GoalHrs,
                        Enabled = excluded.Enabled,
                        DeFactoStart = excluded.DeFactoStart,
                        DeFactoEnd = excluded.DeFactoEnd;
                ";

                foreach (var m in normalized)
                {
                    conn.Execute(
                        upsertSql,
                        m.CardId,
                        m.TimeScope.ToString(),
                        m.GoalHrs,
                        m.Enabled ? 1 : 0,
                        ToDbTimeOnly(m.DeFactoStart),
                        ToDbTimeOnly(m.DeFactoEnd)
                    );
                }
            });
        }


        #endregion

        #region Settings

        private async Task SaveBuiltInSettingDefinitionsAsync()
        {
            var settingDefinitions = SettingKeys.GetBuiltInSettingDefinitions();
            await SaveSettingDefinitionsAsync(settingDefinitions);
        }

        public sealed class SettingRow
        {
            public string SettingKey { get; set; } = string.Empty;
            public string SettingValue { get; set; } = string.Empty;
            public string ValueType { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int IsUserEditable { get; set; }
            public int SortOrder { get; set; }
        }

        private void ValidateSettingDefinitions(List<SettingDefinition> settingDefinitions)
        {
            var duplicateKeys = settingDefinitions
                .GroupBy(x => x.SettingKey)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateKeys.Any())
            {
                throw new InvalidOperationException(
                    $"Duplicate setting keys found: {string.Join(", ", duplicateKeys)}");
            }
        }

        private async Task SaveSettingDefinitionsAsync(List<SettingDefinition> settingDefinitions)
        {
            ValidateSettingDefinitions(settingDefinitions);

            // Get all current rows from the DB
            var existingSettingsInDb = await Db.QueryAsync<SettingRow>("SELECT * FROM Setting");

            foreach (var definition in settingDefinitions)
            {
                var existingSetting = existingSettingsInDb.FirstOrDefault(x => x.SettingKey == definition.SettingKey);

                if (existingSetting == null)
                {
                    // Insert brand new setting row using the code-owned default value + metadata
                    await Db.ExecuteAsync(
                        @"INSERT INTO Setting
                            (SettingKey, SettingValue, ValueType, Category, DisplayName, Description, IsUserEditable, SortOrder)
                          VALUES (?, ?, ?, ?, ?, ?, ?, ?);",
                        definition.SettingKey,
                        definition.DefaultValue,
                        definition.ValueType,
                        definition.Category,
                        definition.DisplayName,
                        definition.Description,
                        definition.IsUserEditable ? 1 : 0,
                        definition.SortOrder);
                }
                else
                {
                    // Update code-owned metadata, but preserve the existing SettingValue
                    await Db.ExecuteAsync(
                        @"UPDATE Setting
                          SET ValueType = ?,
                              Category = ?,
                              DisplayName = ?,
                              Description = ?,
                              IsUserEditable = ?,
                              SortOrder = ?
                          WHERE SettingKey = ?;",
                        definition.ValueType,
                        definition.Category,
                        definition.DisplayName,
                        definition.Description,
                        definition.IsUserEditable ? 1 : 0,
                        definition.SortOrder,
                        definition.SettingKey);

                    // Remove from the "to delete" list
                    existingSettingsInDb.Remove(existingSetting);
                }
            }

            // Any remaining rows in existingSettingsInDb no longer exist in code, so delete them
            foreach (var settingToDelete in existingSettingsInDb)
            {
                await Db.ExecuteAsync(
                    "DELETE FROM Setting WHERE SettingKey = ?;",
                    settingToDelete.SettingKey);
            }
        }

        public async Task<List<AcquiredSetting>> GetSettingsAsync()
        {
            var settingRows = await Db.QueryAsync<SettingRow>(
                "SELECT * FROM Setting ORDER BY Category, SortOrder, SettingKey;");

            var acquiredSettings = new List<AcquiredSetting>();

            foreach (var row in settingRows)
            {
                var acquiredSetting = GetAcquiredSettingFromRow(row);
                acquiredSettings.Add(acquiredSetting);
            }

            return acquiredSettings;
        }

        private AcquiredSetting GetAcquiredSettingFromRow(SettingRow row)
        {
            return row.ValueType switch
            {
                SettingValueTypes.String => GetStringSettingFromRow(row),
                SettingValueTypes.Bool => GetBoolSettingFromRow(row),
                SettingValueTypes.Int => GetIntSettingFromRow(row),
                SettingValueTypes.NullableInt => GetNullableIntSettingFromRow(row),
                SettingValueTypes.Double => GetDoubleSettingFromRow(row),
                _ => throw new InvalidOperationException(
                    $"Unsupported setting value type '{row.ValueType}' for setting '{row.SettingKey}'.")
            };
        }

        private AcquiredSetting GetStringSettingFromRow(SettingRow row)
        {
            return CreateBaseAcquiredSetting(row, row.SettingValue, stringValue: row.SettingValue);
        }

        private AcquiredSetting GetBoolSettingFromRow(SettingRow row)
        {
            if (!TryParseBoolSettingValue(row.SettingValue, out var parsedValue))
            {
                throw new InvalidOperationException(
                    $"Setting '{row.SettingKey}' has ValueType '{SettingValueTypes.Bool}' but value '{row.SettingValue}' could not be parsed.");
            }

            return CreateBaseAcquiredSetting(row, row.SettingValue, boolValue: parsedValue);
        }

        private AcquiredSetting GetIntSettingFromRow(SettingRow row)
        {
            if (!TryParseIntSettingValue(row.SettingValue, out var parsedValue))
            {
                throw new InvalidOperationException(
                    $"Setting '{row.SettingKey}' has ValueType '{SettingValueTypes.Int}' but value '{row.SettingValue}' could not be parsed.");
            }

            return CreateBaseAcquiredSetting(row, row.SettingValue, intValue: parsedValue);
        }

        private AcquiredSetting GetNullableIntSettingFromRow(SettingRow row)
        {
            if (!TryParseNullableIntSettingValue(row.SettingValue, out var parsedValue))
            {
                throw new InvalidOperationException(
                    $"Setting '{row.SettingKey}' has ValueType '{SettingValueTypes.NullableInt}' but value '{row.SettingValue}' could not be parsed.");
            }

            return CreateBaseAcquiredSetting(row, row.SettingValue, intValue: parsedValue);
        }

        private AcquiredSetting GetDoubleSettingFromRow(SettingRow row)
        {
            if (!TryParseDoubleSettingValue(row.SettingValue, out var parsedValue))
            {
                throw new InvalidOperationException(
                    $"Setting '{row.SettingKey}' has ValueType '{SettingValueTypes.Double}' but value '{row.SettingValue}' could not be parsed.");
            }

            return CreateBaseAcquiredSetting(row, row.SettingValue, doubleValue: parsedValue);
        }

        private AcquiredSetting CreateBaseAcquiredSetting(
            SettingRow row,
            string rawValue,
            string? stringValue = null,
            bool? boolValue = null,
            int? intValue = null,
            double? doubleValue = null)
        {
            return new AcquiredSetting
            {
                SettingKey = row.SettingKey,
                ValueType = row.ValueType,
                RawValue = rawValue,

                StringValue = stringValue,
                BoolValue = boolValue,
                IntValue = intValue,
                DoubleValue = doubleValue,

                Category = row.Category,
                DisplayName = row.DisplayName,
                Description = row.Description,
                IsUserEditable = row.IsUserEditable == 1,
                SortOrder = row.SortOrder
            };
        }

        private bool TryParseBoolSettingValue(string value, out bool parsedValue)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                parsedValue = true;
                return true;
            }

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            {
                parsedValue = false;
                return true;
            }

            parsedValue = false;
            return false;
        }

        private bool TryParseIntSettingValue(string value, out int parsedValue)
        {
            return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsedValue);
        }

        private bool TryParseNullableIntSettingValue(string value, out int? parsedValue)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                parsedValue = null;
                return true;
            }

            if (int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsedInt))
            {
                parsedValue = parsedInt;
                return true;
            }

            parsedValue = null;
            return false;
        }

        private bool TryParseDoubleSettingValue(string value, out double parsedValue)
        {
            return double.TryParse(
                value,
                NumberStyles.Float | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out parsedValue);
        }

        public async Task<Dictionary<string, AcquiredSetting>> GetSettingsByKeyAsync()
        {
            var settings = await GetSettingsAsync();
            return settings.ToDictionary(x => x.SettingKey, x => x);
        }

        public async Task SetStringSettingAsync(string settingKey, string value)
        {
            await SetSettingValueAsync(settingKey, value ?? string.Empty, SettingValueTypes.String);
        }

        public async Task SetBoolSettingAsync(string settingKey, bool value)
        {
            await SetSettingValueAsync(settingKey, FormatBoolSettingValue(value), SettingValueTypes.Bool);
        }

        public async Task SetIntSettingAsync(string settingKey, int value)
        {
            await SetSettingValueAsync(settingKey, FormatIntSettingValue(value), SettingValueTypes.Int);
        }

        public async Task SetNullableIntSettingAsync(string settingKey, int? value)
        {
            await SetSettingValueAsync(settingKey, FormatNullableIntSettingValue(value), SettingValueTypes.NullableInt);
        }

        public async Task SetDoubleSettingAsync(string settingKey, double value)
        {
            await SetSettingValueAsync(settingKey, FormatDoubleSettingValue(value), SettingValueTypes.Double);
        }

        private async Task SetSettingValueAsync(string settingKey, string settingValue, string expectedValueType)
        {
            var existingRows = await Db.QueryAsync<SettingRow>(
                "SELECT * FROM Setting WHERE SettingKey = ?;",
                settingKey);

            var existingRow = existingRows.FirstOrDefault();

            if (existingRow == null)
            {
                throw new InvalidOperationException(
                    $"Cannot set value for setting '{settingKey}' because no Setting row exists for that key.");
            }

            ValidateSettingValueType(existingRow, expectedValueType);

            await Db.ExecuteAsync(
                "UPDATE Setting SET SettingValue = ? WHERE SettingKey = ?;",
                settingValue,
                settingKey);
        }

        private void ValidateSettingValueType(SettingRow row, string expectedValueType)
        {
            if (!string.Equals(row.ValueType, expectedValueType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Setting '{row.SettingKey}' has ValueType '{row.ValueType}', but code attempted to write it as '{expectedValueType}'.");
            }
        }

        private string FormatBoolSettingValue(bool value)
        {
            return value ? "true" : "false";
        }

        private string FormatIntSettingValue(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private string FormatNullableIntSettingValue(int? value)
        {
            return value.HasValue
                ? value.Value.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private string FormatDoubleSettingValue(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }


    #endregion

    #endregion

    #region Delete

    #region Achievements

    public async Task DeleteAchievementCardModelAsync(AchievementCardModel model)
        {
            await InitializeAsync();

            if (model == null)
                throw new ArgumentNullException(nameof(model));

            // If the model was never persisted, there's nothing to delete.
            if (model.Id == 0)
                return;

            // Resolve the CardID from the AchievementCard row
            var cardIds = await Db.QueryScalarsAsync<long>(
                "SELECT CardID FROM AchievementCard WHERE AchievementCardID = ? LIMIT 1;",
                model.Id);

            var cardId = cardIds.FirstOrDefault();
            if (cardId == 0)
            {
                // No matching AchievementCard found – nothing to delete.
                return;
            }

            // Deleting the Card row will cascade to:
            //  - AchievementCard (via FOREIGN KEY ... ON DELETE CASCADE)
            //  - AchievementTrophy and any other Card-linked tables
            await Db.ExecuteAsync("DELETE FROM Card WHERE CardID = ?;", cardId);
        }

        public async Task DeleteAchievementTrophyAsync(int trophyId)
        {
            await InitializeAsync();

            await Db.ExecuteAsync(
                @"DELETE FROM AchievementTrophy
                  WHERE TrophyID = ?;",
                trophyId
            );
        }

        #endregion

        #region Common Methods

        private static void DeleteByIds(SQLite.SQLiteConnection conn, string table, string idColumn, List<long> ids)
        {
            // Chunk to avoid SQLite parameter limits if it ever grows.
            const int chunkSize = 500;

            for (int i = 0; i < ids.Count; i += chunkSize)
            {
                var chunk = ids.Skip(i).Take(chunkSize).ToArray();
                var placeholders = string.Join(",", Enumerable.Repeat("?", chunk.Length));
                var sql = $"DELETE FROM {table} WHERE {idColumn} IN ({placeholders});";
                conn.Execute(sql, chunk.Cast<object>().ToArray());
            }
        }

        #endregion

        #endregion

        #region Locks

        #region Row Models

        public sealed class LockRow
        {
            public long LockId { get; set; }
            public int LockNumber { get; set; }
            public long CardId { get; set; }

            public string TimeWindowStart { get; set; } = "";
            public string TimeWindowEnd { get; set; } = "";
        }

        public sealed class LockScheduleRow
        {
            public long ScheduleId { get; set; }
            public long LockId { get; set; }

            public string FrequencyType { get; set; } = "";
            public int FrequencyValue { get; set; }

            public string FromDateTime { get; set; } = "";
            public string? ToDateTime { get; set; }
        }

        public sealed class LockTaskDependencyRow
        {
            public long LockTaskDependencyId { get; set; }
            public long LockId { get; set; }

            public long TaskDependencyCardId { get; set; }
            public int MetricType { get; set; }  // stored as int
            public int TimeScope { get; set; }   // stored as int
            public double TargetValue { get; set; } // stored as REAL
            public int TargetValence { get; set; }
        }

        #endregion

        #region Mappers

        public static class LockMapper
        {
            public static LockModel ToDomain(LockRow row, IEnumerable<LockScheduleRow> scheduleRows, IEnumerable<LockTaskDependencyRow> dependencyRows)
            {
                return new LockModel
                {
                    LockId = row.LockId,
                    LockNumber = row.LockNumber,
                    CardId = row.CardId,
                    TimeWindowStart = TimeOnly.ParseExact(row.TimeWindowStart, "HH:mm:ss", CultureInfo.InvariantCulture),
                    TimeWindowEnd = TimeOnly.ParseExact(row.TimeWindowEnd, "HH:mm:ss", CultureInfo.InvariantCulture),
                    Schedules = scheduleRows.Select(LockScheduleMapper.ToDomain).ToList(),
                    Dependencies = dependencyRows.Select(LockTaskDependencyMapper.ToDomain).ToList(),
                };
            }
        }

        public static class LockScheduleMapper
        {
            public static LockScheduleModel ToDomain(LockScheduleRow row)
            {
                return new LockScheduleModel
                {
                    ScheduleId = row.ScheduleId,
                    LockId = row.LockId,
                    FrequencyType = (FrequencyType)Enum.Parse(typeof(FrequencyType), row.FrequencyType),
                    FrequencyValue = row.FrequencyValue,
                    FromDateTime = DateTime.Parse(row.FromDateTime, null, DateTimeStyles.RoundtripKind),
                    ToDateTime = string.IsNullOrWhiteSpace(row.ToDateTime)
                        ? null
                        : DateTime.Parse(row.ToDateTime!, null, DateTimeStyles.RoundtripKind),
                };
            }
        }

        public static class LockTaskDependencyMapper
        {
            public static LockTaskDependencyModel ToDomain(LockTaskDependencyRow row)
            {
                return new LockTaskDependencyModel
                {
                    LockTaskDependencyId = row.LockTaskDependencyId,
                    LockId = row.LockId,
                    TaskDependencyCardId = row.TaskDependencyCardId,
                    MetricType = (LockDependencyMetricType)row.MetricType,
                    TimeScope = (TimeScope)row.TimeScope,
                    TargetValue = row.TargetValue,
                    TargetValence = (TargetValence)row.TargetValence
                };
            }
        }

        #endregion

        public async Task<List<LockModel>> GetLocksForCardAsync(long cardId)
        {
            await InitializeAsync();

            // 1) Load all locks for this card
            var lockRows = await Db.QueryAsync<LockRow>(
                @"SELECT LockId, LockNumber, CardId, TimeWindowStart, TimeWindowEnd
                  FROM Lock
                  WHERE CardId = ?
                  ORDER BY LockNumber ASC;",
                cardId);

            if (lockRows.Count == 0)
                return new List<LockModel>();

            var lockIds = lockRows.Select(x => x.LockId).ToArray();

            // 2) Load all schedules for these locks (single query)
            var scheduleRows = await QueryByIdsAsync<LockScheduleRow>(
                tableName: "LockSchedule",
                idColumn: "LockId",
                ids: lockIds,
                selectColumns: "ScheduleId, LockId, FrequencyType, FrequencyValue, FromDateTime, ToDateTime",
                orderBy: "LockId ASC, ScheduleId ASC");

            // 3) Load all dependencies for these locks (single query)
            var dependencyRows = await QueryByIdsAsync<LockTaskDependencyRow>(
                tableName: "LockTaskDependency",
                idColumn: "LockId",
                ids: lockIds,
                selectColumns: "LockTaskDependencyId, LockId, TaskDependencyCardId, MetricType, TimeScope, TargetValue, TargetValence",
                orderBy: "LockId ASC, LockTaskDependencyId ASC");

            // 4) Group them for fast assembly
            var schedulesByLock = scheduleRows
                .GroupBy(x => x.LockId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var depsByLock = dependencyRows
                .GroupBy(x => x.LockId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 5) Map to domain
            var result = new List<LockModel>(lockRows.Count);

            foreach (var lr in lockRows)
            {
                schedulesByLock.TryGetValue(lr.LockId, out var s);
                depsByLock.TryGetValue(lr.LockId, out var d);

                result.Add(LockMapper.ToDomain(
                    lr,
                    s ?? Enumerable.Empty<LockScheduleRow>(),
                    d ?? Enumerable.Empty<LockTaskDependencyRow>()));
            }

            return result;
        }

        public async Task SaveLocksForCardAsync(long cardId, List<LockModel> locksToSave)
        {
            await InitializeAsync();

            locksToSave ??= new List<LockModel>();

            // Ensure FK consistency
            foreach (var l in locksToSave)
                l.CardId = cardId;

            await Db.RunInTransactionAsync(conn =>
            {
                // 1) Find existing locks for this card
                var existingLockIds = conn.Query<LockRow>(
                    @"SELECT LockId, LockNumber, CardId, TimeWindowStart, TimeWindowEnd
                      FROM Lock
                      WHERE CardId = ?;",
                    cardId).Select(x => x.LockId).ToList();

                if (existingLockIds.Count > 0)
                {
                    // 2) Delete children first
                    DeleteByIds(conn, "LockSchedule", "LockId", existingLockIds);
                    DeleteByIds(conn, "LockTaskDependency", "LockId", existingLockIds);

                    // 3) Delete locks
                    conn.Execute(@"DELETE FROM Lock WHERE CardId = ?;", cardId);
                }

                // 4) Insert new locks + children
                foreach (var model in locksToSave.OrderBy(x => x.LockNumber))
                {
                    // --- insert Lock ---
                    conn.Execute(
                        @"INSERT INTO Lock (LockNumber, CardId, TimeWindowStart, TimeWindowEnd)
                          VALUES (?, ?, ?, ?);",
                        model.LockNumber,
                        cardId,
                        model.TimeWindowStart.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                        model.TimeWindowEnd.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                    );

                    var newLockId = conn.ExecuteScalar<long>("SELECT last_insert_rowid();");
                    model.LockId = newLockId; // optional, but usually useful

                    // --- insert schedules ---
                    if (model.Schedules != null)
                    {
                        foreach (var s in model.Schedules)
                        {
                            conn.Execute(
                                @"INSERT INTO LockSchedule (LockId, FrequencyType, FrequencyValue, FromDateTime, ToDateTime)
                                  VALUES (?, ?, ?, ?, ?);",
                                newLockId,
                                s.FrequencyType.ToString(),              // TEXT enum name
                                s.FrequencyValue,
                                s.FromDateTime.ToString("o", CultureInfo.InvariantCulture),
                                s.ToDateTime?.ToString("o", CultureInfo.InvariantCulture)
                            );

                            s.LockId = newLockId; // optional
                        }
                    }

                    // --- insert dependencies ---
                    if (model.Dependencies != null)
                    {
                        foreach (var d in model.Dependencies)
                        {
                            conn.Execute(
                                @"INSERT INTO LockTaskDependency
                                    (LockId, TaskDependencyCardId, MetricType, TimeScope, TargetValue, TargetValence)
                                  VALUES (?, ?, ?, ?, ?, ?);",
                                newLockId,
                                d.TaskDependencyCardId,
                                (int)d.MetricType,
                                (int)d.TimeScope,
                                d.TargetValue,
                                (int)d.TargetValence
                            );

                            d.LockId = newLockId; // optional
                        }
                    }
                }
            });
        }

        public async Task DeleteLockModelAsync(LockModel lockModel)
        {
            await InitializeAsync();

            if (lockModel == null)
                throw new ArgumentNullException(nameof(lockModel));

            // We strongly prefer deleting by LockId (stable PK).
            // If LockId isn't present, fall back to (CardId, LockNumber) if available.
            var lockId = lockModel.LockId;
            var hasPk = lockId > 0;

            if (!hasPk)
            {
                if (lockModel.CardId <= 0)
                    throw new InvalidOperationException("Cannot delete Lock without LockId or a valid CardId.");
                // If LockNumber is meaningful/unique per card in your domain, we can resolve LockId.
                // Otherwise, you should require LockId.
                if (lockModel.LockNumber <= 0)
                    throw new InvalidOperationException("Cannot delete Lock without LockId or a valid LockNumber.");

                lockId = await Db.ExecuteScalarAsync<long>(
                    @"SELECT LockId
                      FROM Lock
                      WHERE CardId = ? AND LockNumber = ?
                      LIMIT 1;",
                    lockModel.CardId,
                    lockModel.LockNumber);

                if (lockId <= 0)
                    return; // Already gone / nothing to delete.
            }

            await Db.RunInTransactionAsync(conn =>
            {
                // 1) Delete children first
                conn.Execute(@"DELETE FROM LockSchedule WHERE LockId = ?;", lockId);
                conn.Execute(@"DELETE FROM LockTaskDependency WHERE LockId = ?;", lockId);

                // 2) Delete the lock
                conn.Execute(@"DELETE FROM Lock WHERE LockId = ?;", lockId);
            });
        }

        private async Task PopulateLocks(List<IActiveCardModel> mainQuest, List<MissionCardModel> mission)
        {
            // Gather all cards that can have locks
            var activeCards = mainQuest
                .Cast<IActiveCardModel>()
                .Concat(mission)
                .ToList();

            // Collect distinct card IDs
            var cardIds = activeCards
                .Select(c => c.CardID)
                .Distinct()
                .ToList();

            // Fetch locks and build lookup
            var locksByCardId = new Dictionary<long, List<LockModel>>();

            foreach (var id in cardIds)
            {
                var locks = await GetLocksForCardAsync(id); // your existing method
                locksByCardId[id] = locks;
            }

            // Assign onto each card
            foreach (var card in activeCards)
            {
                card.Locks = locksByCardId.TryGetValue(card.CardID, out var locks)
                    ? locks
                    : new List<LockModel>();
            }
        }

        #endregion

        #region Dashboard Shortcuts

        #region Row Models

        public sealed class ShortcutGroupRow
        {
            public long ShortcutGroupId { get; set; }
            public string Name { get; set; } = "";
            public string Color { get; set; } = "#FF000000"; // #AARRGGBB
            public int ShortcutGroupOrder { get; set; }
        }

        public sealed class ShortcutRow
        {
            public long ShortcutId { get; set; }
            public string IconChar { get; set; } = "";
            public long TargetCardId { get; set; }
            public long ShortcutGroupId { get; set; }
            public int ShortcutOrder { get; set; }
        }

        /// <summary>
        /// Join-row for Dashboard retrieval.
        /// </summary>
        public sealed class DashboardShortcutJoinRow
        {
            public long ShortcutId { get; set; }
            public string IconChar { get; set; } = "";
            public long TargetCardId { get; set; }
            public int ShortcutOrder { get; set; }

            public long ShortcutGroupId { get; set; }
            public string GroupName { get; set; } = "";
            public string GroupColor { get; set; } = "#FF000000";
            public int ShortcutGroupOrder { get; set; }
        }

        #endregion

        #region Mappers

        public static class ShortcutGroupMapper
        {
            public static ShortcutGroupModel ToDomain(ShortcutGroupRow row)
            {
                return new ShortcutGroupModel
                {
                    ShortcutGroupId = row.ShortcutGroupId,
                    Name = row.Name,
                    Color = ParseColor(row.Color),
                    ShortcutGroupOrder = row.ShortcutGroupOrder
                };
            }

            public static ShortcutGroupRow ToRow(ShortcutGroupModel model)
            {
                return new ShortcutGroupRow
                {
                    ShortcutGroupId = model.ShortcutGroupId,
                    Name = model.Name ?? "",
                    Color = NormalizeArgbHex(ToHexArgb(model.Color)),
                    ShortcutGroupOrder = model.ShortcutGroupOrder
                };
            }
        }

        public static class ShortcutMapper
        {
            public static ShortcutModel ToDomain(ShortcutRow row)
            {
                return new ShortcutModel
                {
                    ShortcutId = row.ShortcutId,
                    IconChar = row.IconChar,
                    TargetCardId = row.TargetCardId,
                    ShortcutGroupId = row.ShortcutGroupId,
                    ShortcutOrder = row.ShortcutOrder,
                    Group = null
                };
            }

            public static ShortcutRow ToRow(ShortcutModel model)
            {
                return new ShortcutRow
                {
                    ShortcutId = model.ShortcutId,
                    IconChar = model.IconChar ?? "",
                    TargetCardId = model.TargetCardId,
                    ShortcutGroupId = model.ShortcutGroupId,
                    ShortcutOrder = model.ShortcutOrder
                };
            }

            public static ShortcutModel ToDomain(DashboardShortcutJoinRow row)
            {
                return new ShortcutModel
                {
                    ShortcutId = row.ShortcutId,
                    IconChar = row.IconChar,
                    TargetCardId = row.TargetCardId,
                    ShortcutGroupId = row.ShortcutGroupId,
                    ShortcutOrder = row.ShortcutOrder,
                    Group = new ShortcutGroupModel
                    {
                        ShortcutGroupId = row.ShortcutGroupId,
                        Name = row.GroupName,
                        Color = ParseColor(row.GroupColor),
                        ShortcutGroupOrder = row.ShortcutGroupOrder
                    }
                };
            }
        }

        #endregion

        #region Public API (domain-returning)

        public async Task<List<ShortcutGroupModel>> GetShortcutGroupsAsync()
        {
            await InitializeAsync();

            var rows = await Db.QueryAsync<ShortcutGroupRow>(
                @"SELECT ShortcutGroupId, Name, Color, ShortcutGroupOrder
                  FROM ShortcutGroup
                  ORDER BY ShortcutGroupOrder ASC, ShortcutGroupId ASC;");

            return rows.Select(ShortcutGroupMapper.ToDomain).ToList();
        }

        /// <summary>
        /// Returns shortcuts ordered by (GroupOrder, ShortcutOrder).
        /// Each ShortcutModel includes its Group populated (JOIN).
        /// </summary>
        public async Task<List<ShortcutModel>> GetDashboardShortcutsAsync()
        {
            await InitializeAsync();

            var joinRows = await Db.QueryAsync<DashboardShortcutJoinRow>(
                @"SELECT
                      s.ShortcutId        AS ShortcutId,
                      s.IconChar          AS IconChar,
                      s.TargetCardId      AS TargetCardId,
                      s.ShortcutOrder     AS ShortcutOrder,
                      g.ShortcutGroupId   AS ShortcutGroupId,
                      g.Name              AS GroupName,
                      g.Color             AS GroupColor,
                      g.ShortcutGroupOrder AS ShortcutGroupOrder
                  FROM Shortcut s
                  JOIN ShortcutGroup g ON g.ShortcutGroupId = s.ShortcutGroupId
                  ORDER BY g.ShortcutGroupOrder ASC, s.ShortcutOrder ASC, s.ShortcutId ASC;");

            return joinRows.Select(ShortcutMapper.ToDomain).ToList();
        }

        /// <summary>
        /// Upsert group by name (case-insensitive). Returns the persisted group (including ID).
        /// This supports your "user typed a new group name" scenario.
        /// </summary>
        public async Task<ShortcutGroupModel> UpsertShortcutGroupAsync(ShortcutGroupModel group)
        {
            await InitializeAsync();

            if (group == null) throw new ArgumentNullException(nameof(group));
            if (string.IsNullOrWhiteSpace(group.Name))
                throw new ArgumentException("Group.Name is required.", nameof(group));

            var name = group.Name.Trim();
            var colorHex = NormalizeArgbHex(ToHexArgb(group.Color));
            var order = group.ShortcutGroupOrder;

            ShortcutGroupModel? result = null;

            await Db.RunInTransactionAsync(conn =>
            {
                var existing = conn.Query<ShortcutGroupRow>(
                    @"SELECT ShortcutGroupId, Name, Color, ShortcutGroupOrder
                      FROM ShortcutGroup
                      WHERE Name = ? COLLATE NOCASE
                      LIMIT 1;",
                    name).FirstOrDefault();

                if (existing != null)
                {
                    conn.Execute(
                        @"UPDATE ShortcutGroup
                          SET Color = ?, ShortcutGroupOrder = ?
                          WHERE ShortcutGroupId = ?;",
                        colorHex, order, existing.ShortcutGroupId);

                    // Return updated domain model
                    result = new ShortcutGroupModel
                    {
                        ShortcutGroupId = existing.ShortcutGroupId,
                        Name = existing.Name,
                        Color = ParseColor(colorHex),
                        ShortcutGroupOrder = order
                    };
                    return;
                }

                conn.Execute(
                    @"INSERT INTO ShortcutGroup (Name, Color, ShortcutGroupOrder)
                      VALUES (?, ?, ?);",
                    name, colorHex, order);

                var newId = conn.ExecuteScalar<long>("SELECT last_insert_rowid();");

                result = new ShortcutGroupModel
                {
                    ShortcutGroupId = newId,
                    Name = name,
                    Color = ParseColor(colorHex),
                    ShortcutGroupOrder = order
                };
            });

            return result ?? throw new InvalidOperationException("UpsertShortcutGroupAsync failed unexpectedly.");
        }

        public async Task<ShortcutModel> SaveShortcutAsync(ShortcutModel shortcut)
        {
            await InitializeAsync();

            if (shortcut == null) throw new ArgumentNullException(nameof(shortcut));
            if (shortcut.TargetCardId <= 0) throw new ArgumentException("TargetCardId must be set.", nameof(shortcut));
            if (shortcut.ShortcutGroupId <= 0) throw new ArgumentException("ShortcutGroupId must be set.", nameof(shortcut));

            shortcut.IconChar = (shortcut.IconChar ?? "").Trim();

            var row = ShortcutMapper.ToRow(shortcut);
            long savedId = row.ShortcutId;

            await Db.RunInTransactionAsync(conn =>
            {
                if (savedId <= 0)
                {
                    conn.Execute(
                        @"INSERT INTO Shortcut (IconChar, TargetCardId, ShortcutGroupId, ShortcutOrder)
                          VALUES (?, ?, ?, ?);",
                        row.IconChar, row.TargetCardId, row.ShortcutGroupId, row.ShortcutOrder);

                    savedId = conn.ExecuteScalar<long>("SELECT last_insert_rowid();");
                }
                else
                {
                    conn.Execute(
                        @"UPDATE Shortcut
                          SET IconChar = ?, TargetCardId = ?, ShortcutGroupId = ?, ShortcutOrder = ?
                          WHERE ShortcutId = ?;",
                        row.IconChar, row.TargetCardId, row.ShortcutGroupId, row.ShortcutOrder, savedId);
                }
            });

            shortcut.ShortcutId = savedId;
            return shortcut;
        }

        public async Task DeleteShortcutAsync(long shortcutId)
        {
            await InitializeAsync();
            if (shortcutId <= 0) return;

            await Db.ExecuteAsync(@"DELETE FROM Shortcut WHERE ShortcutId = ?;", shortcutId);
        }

        public async Task DeleteShortcutGroupAsync(long shortcutGroupId)
        {
            await InitializeAsync();
            if (shortcutGroupId <= 0) return;

            // FK ON DELETE CASCADE will remove associated shortcuts
            await Db.ExecuteAsync(@"DELETE FROM ShortcutGroup WHERE ShortcutGroupId = ?;", shortcutGroupId);
        }

        #endregion

        #region Color helpers (keep local + deterministic)

        private static Color ParseColor(string? hex)
        {
            // Expect #AARRGGBB or #RRGGBB; normalize and parse.
            var norm = NormalizeArgbHex(hex);
            return Color.FromArgb(norm);
        }

        private static string ToHexArgb(Color c)
        {
            // MAUI Color gives 0..1 floats
            byte a = (byte)Math.Round(c.Alpha * 255);
            byte r = (byte)Math.Round(c.Red * 255);
            byte g = (byte)Math.Round(c.Green * 255);
            byte b = (byte)Math.Round(c.Blue * 255);
            return $"#{a:X2}{r:X2}{g:X2}{b:X2}";
        }

        private static string NormalizeArgbHex(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return "#FF000000";

            hex = hex.Trim();
            if (!hex.StartsWith("#"))
                hex = "#" + hex;

            // #RRGGBB -> #FFRRGGBB
            if (hex.Length == 7)
                return "#FF" + hex.Substring(1);

            // #AARRGGBB
            if (hex.Length == 9)
                return hex;

            // Fallback
            return "#FF000000";
        }

        #endregion

        #endregion

        #region Reports Implementation

        private static string? ToDbDateTime(DateTime? dt)  => dt?.ToString("o"); // ISO 8601 round-trip

        private static DateTime? FromDbDateTime(string? s) => string.IsNullOrWhiteSpace(s) ? null : DateTime.Parse(s, null, System.Globalization.DateTimeStyles.RoundtripKind);


        public async Task UpsertReportAsync(ReportModel report)
        {
            await InitializeAsync();

            if (report == null) throw new ArgumentNullException(nameof(report));
            if (string.IsNullOrWhiteSpace(report.Title))
                throw new ArgumentException("Report.Title is required.", nameof(report));

            await Db.RunInTransactionAsync(conn =>
            {
                var lastRunOn = ToDbDateTime(report.LastRunOn);
                var eligible = report.EligibleForAchievment ? 1 : 0;
                var sql = report.SQLQuery ?? string.Empty;

                if (report.Id > 0)
                {
                    const string updateSql = @"
                    UPDATE Report
                    SET Title = ?,
                        SQLQuery = ?,
                        LastRunOn = ?,
                        EligibleForAchievment = ?
                    WHERE Id = ?;";

                    conn.Execute(updateSql, report.Title, sql, lastRunOn, eligible, report.Id);
                    return;
                }

                // Insert or update-by-title (requires UX_Report_Title)
                const string upsertByTitleSql = @"
                    INSERT INTO Report (Title, SQLQuery, LastRunOn, EligibleForAchievment)
                    VALUES (?, ?, ?, ?)
                    ON CONFLICT(Title) DO UPDATE SET
                        SQLQuery = excluded.SQLQuery,
                        LastRunOn = excluded.LastRunOn,
                        EligibleForAchievment = excluded.EligibleForAchievment;";

                conn.Execute(upsertByTitleSql, report.Title, sql, lastRunOn, eligible);

                // Ensure report.Id is set:
                // If it was an insert, last_insert_rowid() works.
                // If it was an update (conflict), last_insert_rowid() may not change, so fetch by Title.
                var idRow = conn.Query<IdRow>(
                    "SELECT Id FROM Report WHERE Title = ? LIMIT 1;",
                    report.Title).FirstOrDefault();

                if (idRow != null)
                    report.Id = idRow.Id;
            });
        }

        private sealed class IdRow
        {
            public int Id { get; set; }
        }

        public async Task DeleteReportAsync(int reportId)
        {
            await InitializeAsync();

            await Db.RunInTransactionAsync(conn =>
            {
                conn.Execute("DELETE FROM Report WHERE Id = ?;", reportId);
            });
        }


        private sealed class ReportRow
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public string SQLQuery { get; set; } = "";
            public string? LastRunOn { get; set; }
            public int EligibleForAchievment { get; set; }
        }

        private static ReportModel MapReportRow(ReportRow row)
        {
            return new ReportModel
            {
                Id = row.Id,
                Title = row.Title,
                SQLQuery = row.SQLQuery,
                LastRunOn = string.IsNullOrWhiteSpace(row.LastRunOn)
                    ? null
                    : DateTime.Parse(row.LastRunOn, null, System.Globalization.DateTimeStyles.RoundtripKind),
                EligibleForAchievment = row.EligibleForAchievment == 1
            };
        }

        public async Task<IReadOnlyList<ReportModel>> GetReportsAsync()
        {
            await InitializeAsync();

            const string sql = @"
                SELECT
                    r.Id                    AS Id,
                    r.Title                 AS Title,
                    r.SQLQuery              AS SQLQuery,
                    r.LastRunOn             AS LastRunOn,
                    r.EligibleForAchievment AS EligibleForAchievment
                FROM Report r
                ORDER BY r.Title;";

            var rows = await Db.QueryAsync<ReportRow>(sql);

            return rows.Select(r => new ReportModel
            {
                Id = r.Id,
                Title = r.Title,
                SQLQuery = r.SQLQuery,
                LastRunOn = string.IsNullOrWhiteSpace(r.LastRunOn)
                    ? null
                    : DateTime.Parse(r.LastRunOn, null, System.Globalization.DateTimeStyles.RoundtripKind),
                EligibleForAchievment = r.EligibleForAchievment == 1
            }).ToList();
        }

        #endregion
    }
}
