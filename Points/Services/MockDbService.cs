//using Points.Global;
//using Points.Models;
//using Points.ViewModels;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Points.Services
//{
//    public class MockDbService : IDbService
//    {
//        public string BackupsFolderPath => throw new NotImplementedException();

//        public Task BackupAsync()
//        {
//            return Task.CompletedTask;
//        }

//        public Task<List<ValueRateModel>> GetValueRateModelDataAsync()
//        {
//            var testValueRates = MockDB.ValueRateModels;

//            return Task.FromResult(testValueRates);
//        }

//        public Task<List<IActiveCardModel>> GetMainQuestModelDataAsync()
//        {
//            var tats = GetTatModelDataAsync().Result;
//            var scs = GetScModelDataAsync().Result;

//            var mainQuest = new List<IActiveCardModel>();
//            mainQuest.AddRange(tats);
//            mainQuest.AddRange(scs);

//            return Task.FromResult(mainQuest);
//        }

//        public Task<List<TatCardModel>> GetTatModelDataAsync()
//        {
//            var testValueRates = GetValueRateModelDataAsync().Result;

//            var tats = MockDB.TatCardModels;

//            tats[0].ValueRates = testValueRates;

//            return Task.FromResult(tats);
//        }

//        public Task<List<ScCardModel>> GetScModelDataAsync()
//        {
//            var scs = MockDB.ScCardModels;

//            return Task.FromResult(scs);
//        }

//        public Task<List<MissionCardModel>> GetMissionCardModelDataAsync()
//        {
//            var today = DateTime.Today;
//            var now = DateTime.Now;
            

//            var mission = MockDB.MissionCardModels;

//            // complete some
//            ((MissionCardModel)mission[6]).Complete(MockDB.AtToday(9, 15));
//            ((MissionCardModel)mission[7]).Complete(MockDB.AtToday(11, 30));
//            ((MissionCardModel)mission[8]).Complete(MockDB.AtToday(14, 10));

//            return Task.FromResult(mission);
//        }

//        public Task<List<BudgetCardModel>> GetBudgetCardModelDataAsync()
//        {
//            var budget = MockDB.BudgetCardModels;

//            return Task.FromResult(budget);
//        }

//        public Task<List<AchievementCardModel>> GetAchievementCardModelDataAsync()
//        {
//            var achievements = MockDB.AchievementCardModels;

//            return Task.FromResult(achievements);
//        }

//        public Task<HomeSeedData> GetHomeSeedDataAsync()
//        {
//            var mainQuest = GetMainQuestModelDataAsync().Result;

//            var mission = GetMissionCardModelDataAsync().Result;

//            var budget = GetBudgetCardModelDataAsync().Result;

//            var achievements = GetAchievementCardModelDataAsync().Result;

//            var seed = new HomeSeedData
//            {
//                MainQuestCards = mainQuest,
//                MissionCards = mission,
//                BudgetCards = budget,
//                Achievements = achievements
//            };

//            return Task.FromResult(seed);
//        }

//        public DateTime? GetLastBackupUtc()
//        {
//            return DateTime.Now;
//        }

//        public Task RestoreAsync(string backupFilePath)
//        {
//            return Task.CompletedTask;
//        }

//        public Task WipeAsync()
//        {
//            return Task.CompletedTask;
//        }

//        public Task SaveValueRateModelDataAsync(List<ValueRateModel> models)
//        {
//            throw new NotImplementedException();
//        }

//        public Task SaveTatModelDataAsync(List<TatCardModel> models)
//        {
//            throw new NotImplementedException();
//        }

//        public Task SaveScModelDataAsync(List<ScCardModel> models)
//        {
//            throw new NotImplementedException();
//        }

//        public Task SaveMissionCardModelDataAsync(List<MissionCardModel> models)
//        {
//            throw new NotImplementedException();
//        }

//        public Task SaveBudgetCardModelDataAsync(List<BudgetCardModel> models)
//        {
//            throw new NotImplementedException();
//        }

