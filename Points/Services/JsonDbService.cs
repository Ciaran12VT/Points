//using Points.Models;
//using Points.Models.DbModels;
//using Points.Services.Mappers;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Text.Json;
//using System.Threading.Tasks;

//namespace Points.Services
//{
//    internal class JsonDbService : IDbService
//    {

//        private static readonly JsonSerializerOptions _jsonOptions = new()
//        {
//            WriteIndented = true
//            // Add converters here if you need (enums as strings, etc.)
//        };

//        private static string CardDbModelListFileName = "cards.json";
//        private static string TatCardDbModelListFileName = "tat_cards.json";
//        private static string ValueRateDbModelListFileName = "tat_value_rates.json";
//        private static string ActivityDbModelListFileName = "activities.json";
//        private static string ScCardDbModelListFileName = "sc_cards.json";
//        private static string ScCardStepDbModelListFileName = "sc_steps.json";
//        private static string ScCardStepRepDbModelListFileName = "sc_step_reps.json";
//        private static string MissionCardDbModelListFileName = "mission_cards.json";
//        private static string BudgetCardDbModelListFileName = "budget_cards.json";
//        private static string BudgetCardScheduledTopUpDbModelListFileName = "budget_scheduled_topups.json";
//        private static string BudgetCardTransactionDbModelListFileName = "budget_transactions.json";
//        private static string AchievementDbModelListFileName = "achievements.json";
//        private static string AchievementTrophyDbModelListFileName = "achievement_trophies.json";
//        private static string ReportDbModelListFileName = "reports.json";

//        private readonly TatAggregateMapper _tatMapper = new();

//        public static string GetFilePath(string fileName)
//        {
//            string scopedStoragePath = FileSystem.AppDataDirectory;
//            return System.IO.Path.Combine(scopedStoragePath, fileName);
//        }
//        public static void WriteToFile(string fileName, string content)
//        {
//            var filePath = GetFilePath(fileName);
//            System.IO.File.WriteAllText(filePath, content);
//        }
//        public static string ReadFromFile(string fileName)
//        {
//            var filePath = GetFilePath(fileName);
//            if (System.IO.File.Exists(filePath))
//            {
//                return System.IO.File.ReadAllText(filePath);
//            }
//            return null;
//        }


//        public string BackupsFolderPath => throw new NotImplementedException();

//        public Task BackupAsync()
//        {
//            throw new NotImplementedException();
//        }

//        public async Task<List<AchievementCardModel>> GetAchievementCardModelDataAsync()
//        {
//            var achievementDbRows = await JsonFileStore.ReadListAsync<AchievementDbModel>(AchievementDbModelListFileName);
//            var trophyDbRows = await JsonFileStore.ReadListAsync<AchievementTrophyDbModel>(AchievementTrophyDbModelListFileName);
//            var cardDbRows = await JsonFileStore.ReadListAsync<CardDbModel>(CardDbModelListFileName);

//            achievementDbRows ??= new List<AchievementDbModel>();
//            trophyDbRows ??= new List<AchievementTrophyDbModel>();
//            cardDbRows ??= new List<CardDbModel>();

//            var cardsById = cardDbRows.ToDictionary(c => c.CardID);
//            var trophiesByAchievementId = trophyDbRows
//                .GroupBy(t => t.AchievementID)
//                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.EarnedOn).ToList());

//            var result = new List<AchievementCardModel>();

//            foreach (var a in achievementDbRows)
//            {
//                cardsById.TryGetValue(a.CardID, out var card);

//                // TargetType from ProgressType (fallback ActiveTime)
//                var targetType = AchievementTargetType.ActiveTime;
//                if (!string.IsNullOrWhiteSpace(a.ProgressType))
//                    Enum.TryParse(a.ProgressType, ignoreCase: true, out targetType);

//                // Difficulty from SubType (fallback Easy)
//                var difficulty = AchievementDifficultyLevels.Easy;
//                if (!string.IsNullOrWhiteSpace(a.SubType))
//                    Enum.TryParse(a.SubType, ignoreCase: true, out difficulty);

//                // CompletionType: if Deadline set -> Deadline, else Range
//                var completionType = a.Deadline != null
//                    ? AchievementCompletionType.Deadline
//                    : AchievementCompletionType.Range;

//                var model = new AchievementCardModel
//                {
//                    Id = a.AchievementID,
//                    Title = card?.Title ?? "New Achievement",
//                    Tags = card?.Tags ?? "",

//                    Status = a.Status ?? "",
//                    Difficulty = difficulty,

//                    TargetType = targetType,
//                    TargetValue = a.RangeAmount ?? 1,              // best available numeric field right now
//                    Deadline = a.Deadline,
//                    CompletionType = completionType,

//                    RangeAmount = a.RangeAmount ?? 7,
//                    // RangeUnit currently not persisted; keep default (Days)
//                    // ActiveTimeTargetText currently not persisted; leave empty for now

//                    LastEarnedAt = a.LastEarnedAt,

//                    // CurrentValue not persisted yet
//                    CurrentValue = 0
//                };

//                // If you want to preserve Created/Available/Due/Completed later, add matching fields on AchievementCardModel.

//                if (trophiesByAchievementId.TryGetValue(a.AchievementID, out var trophies))
//                {
//                    // AchievementCardModel.Trophies is ObservableCollection<string>
//                    // We’ll store ImageSource when available, otherwise Title.
//                    foreach (var t in trophies)
//                    {
//                        var val = !string.IsNullOrWhiteSpace(t.ImageSource) ? t.ImageSource : (t.Title ?? "");
//                        if (!string.IsNullOrWhiteSpace(val))
//                            model.Trophies.Add(val);
//                    }
//                }

