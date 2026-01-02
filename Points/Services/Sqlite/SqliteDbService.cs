using Points.Global;
using Points.Models;
using SQLite;
using System;
using System.Collections.Generic;
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

        public string BackupsFolderPath => throw new NotImplementedException();

        public Task BackupAsync()
        {
            throw new NotImplementedException();
        }

        public Task WipeAsync()
        {
            throw new NotImplementedException();
        }

        public Task RestoreAsync(string backupFilePath)
        {
            throw new NotImplementedException();
        }

        public DateTime? GetLastBackupUtc()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Read

        //Achievement
        public async Task<AchievementCardModel> GetAchievementCardModelDataAsync(int id)
        {
            throw new NotImplementedException();
        }

        public async Task<List<AchievementCardModel>> GetAchievementCardModelsDataAsync(string whereClause = null)
        {
            throw new NotImplementedException();
        }

        //Budget
        public async Task<BudgetCardModel> GetBudgetCardModelDataAsync(int id)
        {
            throw new NotImplementedException();
        }

        public async Task<List<BudgetCardModel>> GetBudgetCardModelsDataAsync(string whereClause = null)
        {
            throw new NotImplementedException();
        }

        //Home Seed

        public async Task<HomeSeedData> GetHomeSeedDataAsync()
        {
            var mainQuest = await GetMainQuestModelsDataAsync();

            var mission = await GetMissionCardModelsDataAsync();

            var budget = await GetBudgetCardModelsDataAsync();

            var achievements = await GetAchievementCardModelsDataAsync();

            var seed = new HomeSeedData
            {
                MainQuestCards = mainQuest,
                MissionCards = mission,
                BudgetCards = budget,
                Achievements = achievements
            };

            return seed;
        }

        //Main Quest

        public async Task<List<IActiveCardModel>> GetMainQuestModelsDataAsync(string whereClause = null)
        {
            var tats = await GetTatModelsDataAsync();
            var scs = await GetScModelsDataAsync();

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

            public double ValuePerMinute { get; set; }
        }


        public async Task<List<MissionCardModel>> GetMissionCardModelsDataAsync(string whereClause = null)
        {
            throw new NotImplementedException();
        }

        //SC
        public async Task<ScCardModel> GetScModelDataAsync(int id)
        {
            throw new NotImplementedException();
        }

        public async Task<List<ScCardModel>> GetScModelsDataAsync(string whereClause = null)
        {
            throw new NotImplementedException();
        }

        //TAT
        public async Task<TatCardModel> GetTatModelDataAsync(int id)
        {
            throw new NotImplementedException();
        }

        public async Task<List<TatCardModel>> GetTatModelsDataAsync(string whereClause = null)
        {
            throw new NotImplementedException();
        }

        // ValueRate
        public async Task<ValueRateModel> GetValueRateModelDataAsync(int id)
        {
            throw new NotImplementedException();
        }

        public async Task<List<ValueRateModel>> GetValueRateModelsDataAsync(string whereClause = null)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Write

        //Achievement
        public async Task SaveAchievementCardModelDataAsync(AchievementCardModel model)
        {
            await SaveAchievementCardModelsDataAsync(new List<AchievementCardModel>() { model });
        }

        public async Task SaveAchievementCardModelsDataAsync(List<AchievementCardModel> models)
        {
            throw new NotImplementedException();
        }

        private async Task SaveAchievementCardModelDataAsync(AchievementCardModel acm, long cardId)
        {
            throw new NotImplementedException();
        }

        //Budget
        public async Task SaveBudgetCardModelDataAsync(BudgetCardModel model)
        {
            await SaveBudgetCardModelsDataAsync(new List<BudgetCardModel>() { model });
        }

        public async Task SaveBudgetCardModelsDataAsync(List<BudgetCardModel> models)
        {

        }

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
                }
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
                    await SaveAchievementCardModelDataAsync(acm, cardId.Value);
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

            return null;
        }

        //Mission
        public async Task SaveMissionCardModelDataAsync(MissionCardModel model)
        {
            await SaveMissionCardModelsDataAsync(new List<MissionCardModel>() { model });
        }

        public async Task SaveMissionCardModelsDataAsync(List<MissionCardModel> models)
        {
            throw new NotImplementedException();
        }

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
                        await Db.ExecuteAsync("INSERT INTO Activity (CardID, \"Start\", \"End\", ValuePerMinute) VALUES(?, ?, ?, ?)", cardId, act.StartDate.ToString("o"), act.EndDate.ToString("o"), act.ValuePerMinute);
                    }
                    else
                    {
                        await Db.ExecuteAsync("UPDATE Activity SET \"Start\" = ? , \"End\" = ?, ValuePerMinute = ? WHERE ActivityID = ?", act.StartDate.ToString("o"), act.EndDate.ToString("o"), act.ValuePerMinute, act.Id);
                    }
                }
            }
        }

        //SC
        public async Task SaveScModelDataAsync(ScCardModel model)
        {
            await SaveScModelsDataAsync(new List<ScCardModel>() { model });
        }

        public async Task SaveScModelsDataAsync(List<ScCardModel> models)
        {

        }

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
                        await Db.ExecuteAsync("INSERT INTO Activity (CardID, \"Start\", \"End\", ValuePerMinute) VALUES(?, ?, ?, ?)", cardId, act.StartDate.ToString("o"), act.EndDate.ToString("o"), act.ValuePerMinute);

                        act.Id = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                    }
                    else
                    {
                        await Db.ExecuteAsync("UPDATE Activity SET \"Start\" = ? , \"End\" = ?, ValuePerMinute = ? WHERE ActivityID = ?", act.StartDate.ToString("o"), act.EndDate.ToString("o"), act.ValuePerMinute, act.Id);
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
                    await Db.ExecuteAsync(insertRepSql, step.Id, rep, step.StepValue);
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
                        await Db.ExecuteAsync("INSERT INTO Activity (CardID, \"Start\", \"End\", ValuePerMinute) VALUES(?, ?, ?, ?)", cardId, act.StartDate.ToString("o"), act.EndDate.ToString("o"), act.ValuePerMinute);
                    }
                    else
                    {
                        await Db.ExecuteAsync("UPDATE Activity SET \"Start\" = ? , \"End\" = ?, ValuePerMinute = ? WHERE ActivityID = ?", act.StartDate.ToString("o"), act.EndDate.ToString("o"), act.ValuePerMinute, act.Id);
                    }
                }
            }

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
                }
            }
        }

        public async Task SaveTatModelsDataAsync(List<TatCardModel> models)
        {
            throw new NotImplementedException();
        }

        public Task SaveTatModelDataAsync(TatCardModel model)
        {
            throw new NotImplementedException();
        }


        //ValueRate
        public async Task SaveValueRateModelDataAsync(ValueRateModel model)
        {
            await SaveValueRateModelsDataAsync(new List<ValueRateModel>() { model });
        }

        public async Task SaveValueRateModelsDataAsync(List<ValueRateModel> models)
        {
            throw new NotImplementedException();
        }
        #endregion

    }
}