//        public Task SaveAchievementCardModelDataAsync(List<AchievementCardModel> models)
//        {
//            throw new NotImplementedException();
//        }

//        public Task SaveTatModelAsync(TatCardModel model)
//        {
//            throw new NotImplementedException();
//        }

//        public Task SaveScModelAsync(ScCardModel model)
//        {
//            throw new NotImplementedException();
//        }

//        public Task SaveMissionCardModelAsync(MissionCardModel model)
//        {
//            throw new NotImplementedException();
//        }

//        public Task SaveBudgetCardModelAsync(BudgetCardModel model)
//        {
//            throw new NotImplementedException();
//        }

//        public Task SaveAchievementCardModelAsync(AchievementCardModel model)
//        {
//            throw new NotImplementedException();
//        }

//        public Task SaveCardModelAsync(List<ICardModel> models)
//        {
//            throw new NotImplementedException();
//        }

//        public Task SaveCardModelAsync(ICardModel model)
//        {
//            throw new NotImplementedException();
//        }
//    }

//    public static class MockDB
//    {
//        public static List<ValueRateModel> ValueRateModels = new List<ValueRateModel>
//        {
//            new ValueRateModel { RateName = "Higher Rate", ValuePerMinute = 5 }
//        };

//        public static List<TatCardModel> TatCardModels = new List<TatCardModel>
//        {
//            new TatCardModel { Title = "TAT 1", ValuePerMinute = 1.25 },
//            new TatCardModel { Title = "TAT 2", ValuePerMinute = 0.75 },
//            new TatCardModel { Title = "TAT 3", ValuePerMinute = -1.00 },
//        };

//        public static List<ScCardModel> ScCardModels = new List<ScCardModel>
//        {
//            new ScCardModel  { Title = "SC 1",  ValuePerMinute = 1.00 }
//        };

//        public static List<MissionCardModel> MissionCardModels = new List<MissionCardModel>
//        {
//            new MissionCardModel
//            {
//                Title="Stable - Available & Incomplete", Tags="#Stable #Available",
//                SubType=MissionSubType.Stable, Value=25, ValuePerMinute=0.1, EstCompletionTime=TimeSpan.FromHours(1),
//                CreatedDate=DateTime.Now.AddDays(-2), AvailableFromDate=DateTime.Today.AddDays(-1), DueDate=DateTime.Today.AddDays(+2),
//            },
//            new MissionCardModel
//            {
//                Title="Degrade - Available & Incomplete", Tags="#Degrade #Available",
//                SubType=MissionSubType.Degrade, Value=30, ValuePerMinute=0.1, EstCompletionTime=TimeSpan.FromHours(1),
//                CreatedDate=DateTime.Now.AddDays(-1), AvailableFromDate=AtToday(8,0), DueDate=AtToday(18,0),
//            },
//            new MissionCardModel
//            {
//                Title="Rot - Available, Overdue & Incomplete", Tags="#Rot #Overdue",
//                SubType=MissionSubType.Rot, Value=40, ValuePerMinute=0.1, EstCompletionTime=TimeSpan.FromHours(1),
//                CreatedDate=DateTime.Now.AddDays(-3), AvailableFromDate=DateTime.Today.AddDays(-2), DueDate=AtToday(10,0),
//            },