//                result.Add(model);
//            }

//            return result.OrderBy(x => x.Title).ToList();
//        }

//        public async Task<List<BudgetCardModel>> GetBudgetCardModelDataAsync()
//        {
//            var budgetDbRows = await JsonFileStore.ReadListAsync<BudgetCardDbModel>(BudgetCardDbModelListFileName);
//            var topUpDbRows = await JsonFileStore.ReadListAsync<BudgetCardScheduledTopUpDbModel>(BudgetCardScheduledTopUpDbModelListFileName);
//            var txDbRows = await JsonFileStore.ReadListAsync<BudgetCardTransactionDbModel>(BudgetCardTransactionDbModelListFileName);
//            var cardDbRows = await JsonFileStore.ReadListAsync<CardDbModel>(CardDbModelListFileName);

//            budgetDbRows ??= new List<BudgetCardDbModel>();
//            topUpDbRows ??= new List<BudgetCardScheduledTopUpDbModel>();
//            txDbRows ??= new List<BudgetCardTransactionDbModel>();
//            cardDbRows ??= new List<CardDbModel>();

//            var cardsById = cardDbRows.ToDictionary(c => c.CardID);

//            var topUpsByBudgetId = topUpDbRows
//                .GroupBy(t => t.BudgetCardID)
//                .ToDictionary(g => g.Key, g => g.ToList());

//            var txByBudgetId = txDbRows
//                .GroupBy(t => t.BudgetCardID)
//                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.TimeStamp).ToList());

//            var result = new List<BudgetCardModel>();

//            foreach (var b in budgetDbRows)
//            {
//                cardsById.TryGetValue(b.CardID, out var card);

//                var model = new BudgetCardModel
//                {
//                    Id = b.BudgetCardID,
//                    Title = card?.Title ?? "Budget",
//                    Tags = card?.Tags ?? "",
//                    Status = b.Status ?? "",
//                    Description = b.Description ?? "",
//                    Currency = b.Currency ?? "",
//                    ExchangeRate = b.ExchangeRate,
//                    StartDate = b.StartDate,
//                    InitialBalance = b.InitialBalance
//                };

//                if (topUpsByBudgetId.TryGetValue(b.BudgetCardID, out var tus))
//                {
//                    foreach (var tu in tus)
//                    {
//                        model.TopUps.Add(new ScheduledTopUp
//                        {
//                            // If you later store time-of-day separately, map that here.
//                            // For now your DbModel only has NextTopUpAt, so we use its TimeOfDay.
//                            Id = tu.BudgetCardScheduledTopUpID,
//                            TimeOfDay = tu.TimeOfDay,
//                            Amount = tu.Amount
//                        });
//                    }
//                }

//                if (txByBudgetId.TryGetValue(b.BudgetCardID, out var txs))
//                {
//                    foreach (var tx in txs)
//                    {
//                        model.Transactions.Add(new BudgetTransaction
//                        {
//                            Id = tx.BudgetCardTransactionID,
//                            Timestamp = tx.TimeStamp,
//                            Type = string.Equals(tx.Type, "CashIn", StringComparison.OrdinalIgnoreCase)
//                                ? BudgetTransactionType.CashIn
//                                : BudgetTransactionType.Spend,
//                            CurrencyAmount = tx.Amount,

//                            // If it’s a cash-in, this should be Amount * ExchangeRate-at-the-time;
//                            // DbModel currently only stores Amount, so we recompute using current ExchangeRate.
//                            GlobalValueAmount = string.Equals(tx.Type, "CashIn", StringComparison.OrdinalIgnoreCase)
//                                ? tx.Amount * model.ExchangeRate
//                                : 0
//                        });
//                    }
//                }

//                result.Add(model);
//            }

//            return result.OrderBy(x => x.Title).ToList();
//        }

//        public async Task<HomeSeedData> GetHomeSeedDataAsync()
//        {
//            var mainQuest = await GetMainQuestModelDataAsync();

//            var mission = await GetMissionCardModelDataAsync();

//            var budget = await GetBudgetCardModelDataAsync();

//            var achievements = await GetAchievementCardModelDataAsync();

//            var seed = new HomeSeedData
//            {
//                MainQuestCards = mainQuest,
//                MissionCards = mission,
//                BudgetCards = budget,
//                Achievements = achievements
//            };

//            return seed;
//        }

//        public DateTime? GetLastBackupUtc()
//        {
//            return DateTime.Now;
//        }

//        public async Task<List<IActiveCardModel>> GetMainQuestModelDataAsync()
//        {
//            var tats = await GetTatModelDataAsync();
//            var scs = await GetScModelDataAsync();

//            var mainQuest = new List<IActiveCardModel>();
//            mainQuest.AddRange(tats);
//            mainQuest.AddRange(scs);

//            return mainQuest;
//        }

//        public async Task<List<MissionCardModel>> GetMissionCardModelDataAsync()
//        {
//            var missionDbRows = await JsonFileStore.ReadListAsync<MissionCardDbModel>(MissionCardDbModelListFileName);
//            var cardDbRows = await JsonFileStore.ReadListAsync<CardDbModel>(CardDbModelListFileName);
//            var activityDbRows = await JsonFileStore.ReadListAsync<ActivityDbModel>(ActivityDbModelListFileName);

