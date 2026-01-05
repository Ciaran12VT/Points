using CommunityToolkit.Maui.Core.Extensions;
using Points.Global;
using Points.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public async Task InitializeAsync()
        {
            if (_db != null) return;

            // Ensures native SQLite is loaded correctly on mobile platforms.
            SQLitePCL.Batteries_V2.Init();

            _db = new SQLiteAsyncConnection(_dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

            await _db.ExecuteAsync("PRAGMA foreign_keys = ON;");

            var script = SqlQueryService.GenerateDbCreationScript();
            var statements = script.Split(';').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            await _db.RunInTransactionAsync(conn =>
            {
                conn.Execute("PRAGMA foreign_keys = ON;"); 
                foreach (var stmt in statements)
                    conn.Execute(stmt);
            });
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


        public string BackupsFolderPath => AppPaths.DbBackupsFolder;

        /// <summary>
        /// Creates a consistent snapshot of the current database into AppPaths.DbBackupsFolder.
        /// Uses SQLite "VACUUM INTO" to avoid WAL/shm copy issues.
        /// </summary>
        public async Task BackupAsync()
        {
            await InitializeAsync();

            // Ensure backup folder exists
            Directory.CreateDirectory(BackupsFolderPath);

            // Use UTC in filenames so sorting is stable regardless of device timezone
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"points_{stamp}_utc.db3";
            var backupPath = Path.Combine(BackupsFolderPath, backupFileName);

            // Defensive: don't overwrite (in the extremely unlikely case of same-second calls)
            if (File.Exists(backupPath))
            {
                stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                backupFileName = $"points_{stamp}_utc.db3";
                backupPath = Path.Combine(BackupsFolderPath, backupFileName);
            }

            // Create the snapshot
            // NOTE: must quote the path because it can contain '-' etc.
            var escaped = backupPath.Replace("'", "''");
            await Db.ExecuteAsync($"VACUUM INTO '{escaped}';");
        }

        /// <summary>
        /// Restores the DB from a backup file. This will close the active SQLite connection and replace the DB file.
        /// </summary>
        public async Task RestoreAsync(string backupFilePath)
        {
            if (string.IsNullOrWhiteSpace(backupFilePath))
                throw new ArgumentException("Backup path is required.", nameof(backupFilePath));

            if (!File.Exists(backupFilePath))
                throw new FileNotFoundException("Backup file not found.", backupFilePath);

            // Ensure folder exists (db folder)
            Directory.CreateDirectory(AppPaths.DbFolder);

            // Close current connection if open
            if (_db != null)
            {
                try
                {
                    await _db.CloseAsync();
                }
                finally
                {
                    _db = null;
                }
            }

            // Replace the DB file
            // Overwrite: true
            File.Copy(backupFilePath, _dbPath, overwrite: true);

            // Re-init connection
            await InitializeAsync();
        }

        public DateTime? GetLastBackupUtc()
        {
            try
            {
                if (!Directory.Exists(BackupsFolderPath))
                    return null;

                // Expecting files like: points_20260104_061530_utc.db3
                // We’ll just use file timestamps as a fallback, but prefer parsing from filename if present.
                var dir = new DirectoryInfo(BackupsFolderPath);
                var latest = dir.GetFiles("points_*_utc.db3")
                                .OrderByDescending(f => f.Name) // because yyyyMMdd_HHmmss sorts correctly
                                .FirstOrDefault();

                if (latest == null)
                    return null;

                // Parse from filename if possible
                // points_yyyyMMdd_HHmmss_utc.db3
                var name = Path.GetFileNameWithoutExtension(latest.Name); // points_20260104_061530_utc
                var parts = name.Split('_');
                if (parts.Length >= 4)
                {
                    var datePart = parts[1]; // yyyyMMdd
                    var timePart = parts[2]; // HHmmss
                    var combined = datePart + timePart;

                    if (DateTime.TryParseExact(
                            combined,
                            "yyyyMMddHHmmss",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out var parsedUtc))
                    {
                        return parsedUtc;
                    }
                }

                // Fallback: file system timestamp (already UTC on many systems, but not guaranteed)
                return latest.LastWriteTimeUtc;
            }
            catch
            {
                return null;
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


        #endregion

        #region Read

        //Achievement
        //public async Task<AchievementCardModel> GetAchievementCardModelDataAsync(int id)
        //{
        //    const string sql = @"
        //            SELECT
        //                a.AchievementCardID  AS AchievementCardID,
        //                a.CardID             AS CardID,

        //                c.Title              AS Title,
        //                c.Tags               AS Tags,

        //                a.Status             AS Status,
        //                a.Description        AS Description,

        //                a.SubType            AS SubType,
        //                a.ProgressType       AS ProgressType,
        //                a.RangeAmount        AS RangeAmount,

        //                a.CreatedDate        AS CreatedDate,
        //                a.AvailableFromDate  AS AvailableFromDate,
        //                a.DueDate            AS DueDate,

        //                a.CompletedDate      AS CompletedDate,
        //                a.LastEarnedAt       AS LastEarnedAt,
        //                a.Deadline           AS Deadline,

        //                a.TrophyURLs         AS TrophyURLs
        //            FROM AchievementCard a
        //            JOIN Card c ON c.CardID = a.CardID
        //            WHERE a.AchievementCardID = ?
        //            LIMIT 1;
        //        ";

        //    var row = (await Db.QueryAsync<AchievementCardJoinedRow>(sql, id)).FirstOrDefault();
        //    if (row == null) throw new KeyNotFoundException($"AchievementCard not found. AchievementCardID={id}");

        //    Enum.TryParse(row.SubType, out AchievementDifficultyLevels difficulty);
        //    Enum.TryParse(row.ProgressType, out AchievementGoalType goalType);

        //    var model = new AchievementCardModel
        //    {
        //        Id = row.AchievementCardID,

        //        Title = row.Title ?? "",
        //        Tags = row.Tags ?? "",

        //        Status = row.Status ?? "",
        //        Description = row.Description ?? "",

        //        Difficulty = difficulty,
        //        GoalType = goalType,

        //        RangeAmount = row.RangeAmount ?? 0,

        //        CreatedDate = ParseIsoDateTime(row.CreatedDate),
        //        AvailableFromDate = ParseIsoDateTime(row.AvailableFromDate),
        //        DueDate = ParseIsoDateTime(row.DueDate),

        //        Deadline = string.IsNullOrWhiteSpace(row.Deadline)
        //            ? null
        //            : ParseIsoDateTime(row.Deadline),

        //        LastEarnedAt = string.IsNullOrWhiteSpace(row.LastEarnedAt)
        //            ? null
        //            : ParseIsoDateTime(row.LastEarnedAt),

        //        Trophies = new ObservableCollection<string>()
        //    };

        //    if (!string.IsNullOrWhiteSpace(row.CompletedDate))
        //        model.MarkCompleted(ParseIsoDateTime(row.CompletedDate));

        //    if (!string.IsNullOrWhiteSpace(row.TrophyURLs))
        //    {
        //        foreach (var t in row.TrophyURLs
        //                     .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        //        {
        //            model.Trophies.Add(t);
        //        }
        //    }

        //    return model;
        //}


        //public async Task<List<AchievementCardModel>> GetAchievementCardModelsDataAsync(string whereClause = null)
        //{
        //    var sql = @"
        //        SELECT
        //            a.AchievementCardID  AS AchievementCardID,
        //            a.CardID             AS CardID,

        //            c.Title              AS Title,
        //            c.Tags               AS Tags,

        //            a.Status             AS Status,
        //            a.Description        AS Description,

        //            a.SubType            AS SubType,
        //            a.ProgressType       AS ProgressType,
        //            a.RangeAmount        AS RangeAmount,

        //            a.CreatedDate        AS CreatedDate,
        //            a.AvailableFromDate  AS AvailableFromDate,
        //            a.DueDate            AS DueDate,

        //            a.CompletedDate      AS CompletedDate,
        //            a.LastEarnedAt       AS LastEarnedAt,
        //            a.Deadline           AS Deadline,

        //            a.TrophyURLs         AS TrophyURLs
        //        FROM AchievementCard a
        //        JOIN Card c ON c.CardID = a.CardID
        //    ";

        //    if (!string.IsNullOrWhiteSpace(whereClause))
        //    {
        //        var wc = whereClause.Trim();
        //        sql += wc.StartsWith("WHERE", StringComparison.OrdinalIgnoreCase)
        //            ? " " + wc
        //            : " WHERE " + wc;
        //    }

        //    var rows = await Db.QueryAsync<AchievementCardJoinedRow>(sql);
        //    if (rows.Count == 0)
        //        return new List<AchievementCardModel>();

        //    var result = new List<AchievementCardModel>(rows.Count);

        //    foreach (var row in rows)
        //    {
        //        Enum.TryParse(row.SubType, out AchievementDifficulty difficulty);
        //        Enum.TryParse(row.ProgressType, out AchievementGoalType goalType);

        //        var model = new AchievementCardModel
        //        {
        //            Id = row.AchievementCardID,

        //            Title = row.Title ?? "",
        //            Tags = row.Tags ?? "",

        //            Status = row.Status ?? "",
        //            Description = row.Description ?? "",

        //            Difficulty = difficulty,
        //            GoalType = goalType,

        //            RangeAmount = row.RangeAmount ?? 0,

        //            CreatedDate = ParseIsoDateTime(row.CreatedDate),
        //            AvailableFromDate = ParseIsoDateTime(row.AvailableFromDate),
        //            DueDate = ParseIsoDateTime(row.DueDate),

        //            Deadline = string.IsNullOrWhiteSpace(row.Deadline)
        //                ? null
        //                : ParseIsoDateTime(row.Deadline),

        //            LastEarnedAt = string.IsNullOrWhiteSpace(row.LastEarnedAt)
        //                ? null
        //                : ParseIsoDateTime(row.LastEarnedAt),

        //            Trophies = new ObservableCollection<string>()
        //        };

        //        if (!string.IsNullOrWhiteSpace(row.CompletedDate))
        //            model.MarkCompleted(ParseIsoDateTime(row.CompletedDate));

        //        if (!string.IsNullOrWhiteSpace(row.TrophyURLs))
        //        {
        //            foreach (var t in row.TrophyURLs
        //                         .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        //            {
        //                model.Trophies.Add(t);
        //            }
        //        }

        //        result.Add(model);
        //    }

        //    return result;
        //}


        //private sealed class AchievementCardJoinedRow
        //{
        //    public int AchievementCardID { get; set; }
        //    public long CardID { get; set; }

        //    public string? Title { get; set; }
        //    public string? Tags { get; set; }

        //    public string? Status { get; set; }
        //    public string? Description { get; set; }

        //    public string? SubType { get; set; }
        //    public string? ProgressType { get; set; }

        //    public int? RangeAmount { get; set; }

        //    public string CreatedDate { get; set; } = "";
        //    public string AvailableFromDate { get; set; } = "";
        //    public string DueDate { get; set; } = "";

        //    public string? CompletedDate { get; set; }
        //    public string? LastEarnedAt { get; set; }
        //    public string? Deadline { get; set; }

        //    public string? TrophyURLs { get; set; }
        //}


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
            if(Enum.TryParse<BudgetTransactionType>(type ?? "", true, out BudgetTransactionType btt))
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


        //Home Seed

        public async Task<HomeSeedData> GetHomeSeedDataAsync(DateTime rangeStart, DateTime rangeEnd)
        {
            var mainQuest = await GetMainQuestModelsDataAsync(rangeStart, rangeEnd);

            var mission = await GetMissionCardModelsDataAsync("m.CompletedDate IS NULL OR m.CompletedDate >= datetime('now', 'localtime', 'start of day')");

            var budget = await GetBudgetCardModelsDataAsync();

            //var achievements = await GetAchievementCardModelsDataAsync();

            var valueTrackers = await GetValueTrackerCardModelsDataAsync();

            var eventTrackers = await GetEventTrackerCardModelsDataAsync();

            var seed = new HomeSeedData
            {
                MainQuestCards = mainQuest,
                MissionCards = mission,
                BudgetCards = budget,
                Achievements = new List<ICardModel>(), //achievements
                ValueTrackers = valueTrackers,
                EventTrackers = eventTrackers
            };

            return seed;
        }

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

                EstCompletionTime = StringToTimeSpan(row.EstCompletionTimeText),
                IsFailed = row.IsFailed != 0,
            };

            // 3) Load activity slices by CardID (because that’s how you save them)
            const string actSql = @"
                SELECT
                    ActivityID     AS ActivityID,
                    CardID         AS CardID,
                    Start          AS Start,
                    ""End""        AS End,
                    ValuePerMinute AS ValuePerMinute
                FROM Activity
                WHERE CardID = ?
                ORDER BY Start;
            ";

            var actRows = await Db.QueryAsync<ActivityRow>(actSql, row.CardID);

            // If your MissionCardModel.Activity is a List<ActivityModel> (or similar)
            model.Activity = actRows.Select(a => new ActivityModel
            {
                Id = a.ActivityID,
                StartDate = ParseIsoDateTime(a.Start),
                EndDate = ParseIsoDateTime(a.End),
                ValuePerMinute = a.ValuePerMinute
            }).ToList();

            return model;
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

        private static DateTime ParseIsoDateTime(string value)  => DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);

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

            public string? EstCompletionTimeText { get; set; }

            // Stored as INTEGER (0/1)
            public int IsFailed { get; set; }

            public double ValuePerMinute { get; set; }
        }

        private sealed class ActivityRow
        {
            public int ActivityID { get; set; }
            public long CardID { get; set; }

            // Stored as TEXT (ISO-8601)
            public string Start { get; set; } = "";
            public string End { get; set; } = "";
            public string ValueRateName { get; set; }

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
                    ActivityID     AS ActivityID,
                    CardID         AS CardID,
                    Start          AS Start,
                    ""End""        AS End,
                    ValuePerMinute AS ValuePerMinute
                FROM Activity
                WHERE CardID IN ({placeholders})
                ORDER BY CardID, Start;
            ";

            var actRows = await Db.QueryAsync<ActivityRow>(actSql, cardIds.Cast<object>().ToArray());

            foreach (var a in actRows)
            {
                if (!byCardId.TryGetValue(a.CardID, out var mission))
                    continue;

                mission.Activity.Add(new ActivityModel
                {
                    Id = a.ActivityID,
                    StartDate = ParseIsoDateTime(a.Start),
                    EndDate = ParseIsoDateTime(a.End),
                    ValuePerMinute = a.ValuePerMinute
                });
            }

            // Return in the same order as the base query result set
            // (Dictionary doesn't preserve ordering reliably).
            var result = new List<MissionCardModel>(rows.Count);
            foreach (var row in rows)
                result.Add(byCardId[row.CardID]);

            return result;
        }


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

                Title = row.Title ?? "",
                Tags = row.Tags ?? "",

                Status = row.Status ?? "",
                Description = row.Description ?? "",
            };

            // 2.5) Load activity by CardID (same pattern as TAT)
            const string actSql = @"
                SELECT
                    ActivityID     AS ActivityID,
                    CardID         AS CardID,
                    Start          AS Start,
                    ""End""        AS End,
                    ValuePerMinute AS ValuePerMinute
                FROM Activity
                WHERE CardID = ?
                ORDER BY Start;
            ";

            var actRows = await Db.QueryAsync<ActivityRow>(actSql, row.CardID);

            model.Activity = actRows.Select(a => new ActivityModel
            {
                Id = a.ActivityID,
                StartDate = ParseIsoDateTime(a.Start),
                EndDate = ParseIsoDateTime(a.End),
                ValuePerMinute = a.ValuePerMinute
            }).ToList();

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
                    Title = r.Title ?? "",
                    Tags = r.Tags ?? "",
                    Status = r.Status ?? "",
                    Description = r.Description ?? "",
                    Activity = new List<ActivityModel>()
                })
                .ToList();

            var byScId = models.ToDictionary(m => m.Id);

            // 2.5) Bulk-load Activity for all CardIDs (same pattern as TAT)
            var cardIds = rows.Select(r => r.CardID).Distinct().ToList();
            var actByCardId = new Dictionary<long, List<ActivityModel>>();

            if (cardIds.Count > 0)
            {
                var placeholders = string.Join(", ", cardIds.Select(_ => "?"));
                var actSql = $@"
                    SELECT
                        ActivityID     AS ActivityID,
                        CardID         AS CardID,
                        Start          AS Start,
                        ""End""        AS End,
                        ValueRateName AS ValueRateName,
                        ValuePerMinute AS ValuePerMinute
                    FROM Activity
                    WHERE CardID IN ({placeholders})
                      AND datetime(Start) < datetime(?)
                      AND (
                            datetime(""End"") >= datetime(?)
                            OR ""End"" = ?
                          )
                    ORDER BY CardID, Start;
                ";

                var actRows = await Db.QueryAsync<ActivityRow>(
                    actSql, 
                    cardIds.Cast<object>()
                    .Append(rangeEnd.ToString("o"))
                    .Append(rangeStart.ToString("o"))
                    .Append(DateTime.MinValue.ToString("o"))
                    .ToArray());

                actByCardId = actRows
                    .GroupBy(a => a.CardID)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(a => new ActivityModel
                        {
                            Id = a.ActivityID,
                            StartDate = ParseIsoDateTime(a.Start),
                            EndDate = ParseIsoDateTime(a.End),
                            ValuePerMinute = a.ValuePerMinute
                        }).ToList()
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
                    t.Description    AS Description
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
                Title = row.Title ?? "",
                Tags = row.Tags ?? "",
                ValuePerMinute = row.ValuePerMinute,
                Status = row.Status ?? "",
                Description = row.Description ?? "",
                Activity = new List<ActivityModel>(),
                ValueRates = new List<ValueRateModel>()
            };

            // 2) Load activity by CardID
            const string actSql = @"
                SELECT
                    ActivityID     AS ActivityID,
                    CardID         AS CardID,
                    Start          AS Start,
                    ""End""        AS End,
                    ValueRateName AS ValueRateName,
                    ValuePerMinute AS ValuePerMinute
                FROM Activity
                WHERE CardID = ?
                ORDER BY Start;
            ";

            var actRows = await Db.QueryAsync<ActivityRow>(actSql, row.CardID);

            model.Activity = actRows.Select(a => new ActivityModel
            {
                Id = a.ActivityID,
                StartDate = ParseIsoDateTime(a.Start),
                EndDate = ParseIsoDateTime(a.End),
                ValuePerMinute = a.ValuePerMinute
            }).ToList();

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
                    t.Description    AS Description
                FROM TatCard t
                JOIN Card c ON c.CardID = t.CardID
            ";

            sql += ";";

            var rows = await Db.QueryAsync<TatCardJoinedRow>(sql);
            if (rows.Count == 0) return new List<TatCardModel>();

            // 2) Bulk-load Activity for all CardIDs
            var cardIds = rows.Select(r => r.CardID).Distinct().ToList();
            var actByCardId = new Dictionary<long, List<ActivityModel>>();

            {
                var placeholders = string.Join(",", cardIds.Select(_ => "?"));
                var actSql = $@"
                    SELECT
                        ActivityID     AS ActivityID,
                        CardID         AS CardID,
                        Start          AS Start,
                        ""End""        AS End,
                        ValueRateName AS ValueRateName,
                        ValuePerMinute AS ValuePerMinute
                    FROM Activity
                    WHERE CardID IN ({placeholders})
                      AND datetime(Start) < datetime(?)
                      AND (
                            datetime(""End"") >= datetime(?)
                            OR ""End"" = ?
                          )
                    ORDER BY CardID, Start;
                ";

                var actRows = await Db.QueryAsync<ActivityRow>(
                    actSql, 
                    cardIds.Cast<object>()
                    .Append(rangeEnd.ToString("o"))
                    .Append(rangeStart.ToString("o"))
                    .Append(DateTime.MinValue.ToString("o"))
                    .ToArray());

                actByCardId = actRows
                    .GroupBy(a => a.CardID)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(a => new ActivityModel
                        {
                            Id = a.ActivityID,
                            StartDate = ParseIsoDateTime(a.Start),
                            EndDate = ParseIsoDateTime(a.End),
                            ValuePerMinute = a.ValuePerMinute
                        }).ToList()
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
                    Title = r.Title ?? "",
                    Tags = r.Tags ?? "",
                    ValuePerMinute = r.ValuePerMinute,
                    Status = r.Status ?? "",
                    Description = r.Description ?? "",
                    Activity = actByCardId.TryGetValue(r.CardID, out var acts) ? acts : new List<ActivityModel>(),
                    ValueRates = vrByTatId.TryGetValue(r.TatCardID, out var vrs) ? vrs : new List<ValueRateModel>()
                };

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
        }

        private sealed class TatValueRateRow
        {
            public int TatCardValueRateID { get; set; }
            public int TatCardID { get; set; }
            public string? RateName { get; set; }
            public double ValuePerMinute { get; set; }
        }
        public async Task<Tuple<DateTime, DateTime>> GetPreviousAndNextActivePeriodDateTimes(DateTime current)
        {
            var currentIso = current.ToString("o");
            var minIso = DateTime.MinValue.ToString("o");

            var sql = @"
                SELECT
                    (
                        SELECT ""End""
                        FROM Activity
                        WHERE ""End"" <> ?
                          AND datetime(""End"") < datetime(?)
                        ORDER BY datetime(""End"") DESC
                        LIMIT 1
                    ) AS PreviousEnd,
                    (
                        SELECT Start
                        FROM Activity
                        WHERE datetime(Start) > datetime(?)
                        ORDER BY datetime(Start) ASC
                        LIMIT 1
                    ) AS NextStart;
            ";

            var rows = await Db.QueryAsync<AdjacentActivityDatesRow>(
                sql,
                minIso,
                currentIso,
                currentIso
            );

            var row = rows.FirstOrDefault();

            var previous = row?.PreviousEnd != null
                ? DateTime.Parse(row.PreviousEnd, null, System.Globalization.DateTimeStyles.RoundtripKind)
                : DateTime.MinValue;

            var next = row?.NextStart != null
                ? DateTime.Parse(row.NextStart, null, System.Globalization.DateTimeStyles.RoundtripKind)
                : DateTime.MaxValue;

            return Tuple.Create(previous, next);
        }

        private sealed class AdjacentActivityDatesRow
        {
            public string? PreviousEnd { get; set; }
            public string? NextStart { get; set; }
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
            var models = rows.Select(r => new ValueTrackerCardModel
            {
                Id = r.ValueTrackerCardID,
                Title = r.Title ?? "",
                Tags = r.Tags ?? "",
                Unit = r.Unit ?? "",
                CreatedDate = ParseIsoDateTime(r.CreatedDate),
                RangeStart = ParseIsoDateTime(r.RangeStart),
                ScheduleEvery = r.ScheduleEvery,
                ScheduleUnit = r.ScheduleUnit ?? "Week"
            }).ToList();

            // Bulk-load values for all trackers
            var cardIds = rows.Select(r => r.CardID).Distinct().ToList();
            var byCardId = models.ToDictionary(m => rows.First(r => r.ValueTrackerCardID == m.Id).CardID);

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

        private sealed class TrackerValueRow
        {
            public int TrackerValueID { get; set; }
            public long CardID { get; set; }
            public string TimeStamp { get; set; } = "";
            public double Value { get; set; }
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
                Title = r.Title ?? "",
                Tags = r.Tags ?? "",
                Unit = r.Unit ?? "",
                CreatedDate = ParseIsoDateTime(r.CreatedDate),
                RangeStart = ParseIsoDateTime(r.RangeStart),
                GroupByPeriod = r.GroupByPeriod ?? "Day"
            }).ToList();

            var cardIds = rows.Select(r => r.CardID).Distinct().ToList();
            var byCardId = models.ToDictionary(m => rows.First(r => r.EventTrackerCardID == m.Id).CardID);

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

        #region Write

        //Achievement
        //private async Task SaveAchievementCardModelDataAsync(AchievementCardModel acm, long cardId)
        //{
        //    // We have some NOT NULL columns in the current table schema that the model doesn't expose.
        //    // Pick stable defaults.
        //    var now = DateTime.Now;
        //    var createdDate = now;
        //    var availableFromDate = now;

        //    // "DueDate" exists in the table but the model has only "Deadline".
        //    // We'll treat Deadline as the due date when present.
        //    var dueDate = acm.Deadline ?? now;

        //    // CompletedDate doesn't exist on the model in a reliable way (CompletedAt is internal set and
        //    // may be default(DateTime)). We'll write it only if it looks meaningful.
        //    string? completedDateText = null;
        //    if (acm.CompletedAt != default)
        //        completedDateText = acm.CompletedAt.ToString("o");

        //    // Last earned is nullable and does exist on the model.
        //    var lastEarnedAtText = acm.LastEarnedAt?.ToString("o");

        //    // Table wants TEXT fields that we can reasonably map:
        //    var subTypeText = acm.Difficulty.ToString();
        //    var progressTypeText = acm.GoalType.ToString();

        //    // Table has RangeAmount (nullable int). Model has RangeAmount always int.
        //    // Only meaningful when CompletionType == Range, but storing it always is harmless.
        //    int? rangeAmount = acm.CompletionType == AchievementCompletionType.Range ? acm.RangeAmount : null;

        //    // Deadline text
        //    var deadlineText = acm.Deadline?.ToString("o");

        //    // Store trophies as newline-separated TEXT in TrophyURLs for now
        //    // (since the model's Trophies is just strings and doesn't match AchievementTrophy table columns).
        //    var trophyUrls = acm.Trophies.Count == 0
        //        ? ""
        //        : string.Join("\n", acm.Trophies.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()));

        //    if (acm.Id == 0)
        //    {
        //        await Db.ExecuteAsync(
        //            @"INSERT INTO AchievementCard
        //              (CardID, Status, Description, SubType, CreatedDate, AvailableFromDate, DueDate,
        //               CompletedDate, LastEarnedAt, ScCardStepID, ProgressType, RangeAmount, Deadline, TrophyURLs)
        //              VALUES
        //              (?,     ?,      ?,           ?,       ?,          ?,                ?,
        //               ?,            ?,           ?,          ?,            ?,          ?,        ?);",
        //            cardId,
        //            acm.Status ?? "",
        //            acm.Description ?? "",
        //            subTypeText,

        //            createdDate.ToString("o"),
        //            availableFromDate.ToString("o"),
        //            dueDate.ToString("o"),

        //            completedDateText,
        //            lastEarnedAtText,

        //            null, // ScCardStepID - model doesn't currently expose a step ID
        //            progressTypeText,
        //            rangeAmount,
        //            deadlineText,
        //            trophyUrls
        //        );

        //        acm.Id = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        //    }
        //    else
        //    {
        //        await Db.ExecuteAsync(
        //            @"UPDATE AchievementCard
        //              SET Status = ?,
        //                  Description = ?,
        //                  SubType = ?,
        //                  DueDate = ?,
        //                  CompletedDate = ?,
        //                  LastEarnedAt = ?,
        //                  ProgressType = ?,
        //                  RangeAmount = ?,
        //                  Deadline = ?,
        //                  TrophyURLs = ?
        //              WHERE CardID = ?;",

        //            acm.Status ?? "",
        //            acm.Description ?? "",
        //            subTypeText,

        //            dueDate.ToString("o"),
        //            completedDateText,
        //            lastEarnedAtText,

        //            progressTypeText,
        //            rangeAmount,
        //            deadlineText,
        //            trophyUrls,

        //            cardId
        //        );
        //    }

        //    // Note:
        //    // You *also* have an AchievementTrophy table in the schema, but the current model exposes only
        //    // ObservableCollection<string> Trophies (no title/earnedOn/imageSource), so we intentionally
        //    // persist trophies into AchievementCard.TrophyURLs until the model/table align. :contentReference[oaicite:2]{index=2} :contentReference[oaicite:3]{index=3}
        //}

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

                if(cardId == null)
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
                    //await SaveAchievementCardModelDataAsync(acm, cardId.Value);
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
                var ids = await Db.QueryScalarsAsync<long> ("SELECT CardID FROM BudgetCard WHERE BudgetCardID = ? LIMIT 1", model.Id);

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

        // =========================
        // New Methods
        // =========================

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

        // This should add an entity in the Activity table.
        // Since the app doesn't know CardID, we resolve it from the typed model.Id and type.
        public async Task<int> AddActivity(IActiveCardModel model, DateTime startTime)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var cardId = await ResolveCardIdForActivityModel(model);

            string rateName = "Base Rate";
            double valuePerMinuteToUse = model.ValuePerMinute;

            if(model is TatCardModel tat && tat.SelectedValueRateModel != null)
            {
                rateName = tat.SelectedValueRateModel.RateName;
                valuePerMinuteToUse = tat.SelectedValueRateModel.ValuePerMinute;
            }

            // Activity.End is NOT NULL in your schema, so we write End=start initially.
            await Db.ExecuteAsync(
                @"INSERT INTO Activity (CardID, Start, ""End"", ValueRateName, ValuePerMinute) VALUES (?, ?, ?, ?, ?);",
                cardId,
                startTime.ToString("o"),
                DateTime.MinValue.ToString("o"),
                rateName,
                valuePerMinuteToUse);

            var activityId = await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            return (int)activityId;
        }

        // As with AddActivity, find the CardID then end the current open activity slice.
        public async Task EndActivity(IActiveCardModel model, DateTime endTime)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var cardId = await ResolveCardIdForActivityModel(model);

            // Prefer an "open" activity where End==Start (how Save* currently tends to create them).
            // If none found, fall back to the most recent activity row.
            var activityId = await Db.ExecuteScalarAsync<long?>(
                @"SELECT ActivityID
                  FROM Activity
                  WHERE CardID = ?
                    AND (""End"" = Start OR ""End"" = '' OR ""End"" IS NULL)
                  ORDER BY Start DESC
                  LIMIT 1;",
                cardId);

            if (activityId == null)
            {
                activityId = await Db.ExecuteScalarAsync<long?>(
                    @"SELECT ActivityID
                      FROM Activity
                      WHERE CardID = ?
                      ORDER BY Start DESC
                      LIMIT 1;",
                    cardId);
            }

            if (activityId == null)
                return; // nothing to end

            await Db.ExecuteAsync(
                @"UPDATE Activity
                  SET ""End"" = ?
                  WHERE ActivityID = ?;",
                endTime.ToString("o"),
                activityId.Value);
        }

        // -------------------------
        // Helper: resolve CardID from typed model.Id
        // -------------------------
        private async Task<long> ResolveCardIdForActivityModel(IActiveCardModel model)
        {
            // model.Id is the "typed" ID (TatCardID / ScCardID / MissionCardID / etc.)
            // We map type -> table -> CardID.
            //
            // Add/remove cases to match whatever cards in your app actually support activity.

            if (model is ScCardModel)
            {
                return await Db.ExecuteScalarAsync<long>(
                    @"SELECT CardID FROM ScCard WHERE ScCardID = ?;",
                    model.Id);
            }

            if (model is TatCardModel)
            {
                return await Db.ExecuteScalarAsync<long>(
                    @"SELECT CardID FROM TatCard WHERE TatCardID = ?;",
                    model.Id);
            }

            if (model is MissionCardModel)
            {
                return await Db.ExecuteScalarAsync<long>(
                    @"SELECT CardID FROM MissionCard WHERE MissionCardID = ?;",
                    model.Id);
            }

            if (model is BudgetCardModel)
            {
                return await Db.ExecuteScalarAsync<long>(
                    @"SELECT CardID FROM BudgetCard WHERE BudgetCardID = ?;",
                    model.Id);
            }

            if (model is AchievementCardModel)
            {
                return await Db.ExecuteScalarAsync<long>(
                    @"SELECT CardID FROM AchievementCard WHERE AchievementCardID = ?;",
                    model.Id);
            }

            throw new NotSupportedException(
                $"Unsupported activity model type: {model.GetType().Name}. " +
                "Add a ResolveCardIdForActivityModel(...) case for it.");
        }


        //Mission
        private async Task SaveMissionCardModelDataAsync(MissionCardModel model, long cardId)
        {
            if (model.Id == 0)
            {
                // Insert the “typed” row (e.g. ScCard)
                await Db.ExecuteAsync(
                    @"INSERT INTO MissionCard (CardID, Status, Description, SubType, Value, CreatedDate, AvailableFromDate, DueDate, CompletedDate, EstCompletionTimeText, IsFailed, ValuePerMinute) 
                      VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);",
                cardId, model.Status, model.Description, model.SubType.ToString(), model.Value, model.CreatedDate.ToString("o"), model.AvailableFromDate.ToString("o"), model.DueDate.ToString("o"), model.CompletedDate?.ToString("o"), model.EstCompletionTimeText, model.IsFailed, model.ValuePerMinute);

                model.Id = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            }
            else
            {
                await Db.ExecuteAsync(
                    "UPDATE MissionCard SET Status = ?, Description = ?, SubType = ?, Value = ?, CreatedDate = ?, AvailableFromDate = ?, DueDate = ?, CompletedDate = ?, EstCompletionTimeText = ?, IsFailed = ?, ValuePerMinute = ? WHERE CardID = ?",
                    model.Status, model.Description, model.SubType.ToString(), model.Value, model.CreatedDate.ToString("o"), model.AvailableFromDate.ToString("o"), model.DueDate.ToString("o"), model.CompletedDate?.ToString("o"), model.EstCompletionTimeText, model.IsFailed, model.ValuePerMinute, cardId);

                foreach (var act in model.Activity)
                {
                    if(act.Id == 0)
                    {
                        await Db.ExecuteAsync("INSERT INTO Activity (CardID, \"Start\", \"End\", ValueRateName, ValuePerMinute) VALUES(?, ?, ?, ?, ?)", cardId, act.StartDate.ToString("o"), act.EndDate.ToString("o"), "Base Rate", act.ValuePerMinute);
                    }
                    else
                    {
                        await Db.ExecuteAsync("UPDATE Activity SET \"Start\" = ? , \"End\" = ?, ValueRateName, ValuePerMinute = ? WHERE ActivityID = ?", act.StartDate.ToString("o"), act.EndDate.ToString("o"), "Base Rate", act.ValuePerMinute, act.Id);
                    }
                }
            }
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

                foreach (var act in model.Activity)
                {
                    if (act.Id == 0)
                    {
                        await Db.ExecuteAsync("INSERT INTO Activity (CardID, \"Start\", \"End\", ValueRateName, ValuePerMinute) VALUES(?, ?, ?, ?, ?)", cardId, act.StartDate.ToString("o"), act.EndDate.ToString("o"), "Base Rate", act.ValuePerMinute);

                        act.Id = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                    }
                    else
                    {
                        await Db.ExecuteAsync("UPDATE Activity SET \"Start\" = ? , \"End\" = ?, ValueRateName = ?, ValuePerMinute = ? WHERE ActivityID = ?", act.StartDate.ToString("o"), act.EndDate.ToString("o"), "Base Rate", act.ValuePerMinute, act.Id);
                    }
                }
            }

            foreach (var step in model.Steps)
            {
                if(step.Id == 0)
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
        }

        //TAT
        private async Task SaveTatModelDataAsync(TatCardModel model, long cardId)
        {
            if (model.Id == 0)
            {
                // Insert the “typed” row (e.g. ScCard)
                await Db.ExecuteAsync(
                    "INSERT INTO TatCard (CardID, ValuePerMinute, Status, Description) VALUES (?, ?, ?, ?);",
                    cardId, model.ValuePerMinute, model.Status, model.Description);

                model.Id = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            }
            else
            {
                await Db.ExecuteAsync(
                    "UPDATE TatCard SET ValuePerMinute = ?, Status = ?, Description = ? WHERE CardID = ?",
                    model.ValuePerMinute, model.Status, model.Description, cardId);
                foreach (var act in model.Activity)
                {
                    if (act.Id == 0)
                    {
                        await Db.ExecuteAsync("INSERT INTO Activity (CardID, \"Start\", \"End\", ValueRateName, ValuePerMinute) VALUES(?, ?, ?, ?, ?)",
                            cardId, act.StartDate.ToString("o"), act.EndDate.ToString("o"), act.RateName ?? "Base Rate", act.ValuePerMinute);
                    }
                    else
                    {
                        await Db.ExecuteAsync("UPDATE Activity SET \"Start\" = ? , \"End\" = ?, ValueRateName = ?, ValuePerMinute = ? WHERE ActivityID = ?", 
                            act.StartDate.ToString("o"), act.EndDate.ToString("o"), act.RateName ?? "Base Rate", act.ValuePerMinute, act.Id);
                    }
                }
            }

            //Do a query to get all of the ValueRates for this Tat in the datbase
            var existingValueRateForThisTatModel = await Db.QueryAsync<TatValueRateRow>("SELECT * FROM TatCardValueRate WHERE TatCardID = ?", model.Id);

            foreach (var vr in model.ValueRates)
            {
                if(vr.Id == 0)
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
        }

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

            await SaveTrackerValuesAsync(cardId, model.Values);
        }

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

    }
}