//            new MissionCardModel
//            {
//                Title="Stable - Not Available Yet", Tags="#Stable #Locked",
//                SubType=MissionSubType.Stable, Value=15, ValuePerMinute=0.1, EstCompletionTime=TimeSpan.FromHours(1),
//                CreatedDate=DateTime.Now, AvailableFromDate=DateTime.Now.AddHours(+2), DueDate=DateTime.Today.AddDays(+1),
//            },
//            new MissionCardModel
//            {
//                Title="Degrade - Not Available Yet", Tags="#Degrade #Locked",
//                SubType=MissionSubType.Degrade, Value=20, ValuePerMinute=0.1, EstCompletionTime=TimeSpan.FromHours(1),
//                CreatedDate=DateTime.Now, AvailableFromDate=DateTime.Today.AddDays(+1), DueDate=DateTime.Today.AddDays(+2),
//            },
//            new MissionCardModel
//            {
//                Title="Rot - Not Available Yet", Tags="#Rot #Locked",
//                SubType=MissionSubType.Rot, Value=10, ValuePerMinute=0.1, EstCompletionTime=TimeSpan.FromHours(1),
//                CreatedDate=DateTime.Now, AvailableFromDate=DateTime.Today.AddDays(+1), DueDate=DateTime.Today.AddDays(+1).AddHours(6),
//            },

//            new MissionCardModel
//            {
//                Title="Stable - Completed Today", Tags="#Stable #Done",
//                SubType=MissionSubType.Stable, Value=25, ValuePerMinute=0.1, EstCompletionTime=TimeSpan.FromHours(1),
//                CreatedDate=DateTime.Now.AddDays(-5), AvailableFromDate=DateTime.Today.AddDays(-2), DueDate=DateTime.Today.AddDays(+5),
//            },
//            new MissionCardModel
//            {
//                Title="Degrade - Completed Today", Tags="#Degrade #Done",
//                SubType=MissionSubType.Degrade, Value=30, ValuePerMinute=0.1, EstCompletionTime=TimeSpan.FromHours(1),
//                CreatedDate=DateTime.Now.AddDays(-2), AvailableFromDate=AtToday(7,0), DueDate=AtToday(19,0),
//            },
//            new MissionCardModel
//            {
//                Title="Rot - Completed Today (Freezes Damage)", Tags="#Rot #Done",
//                SubType=MissionSubType.Rot, Value=40, ValuePerMinute=0.1, EstCompletionTime=TimeSpan.FromHours(1),
//                CreatedDate=DateTime.Now.AddDays(-2), AvailableFromDate=DateTime.Today.AddDays(-1), DueDate=AtToday(9,0),
//            },
//        };

//        public static List<BudgetCardModel> BudgetCardModels = new List<BudgetCardModel>
//        {
//            new BudgetCardModel
//            {
//                Title="Calorie Budget",
//                Currency="Kcal",
//                ExchangeRate=0.01,
//                StartDate=DateTime.Today,
//                InitialBalance=0,
//                Status="In-Progress",
//                Tags="PRO TAT Other",
//                TopUps =
//                {
//                    new ScheduledTopUp { TimeOfDay = new TimeSpan(7,0,0), Amount = 500 },
//                    new ScheduledTopUp { TimeOfDay = new TimeSpan(12,0,0), Amount = 500 },
//                    new ScheduledTopUp { TimeOfDay = new TimeSpan(18,0,0), Amount = 500 },
//                }
//            }
//        };

//        public static List<AchievementCardModel> AchievementCardModels = new List<AchievementCardModel>
//        {   
//            new AchievementCardModel
//            {
//                Title = "Super Nerd",
//                Status = "In-Progress",
//                Tags = "#Study, #Consistency",
//                TargetType = AchievementTargetType.ActiveTime,
//                Target = 600, // minutes
//                CurrentValue = 245
//            },
//            new AchievementCardModel
//            {
//                Title = "Gym Rat",
//                Status = "Completed",
//                Tags = "#Fitness",
//                TargetType = AchievementTargetType.Value,
//                Target = 1000,
//                CurrentValue = 1000,
//                CompletedAt = DateTime.Now.AddDays(-2),
//                CompletionType = AchievementCompletionType.Range,
//                RangeAmount = 6,
//                RangeUnit  = AchievementRangeUnit.Months,
//                LastEarnedAt = DateTime.Now.AddDays(-2),
//                ActiveTimeTargetText = "200:00:00"
//            }
//        };

//        public static DateTime AtToday(int hour, int minute = 0) => DateTime.Today.AddHours(hour).AddMinutes(minute);
//    }
//}