//            missionDbRows ??= new List<MissionCardDbModel>();
//            cardDbRows ??= new List<CardDbModel>();
//            activityDbRows ??= new List<ActivityDbModel>();

//            var cardsById = cardDbRows.ToDictionary(c => c.CardID);
//            var activitiesByCardId = activityDbRows
//                .GroupBy(a => a.CardID)
//                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Start).ToList());

//            var result = new List<MissionCardModel>();

//            foreach (var m in missionDbRows)
//            {
//                cardsById.TryGetValue(m.CardID, out var card);

//                // Parse SubType string -> enum (fallback Stable)
//                var subType = MissionSubType.Stable;
//                if (!string.IsNullOrWhiteSpace(m.SubType))
//                    Enum.TryParse(m.SubType, ignoreCase: true, out subType);

//                var model = new MissionCardModel
//                {
//                    Id = m.MissionCardID,
//                    Title = card?.Title ?? "Mission",
//                    Tags = card?.Tags ?? "",

//                    Status = m.Status ?? "",
//                    Description = m.Description ?? "",
//                    SubType = subType,

//                    Value = m.Value,
//                    CreatedDate = m.CreatedDate,
//                    AvailableFromDate = m.AvailableFromDate,
//                    DueDate = m.DueDate,

//                    // Your MissionCardModel uses ValuePerMinute for activity value stream
//                    ValuePerMinute = m.ValuePerMinute,

//                    // Keep EstCompletionTime in sync with the text stored
//                    EstCompletionTime = TryParseTimeSpanFromHms(m.EstCompletionTimeText)
//                };

//                // Activities
//                if (activitiesByCardId.TryGetValue(m.CardID, out var acts))
//                {
//                    model.Activity = acts
//                        .Select(a => new ActivityModel(
//                            a.Start,
//                            a.End,
//                            rate: "Base Rate",
//                            value: m.ValuePerMinute
//                        ))
//                        .ToList();
//                }

//                // Completion / failure state:
//                // Your MissionCardModel has private setters, so we restore state by calling methods.
//                if (m.IsFailed)
//                {
//                    model.Fail(m.CompletedDate);
//                }
//                else if (m.CompletedDate != null)
//                {
//                    model.Complete(m.CompletedDate);
//                }

//                result.Add(model);
//            }

//            return result.OrderBy(x => x.Title).ToList();


//            static TimeSpan? TryParseTimeSpanFromHms(string? text)
//            {
//                if (string.IsNullOrWhiteSpace(text)) return null;

//                // Your UI formats as H:MM:SS (hours can exceed 24), so split manually.
//                var parts = text.Split(':');
//                if (parts.Length != 3) return null;

//                if (!int.TryParse(parts[0], out var h)) return null;
//                if (!int.TryParse(parts[1], out var m)) return null;
//                if (!int.TryParse(parts[2], out var s)) return null;

//                return new TimeSpan(h, m, s);
//            }
//        }

//        public async Task<List<ScCardModel>> GetScModelDataAsync()
//        {
//            // “Tables”
//            var scDbRows = await JsonFileStore.ReadListAsync<ScCardDbModel>(ScCardDbModelListFileName);
//            var cardDbRows = await JsonFileStore.ReadListAsync<CardDbModel>(CardDbModelListFileName);
//            var stepDbRows = await JsonFileStore.ReadListAsync<ScCardStepDbModel>(ScCardStepDbModelListFileName);
//            var repDbRows = await JsonFileStore.ReadListAsync<ScCardStepRepDbModel>(ScCardStepRepDbModelListFileName);

//            scDbRows ??= new List<ScCardDbModel>();
//            cardDbRows ??= new List<CardDbModel>();
//            stepDbRows ??= new List<ScCardStepDbModel>();
//            repDbRows ??= new List<ScCardStepRepDbModel>();

//            var cardsById = cardDbRows.ToDictionary(c => c.CardID);

//            // steps grouped by ScCardID
//            var stepsByScId = stepDbRows
//                .GroupBy(s => s.ScCardID)
//                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Order).ToList());

//            // reps grouped by ScCardStepID
//            var repsByStepId = repDbRows
//                .GroupBy(r => r.ScCardStepID)
//                .ToDictionary(g => g.Key, g => g.Select(x => x.TimeStamp).OrderBy(d => d).ToList());

//            var result = new List<ScCardModel>();

//            foreach (var sc in scDbRows)
//            {
//                cardsById.TryGetValue(sc.CardID, out var card);

//                var model = new ScCardModel
//                {
//                    Id = sc.ScCardID,
//                    Title = card?.Title ?? "SC Card",
//                    Tags = card?.Tags ?? "",
//                    Status = sc.Status ?? "",

//                    // NOTE:
//                    // Your ScCardDbModel currently doesn’t store ValuePerMinute sign (which your ScCardModel uses).
//                    // Defaulting to +1 for now.
//                    ValuePerMinute = 0
//                };

//                if (stepsByScId.TryGetValue(sc.ScCardID, out var steps))
//                {
//                    foreach (var s in steps)
//                    {
//                        var stepModel = new ScStepModel
//                        {
//                            Id = s.ScCardStepID,
//                            Order = s.Order,
//                            Title = s.Title ?? "",
//                            StepValue = s.StepValue
//                        };

//                        if (repsByStepId.TryGetValue(s.ScCardStepID, out var reps))
//                        {
//                            // Reps is a public List<DateTime> field in ScStepModel
//                            stepModel.Reps.AddRange(reps);
//                            // RepsVersion is private-set; UI refresh is handled elsewhere in your runtime logic.
//                        }

//                        model.Steps.Add(stepModel);
//                    }
//                }

//                result.Add(model);
//            }

//            return result.OrderBy(x => x.Title).ToList();
//        }

//        public async Task<List<TatCardModel>> GetTatModelDataAsync()
//        {
//            // 1) Read each "table" (json file -> list of DbModels)
//            var tatDbRows = await JsonFileStore.ReadListAsync<TatCardDbModel>(TatCardDbModelListFileName);
//            var cardDbRows = await JsonFileStore.ReadListAsync<CardDbModel>(CardDbModelListFileName);
//            var actDbRows = await JsonFileStore.ReadListAsync<ActivityDbModel>(ActivityDbModelListFileName);
//            var rateDbRows = await JsonFileStore.ReadListAsync<ValueRateDbModel>(ValueRateDbModelListFileName);

//            // Normalize nulls to empty lists
//            tatDbRows ??= new List<TatCardDbModel>();
//            cardDbRows ??= new List<CardDbModel>();
//            actDbRows ??= new List<ActivityDbModel>();
//            rateDbRows ??= new List<ValueRateDbModel>();

//            // 2) Map Db -> business models (aggregate mapping)
//            var tatModels = _tatMapper.MapToModels(tatDbRows, cardDbRows, actDbRows, rateDbRows);

//            // 3) Optional: sort if you want stable ordering
//            return tatModels.OrderBy(t => t.Title).ToList();
//        }

//        public async Task<List<ValueRateModel>> GetValueRateModelDataAsync()
//        {
//            var rateDbRows = await JsonFileStore.ReadListAsync<ValueRateDbModel>(ValueRateDbModelListFileName);
//            rateDbRows ??= new List<ValueRateDbModel>();

//            var result = rateDbRows
//                .Select(r => new ValueRateModel
//                {
//                    RateName = r.RateName ?? "",
//                    ValuePerMinute = r.ValuePerMinute
//                })
//                .OrderBy(r => r.RateName)
//                .ToList();

//            return result;
//        }

//        public async Task RestoreAsync(string backupFilePath)
//        {
//            throw new NotImplementedException();
//        }

//        public async Task WipeAsync()
//        {
//            // All "table" files used by JsonDbService
//            var files = new[]
//            {
//                CardDbModelListFileName,

//                TatCardDbModelListFileName,
//                ActivityDbModelListFileName,
//                ValueRateDbModelListFileName,

//                ScCardDbModelListFileName,
//                ScCardStepDbModelListFileName,
//                ScCardStepRepDbModelListFileName,

//                MissionCardDbModelListFileName,

//                BudgetCardDbModelListFileName,
//                BudgetCardScheduledTopUpDbModelListFileName,
//                BudgetCardTransactionDbModelListFileName,

//                AchievementDbModelListFileName,
//                AchievementTrophyDbModelListFileName,
//            };

//            foreach (var file in files)
//            {
//                await DeleteFileSafeAsync(file);
//            }
//        }

//        private static async Task DeleteFileSafeAsync(string fileName)
//        {
//            var filePath = JsonFileStore.GetFilePath(fileName);

//            // Use the same per-file semaphore as JsonFileStore
//            var gate = typeof(JsonFileStore)
//                .GetMethod("GetLock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
//                .Invoke(null, new object[] { filePath }) as SemaphoreSlim;

//            if (gate == null)
//                return;

//            await gate.WaitAsync().ConfigureAwait(false);
//            try
//            {
//                if (File.Exists(filePath))
//                    File.Delete(filePath);
//            }
//            finally
//            {
//                gate.Release();
//            }
//        }

//        private static int NextId<T>(IEnumerable<T> rows, Func<T, int> idSelector)
//        {
//            if (rows == null) return 1;
//            var max = 0;
//            foreach (var r in rows)
//                max = Math.Max(max, idSelector(r));
//            return max + 1;
//        }

//        public async Task SaveTatModelDataAsync(List<TatCardModel> models)
//        {
//            models ??= new List<TatCardModel>();

//            // Load tables
//            var cards = await JsonFileStore.ReadListAsync<CardDbModel>(CardDbModelListFileName) ?? new();
//            var tats = await JsonFileStore.ReadListAsync<TatCardDbModel>(TatCardDbModelListFileName) ?? new();
//            var acts = await JsonFileStore.ReadListAsync<ActivityDbModel>(ActivityDbModelListFileName) ?? new();
//            var rates = await JsonFileStore.ReadListAsync<ValueRateDbModel>(ValueRateDbModelListFileName) ?? new();

//            // Next IDs (mimic AUTOINCREMENT)
//            var nextTatId = NextId(tats, t => t.TatCardID);
//            var nextCardId = NextId(cards, c => c.CardID);
//            var nextActivityId = NextId(acts, a => a.ActivityID);
//            var nextRateId = NextId(rates, r => r.TatCardValueRateID);

//            // Indexes for lookup
//            var tatById = tats.ToDictionary(t => t.TatCardID);
//            var cardById = cards.ToDictionary(c => c.CardID);

//            // We will mutate these lists and then write them back
//            // (simple + clear for now; SQLite will be more efficient later)
//            foreach (var model in models)
//            {
//                if (model == null) continue;

//                // Ensure TatCardID exists
//                if (model.Id <= 0)
//                    model.Id = nextTatId++;

//                var tatId = model.Id;

//                // 1) Find existing tat row (by TatCardID)
//                tatById.TryGetValue(tatId, out var existingTat);

//                int cardId;
//                CardDbModel? existingCard = null;

//                if (existingTat != null)
//                {
//                    // Existing: reuse its CardID (DO NOT assume equals)
//                    cardId = existingTat.CardID;
//                    cardById.TryGetValue(cardId, out existingCard);
//                }
//                else
//                {
//                    // New: create a fresh CardID
//                    cardId = nextCardId++;
//                }

//                // 2) Map + upsert Card row
//                var updatedCard = _tatMapper.MapToCardDb(model, cardId);

//                if (existingCard != null)
//                {
//                    // update in-place
//                    existingCard.Title = updatedCard.Title;
//                    existingCard.Tags = updatedCard.Tags;
//                    // keep existingCard.Id as-is (or set if blank)
//                    //if (string.IsNullOrWhiteSpace(existingCard.Id))
//                    //    existingCard.Id = updatedCard.Id;
//                }
//                else
//                {
//                    // insert
//                    cards.Add(updatedCard);
//                    cardById[cardId] = updatedCard;
//                }

//                // 3) Map + upsert Tat row
//                var updatedTat = _tatMapper.MapToTatDb(model, tatId, cardId);

//                if (existingTat != null)
//                {
//                    existingTat.CardID = updatedTat.CardID;
//                    existingTat.ValuePerMinute = updatedTat.ValuePerMinute;
//                    existingTat.Status = updatedTat.Status;
//                    existingTat.Description = updatedTat.Description;
//                }
//                else
//                {
//                    tats.Add(updatedTat);
//                    tatById[tatId] = updatedTat;
//                }

//                // 4) Replace child rows (simple strategy)
//                // Activities are CardID-linked
//                acts.RemoveAll(a => a.CardID == cardId);
//                var newActs = _tatMapper.MapToActivityDbRows(model, cardId);
//                foreach (var a in newActs)
//                    a.ActivityID = nextActivityId++;
//                acts.AddRange(newActs);

//                // Value rates are TatCardID-linked
//                rates.RemoveAll(r => r.TatCardID == tatId);
//                var newRates = _tatMapper.MapToValueRateDbRows(model, tatId);
//                foreach (var r in newRates)
//                    r.TatCardValueRateID = nextRateId++;
//                rates.AddRange(newRates);
//            }

//            // Persist tables (each json file = a "table")
//            await JsonFileStore.WriteListAsync(CardDbModelListFileName, cards);
//            await JsonFileStore.WriteListAsync(TatCardDbModelListFileName, tats);
//            await JsonFileStore.WriteListAsync(ActivityDbModelListFileName, acts);
//            await JsonFileStore.WriteListAsync(ValueRateDbModelListFileName, rates);
//        }

//        public async Task SaveScModelDataAsync(List<ScCardModel> models)
//        {
//            models ??= new List<ScCardModel>();

//            // Read existing "tables"
//            var existingCards = await JsonFileStore.ReadListAsync<CardDbModel>(CardDbModelListFileName) ?? new();
//            var existingScs = await JsonFileStore.ReadListAsync<ScCardDbModel>(ScCardDbModelListFileName) ?? new();
//            var existingSteps = await JsonFileStore.ReadListAsync<ScCardStepDbModel>(ScCardStepDbModelListFileName) ?? new();
//            var existingReps = await JsonFileStore.ReadListAsync<ScCardStepRepDbModel>(ScCardStepRepDbModelListFileName) ?? new();

//            // Next IDs (IMPORTANT: CardID and ScCardID are independent)
//            var nextCardId = NextId(existingCards, c => c.CardID);
//            var nextScId = NextId(existingScs, s => s.ScCardID);
//            var nextStepId = NextId(existingSteps, s => s.ScCardStepID);


//            // Assign IDs to new SC cards and new steps
//            foreach (var m in models)
//            {
//                if (m.Id <= 0)
//                    m.Id = nextScId++;

//                if (m.Steps != null)
//                {
//                    foreach (var step in m.Steps)
//                    {
//                        if (step.Id <= 0)
//                            step.Id = nextStepId++;
//                    }
//                }
//            }

//            // We'll build outputs fresh based on the incoming model list (table replacement semantics)
//            var outCards = new List<CardDbModel>();
//            var outScs = new List<ScCardDbModel>();
//            var outSteps = new List<ScCardStepDbModel>();
//            var outReps = new List<ScCardStepRepDbModel>();

//            // Helper: resolve CardID for a given ScCardID (create Card row + link row if new)
//            int ResolveCardIdForSc(int scCardId)
//            {
//                var existingSc = existingScs.FirstOrDefault(x => x.ScCardID == scCardId);
//                if (existingSc != null && existingSc.CardID > 0)
//                    return existingSc.CardID;

//                // brand new SC card => allocate a new Card row and link it
//                var newCardId = nextCardId++;
//                existingScs.Add(new ScCardDbModel
//                {
//                    ScCardID = scCardId,
//                    CardID = newCardId,
//                    Status = "" // will be overwritten below when we emit outScs
//                });
//                return newCardId;
//            }

//            foreach (var m in models)
//            {
//                var scId = m.Id;
//                var cardId = ResolveCardIdForSc(scId);

//                // Card row
//                outCards.Add(new CardDbModel
//                {
//                    CardID = cardId,
//                    Title = m.Title ?? "",
//                    Tags = m.Tags ?? ""
//                });

//                // Sc row (note: CardID is NOT scId)
//                outScs.Add(new ScCardDbModel
//                {
//                    ScCardID = scId,
//                    CardID = cardId,
//                    Status = m.Status ?? ""
//                });

//                // Steps + reps
//                if (m.Steps != null)
//                {
//                    foreach (var s in m.Steps.OrderBy(x => x.Order))
//                    {
//                        outSteps.Add(new ScCardStepDbModel
//                        {
//                            ScCardStepID = s.Id,
//                            ScCardID = scId,
//                            Order = s.Order,
//                            Title = s.Title ?? "",
//                            StepValue = s.StepValue
//                        });

//                        if (s.Reps != null)
//                        {
//                            foreach (var rep in s.Reps.OrderBy(d => d))
//                            {
//                                outReps.Add(new ScCardStepRepDbModel
//                                {
//                                    ScCardStepID = s.Id,
//                                    TimeStamp = rep
//                                });
//                            }
//                        }
//                    }
//                }
//            }

//            // Write "tables"
//            await JsonFileStore.WriteListAsync(CardDbModelListFileName, outCards);
//            await JsonFileStore.WriteListAsync(ScCardDbModelListFileName, outScs);
//            await JsonFileStore.WriteListAsync(ScCardStepDbModelListFileName, outSteps);
//            await JsonFileStore.WriteListAsync(ScCardStepRepDbModelListFileName, outReps);
//        }

//        public async Task SaveMissionCardModelDataAsync(List<MissionCardModel> models)
//        {
//            models ??= new List<MissionCardModel>();

//            var existingCards = await JsonFileStore.ReadListAsync<CardDbModel>(CardDbModelListFileName) ?? new();
//            var existingMissions = await JsonFileStore.ReadListAsync<MissionCardDbModel>(MissionCardDbModelListFileName) ?? new();
//            var existingActs = await JsonFileStore.ReadListAsync<ActivityDbModel>(ActivityDbModelListFileName) ?? new();

//            // Independent sequences
//            var nextCardId = NextId(existingCards, c => c.CardID);
//            var nextMissionId = NextId(existingMissions, m => m.MissionCardID);
//            var nextActivityId = NextId(existingActs, a => a.ActivityID);

//            // Allocate Mission IDs for new models
//            foreach (var m in models)
//            {
//                if (m.Id <= 0)
//                    m.Id = nextMissionId++;
//            }

//            // Helper: resolve CardID for a MissionCardID
//            int ResolveCardIdForMission(int missionId)
//            {
//                var existing = existingMissions.FirstOrDefault(x => x.MissionCardID == missionId);
//                if (existing != null && existing.CardID > 0)
//                    return existing.CardID;

//                var newCardId = nextCardId++;
//                existingMissions.Add(new MissionCardDbModel
//                {
//                    MissionCardID = missionId,
//                    CardID = newCardId
//                });
//                return newCardId;
//            }

//            var outCards = new List<CardDbModel>();
//            var outMissions = new List<MissionCardDbModel>();
//            var outActs = new List<ActivityDbModel>();

//            foreach (var m in models)
//            {
//                var missionId = m.Id;
//                var cardId = ResolveCardIdForMission(missionId);

//                outCards.Add(new CardDbModel
//                {
//                    CardID = cardId,
//                    Title = m.Title ?? "",
//                    Tags = m.Tags ?? ""
//                });

//                outMissions.Add(new MissionCardDbModel
//                {
//                    MissionCardID = missionId,
//                    CardID = cardId,

//                    Status = m.Status ?? "",
//                    Description = m.Description ?? "",
//                    SubType = m.SubType.ToString(),

//                    Value = m.Value,

//                    CreatedDate = m.CreatedDate,
//                    AvailableFromDate = m.AvailableFromDate,
//                    DueDate = m.DueDate,
//                    CompletedDate = m.CompletedDate,

//                    EstCompletionTimeText = m.EstCompletionTime.HasValue
//                        ? ToHms(m.EstCompletionTime.Value)
//                        : "",

//                    IsFailed = m.IsFailed,
//                    ValuePerMinute = m.ValuePerMinute
//                });

//                // Activities linked by *CardID* (per your schema)
//                if (m.Activity != null)
//                {
//                    foreach (var a in m.Activity.OrderBy(x => x.StartDate))
//                    {
//                        outActs.Add(new ActivityDbModel
//                        {
//                            ActivityID = nextActivityId++,
//                            CardID = cardId,
//                            Start = a.StartDate,
//                            End = a.EndDate
//                        });
//                    }
//                }
//            }

//            await JsonFileStore.WriteListAsync(CardDbModelListFileName, outCards);
//            await JsonFileStore.WriteListAsync(MissionCardDbModelListFileName, outMissions);
//            await JsonFileStore.WriteListAsync(ActivityDbModelListFileName, outActs);

//            static string ToHms(TimeSpan ts)
//            {
//                var totalHours = (int)Math.Floor(ts.TotalHours);
//                return $"{totalHours}:{ts.Minutes:00}:{ts.Seconds:00}";
//            }
//        }


//        public async Task SaveBudgetCardModelDataAsync(List<BudgetCardModel> models)
//        {
//            models ??= new List<BudgetCardModel>();

//            var existingCards = await JsonFileStore.ReadListAsync<CardDbModel>(CardDbModelListFileName) ?? new();
//            var existingBudgets = await JsonFileStore.ReadListAsync<BudgetCardDbModel>(BudgetCardDbModelListFileName) ?? new();
//            var existingTopUps = await JsonFileStore.ReadListAsync<BudgetCardScheduledTopUpDbModel>(BudgetCardScheduledTopUpDbModelListFileName) ?? new();
//            var existingTx = await JsonFileStore.ReadListAsync<BudgetCardTransactionDbModel>(BudgetCardTransactionDbModelListFileName) ?? new();

//            // Independent sequences
//            var nextCardId = NextId(existingCards, c => c.CardID);
//            var nextBudgetId = NextId(existingBudgets, b => b.BudgetCardID);
//            var nextTopUpId = NextId(existingTopUps, t => t.BudgetCardScheduledTopUpID);
//            var nextTxId = NextId(existingTx, t => t.BudgetCardTransactionID);

//            // Allocate IDs for new budget cards + new child rows
//            foreach (var m in models)
//            {
//                if (m.Id <= 0)
//                    m.Id = nextBudgetId++;

//                if (m.TopUps != null)
//                    foreach (var tu in m.TopUps)
//                        if (tu.Id <= 0) tu.Id = nextTopUpId++;

//                if (m.Transactions != null)
//                    foreach (var tx in m.Transactions)
//                        if (tx.Id <= 0) tx.Id = nextTxId++;
//            }

//            int ResolveCardIdForBudget(int budgetId)
//            {
//                var existing = existingBudgets.FirstOrDefault(x => x.BudgetCardID == budgetId);
//                if (existing != null && existing.CardID > 0)
//                    return existing.CardID;

//                var newCardId = nextCardId++;
//                existingBudgets.Add(new BudgetCardDbModel
//                {
//                    BudgetCardID = budgetId,
//                    CardID = newCardId
//                });
//                return newCardId;
//            }

//            var outCards = new List<CardDbModel>();
//            var outBudgets = new List<BudgetCardDbModel>();
//            var outTopUps = new List<BudgetCardScheduledTopUpDbModel>();
//            var outTx = new List<BudgetCardTransactionDbModel>();

//            foreach (var m in models)
//            {
//                var budgetId = m.Id;
//                var cardId = ResolveCardIdForBudget(budgetId);

//                outCards.Add(new CardDbModel
//                {
//                    CardID = cardId,
//                    Title = m.Title ?? "",
//                    Tags = m.Tags ?? ""
//                });

//                outBudgets.Add(new BudgetCardDbModel
//                {
//                    BudgetCardID = budgetId,
//                    CardID = cardId,
//                    Status = m.Status ?? "",
//                    Description = m.Description ?? "",

//                    // Use your *updated* BudgetCardDbModel fields here:
//                    Currency = m.Currency ?? "",
//                    ExchangeRate = m.ExchangeRate,
//                    StartDate = m.StartDate,
//                    InitialBalance = m.InitialBalance
//                });

//                if (m.TopUps != null)
//                {
//                    foreach (var tu in m.TopUps)
//                    {
//                        outTopUps.Add(new BudgetCardScheduledTopUpDbModel
//                        {
//                            BudgetCardScheduledTopUpID = tu.Id,
//                            BudgetCardID = budgetId,
//                            Amount = tu.Amount,
//                            TimeOfDay = tu.TimeOfDay
//                        });
//                    }
//                }

//                if (m.Transactions != null)
//                {
//                    foreach (var tx in m.Transactions.OrderBy(t => t.Timestamp))
//                    {
//                        outTx.Add(new BudgetCardTransactionDbModel
//                        {
//                            BudgetCardTransactionID = tx.Id,
//                            BudgetCardID = budgetId,
//                            Amount = tx.CurrencyAmount,
//                            Type = tx.Type == BudgetTransactionType.CashIn ? "CashIn" : "Spend",
//                            TimeStamp = tx.Timestamp,
//                            Description = "" // unless you add it to your BO
//                        });
//                    }
//                }
//            }

//            await JsonFileStore.WriteListAsync(CardDbModelListFileName, outCards);
//            await JsonFileStore.WriteListAsync(BudgetCardDbModelListFileName, outBudgets);
//            await JsonFileStore.WriteListAsync(BudgetCardScheduledTopUpDbModelListFileName, outTopUps);
//            await JsonFileStore.WriteListAsync(BudgetCardTransactionDbModelListFileName, outTx);
//        }


//        public async Task SaveAchievementCardModelDataAsync(List<AchievementCardModel> models)
//        {
//            models ??= new List<AchievementCardModel>();

//            var existingCards = await JsonFileStore.ReadListAsync<CardDbModel>(CardDbModelListFileName) ?? new();
//            var existingAchievements = await JsonFileStore.ReadListAsync<AchievementDbModel>(AchievementDbModelListFileName) ?? new();
//            var existingTrophies = await JsonFileStore.ReadListAsync<AchievementTrophyDbModel>(AchievementTrophyDbModelListFileName) ?? new();

//            // Independent sequences
//            var nextCardId = NextId(existingCards, c => c.CardID);
//            var nextAchievementId = NextId(existingAchievements, a => a.AchievementID);
//            var nextTrophyId = NextId(existingTrophies, t => t.TrophyID);

//            foreach (var m in models)
//            {
//                if (m.Id <= 0)
//                    m.Id = nextAchievementId++;
//            }

//            int ResolveCardIdForAchievement(int achievementId)
//            {
//                var existing = existingAchievements.FirstOrDefault(x => x.AchievementID == achievementId);
//                if (existing != null && existing.CardID > 0)
//                    return existing.CardID;

//                var newCardId = nextCardId++;
//                existingAchievements.Add(new AchievementDbModel
//                {
//                    AchievementID = achievementId,
//                    CardID = newCardId
//                });
//                return newCardId;
//            }

//            var outCards = new List<CardDbModel>();
//            var outAchievements = new List<AchievementDbModel>();
//            var outTrophies = new List<AchievementTrophyDbModel>();

//            foreach (var m in models)
//            {
//                var achievementId = m.Id;
//                var cardId = ResolveCardIdForAchievement(achievementId);

//                outCards.Add(new CardDbModel
//                {
//                    CardID = cardId,
//                    Title = m.Title ?? "",
//                    Tags = m.Tags ?? ""
//                });

//                outAchievements.Add(new AchievementDbModel
//                {
//                    AchievementID = achievementId,
//                    CardID = cardId,

//                    Status = m.Status ?? "",
//                    Description = m.Description ?? "",

//                    SubType = m.Difficulty.ToString(),
//                    ProgressType = m.TargetType.ToString(),

//                    RangeAmount = m.RangeAmount,
//                    Deadline = m.Deadline,
//                    LastEarnedAt = m.LastEarnedAt
//                });

//                if (m.Trophies != null)
//                {
//                    foreach (var trophyStr in m.Trophies)
//                    {
//                        if (string.IsNullOrWhiteSpace(trophyStr)) continue;

//                        outTrophies.Add(new AchievementTrophyDbModel
//                        {
//                            TrophyID = nextTrophyId++,
//                            AchievementID = achievementId,
//                            Title = trophyStr,
//                            ImageSource = trophyStr,
//                            EarnedOn = DateTime.UtcNow
//                        });
//                    }
//                }
//            }

//            await JsonFileStore.WriteListAsync(CardDbModelListFileName, outCards);
//            await JsonFileStore.WriteListAsync(AchievementDbModelListFileName, outAchievements);
//            await JsonFileStore.WriteListAsync(AchievementTrophyDbModelListFileName, outTrophies);
//        }


//        public async Task SaveValueRateModelDataAsync(List<ValueRateModel> models)
//        {
//            models ??= new List<ValueRateModel>();

//            var existingRates = await JsonFileStore.ReadListAsync<ValueRateDbModel>(ValueRateDbModelListFileName);
//            existingRates ??= new();

//            var nextRateId = NextId(existingRates, r => r.TatCardValueRateID);

//            // ValueRateModel doesn't currently have Id or TatCardID in your codebase snippet,
//            // but you said you added Id to every business model.
//            // So we treat Id as TatCardValueRateID and store TatCardID as 0 (global list)
//            // unless you later extend ValueRateModel with TatCardId.

//            foreach (var m in models)
//            {
//                if (m.Id <= 0)
//                    m.Id = nextRateId++;
//            }

//            var outRates = models
//                .Select(m => new ValueRateDbModel
//                {
//                    TatCardValueRateID = m.Id,
//                    TatCardID = 0,
//                    RateName = m.RateName ?? "",
//                    ValuePerMinute = m.ValuePerMinute
//                })
//                .OrderBy(r => r.RateName)
//                .ToList();

//            await JsonFileStore.WriteListAsync(ValueRateDbModelListFileName, outRates);
//        }

//        public async Task SaveTatModelAsync(TatCardModel model)
//        {
//            await SaveTatModelDataAsync(new List<TatCardModel>() { model });
//        }

//        public async Task SaveScModelAsync(ScCardModel model)
//        {
//            await SaveScModelDataAsync(new List<ScCardModel>() { model });
//        }

//        public async Task SaveMissionCardModelAsync(MissionCardModel model)
//        {
//            await SaveMissionCardModelDataAsync(new List<MissionCardModel>() { model });
//        }

//        public async Task SaveBudgetCardModelAsync(BudgetCardModel model)
//        {
//            await SaveBudgetCardModelDataAsync(new List<BudgetCardModel>() { model });
//        }

//        public async Task SaveAchievementCardModelAsync(AchievementCardModel model)
//        {
//            await SaveAchievementCardModelDataAsync(new List<AchievementCardModel>() { model });
//        }

//        public async Task SaveCardModelAsync(List<ICardModel> models)
//        {
//            if (models.Count <= 0) return;

//            var scmodels = models.OfType<ScCardModel>();
//            var tatModels = models.OfType<TatCardModel>().Except(scmodels);
//            var budgetModels = models.OfType<BudgetCardModel>();
//            var achievementModels = models.OfType<AchievementCardModel>();
//            var missionModels = models.OfType<MissionCardModel>();

//            if (scmodels != null && scmodels.Count() > 0)
//            {
//                await SaveScModelDataAsync(scmodels.Cast<ScCardModel>().ToList());
//            }
//            if (tatModels != null && tatModels.Count() > 0)
//            {
//                await SaveTatModelDataAsync(tatModels.Cast<TatCardModel>().ToList());
//            }
//            if (budgetModels != null && budgetModels.Count() > 0)
//            {
//                await SaveBudgetCardModelDataAsync(budgetModels.Cast<BudgetCardModel>().ToList());
//            }
//            if (achievementModels != null && achievementModels.Count() > 0)
//            {
//                await SaveAchievementCardModelDataAsync(achievementModels.Cast<AchievementCardModel>().ToList());
//            }
//            if (missionModels != null && missionModels.Count() > 0)
//            {
//                await SaveMissionCardModelDataAsync(missionModels.Cast<MissionCardModel>().ToList());
//            }
//        }

//        public async Task SaveCardModelAsync(ICardModel model)
//        {
//            await SaveCardModelAsync(new List<ICardModel>() { model });
//        }
//    }
//}
