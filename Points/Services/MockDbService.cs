using Points.Models;
using Points.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Services
{
    public class MockDbService : IDbService
    {
        public string BackupsFolderPath => throw new NotImplementedException();

        public Task BackupAsync()
        {
            return Task.CompletedTask;
        }

        public Task<HomeSeedData> GetHomeSeedDataAsync()
        {
            var testValueRates = new List<ValueRateModel>
            {
                new ValueRateModel { RateName = "Higher Rate", ValuePerMinute = 5 }
            };

            var mainQuest = new IActiveCardModel[]
            {
                new TatCardModel { Title = "TAT 1", ValuePerMinute = 1.25, ValueRates = testValueRates },
                new ScCardModel  { Title = "SC 1",  ValuePerMinute = 1.00 },
                new TatCardModel { Title = "TAT 2", ValuePerMinute = 0.75 },
                new TatCardModel { Title = "TAT 3", ValuePerMinute = -1.00 },
            };

            var today = DateTime.Today;
            var now = DateTime.Now;
            DateTime AtToday(int hour, int minute = 0) => today.AddHours(hour).AddMinutes(minute);

            var mission = new IActiveCardModel[]
            {
                new MissionCardModel
                {
                    Title="Stable - Available & Incomplete", Tags="#Stable #Available",
                    SubType=MissionSubType.Stable, Value=25,
                    CreatedDate=now.AddDays(-2), AvailableFromDate=today.AddDays(-1), DueDate=today.AddDays(+2),
                },
                new MissionCardModel
                {
                    Title="Degrade - Available & Incomplete", Tags="#Degrade #Available",
                    SubType=MissionSubType.Degrade, Value=30,
                    CreatedDate=now.AddDays(-1), AvailableFromDate=AtToday(8,0), DueDate=AtToday(18,0),
                },
                new MissionCardModel
                {
                    Title="Rot - Available, Overdue & Incomplete", Tags="#Rot #Overdue",
                    SubType=MissionSubType.Rot, Value=40,
                    CreatedDate=now.AddDays(-3), AvailableFromDate=today.AddDays(-2), DueDate=AtToday(10,0),
                },

                new MissionCardModel
                {
                    Title="Stable - Not Available Yet", Tags="#Stable #Locked",
                    SubType=MissionSubType.Stable, Value=15,
                    CreatedDate=now, AvailableFromDate=now.AddHours(+2), DueDate=today.AddDays(+1),
                },
                new MissionCardModel
                {
                    Title="Degrade - Not Available Yet", Tags="#Degrade #Locked",
                    SubType=MissionSubType.Degrade, Value=20,
                    CreatedDate=now, AvailableFromDate=today.AddDays(+1), DueDate=today.AddDays(+2),
                },
                new MissionCardModel
                {
                    Title="Rot - Not Available Yet", Tags="#Rot #Locked",
                    SubType=MissionSubType.Rot, Value=10,
                    CreatedDate=now, AvailableFromDate=today.AddDays(+1), DueDate=today.AddDays(+1).AddHours(6),
                },

                new MissionCardModel
                {
                    Title="Stable - Completed Today", Tags="#Stable #Done",
                    SubType=MissionSubType.Stable, Value=25,
                    CreatedDate=now.AddDays(-5), AvailableFromDate=today.AddDays(-2), DueDate=today.AddDays(+5),
                },
                new MissionCardModel
                {
                    Title="Degrade - Completed Today", Tags="#Degrade #Done",
                    SubType=MissionSubType.Degrade, Value=30,
                    CreatedDate=now.AddDays(-2), AvailableFromDate=AtToday(7,0), DueDate=AtToday(19,0),
                },
                new MissionCardModel
                {
                    Title="Rot - Completed Today (Freezes Damage)", Tags="#Rot #Done",
                    SubType=MissionSubType.Rot, Value=40,
                    CreatedDate=now.AddDays(-2), AvailableFromDate=today.AddDays(-1), DueDate=AtToday(9,0),
                },
            };

            // complete some
            ((MissionCardModel)mission[6]).Complete(AtToday(9, 15));
            ((MissionCardModel)mission[7]).Complete(AtToday(11, 30));
            ((MissionCardModel)mission[8]).Complete(AtToday(14, 10));

            var budget = new ICardModel[]
            {
                new BudgetCardModel
                {
                    Title="Calorie Budget",
                    Currency="Kcal",
                    ExchangeRate=0.01,
                    StartDate=DateTime.Today,
                    InitialBalance=0,
                    Status="In-Progress",
                    Tags="PRO TAT Other",
                    TopUps =
                    {
                        new ScheduledTopUp { TimeOfDay = new TimeSpan(7,0,0), Amount = 500 },
                        new ScheduledTopUp { TimeOfDay = new TimeSpan(12,0,0), Amount = 500 },
                        new ScheduledTopUp { TimeOfDay = new TimeSpan(18,0,0), Amount = 500 },
                    }
                }
            };

            var seed = new HomeSeedData
            {
                MainQuestCards = mainQuest,
                MissionCards = mission,
                BudgetCards = budget
            };

            return Task.FromResult(seed);
        }

        public DateTime? GetLastBackupUtc()
        {
            return DateTime.Now;
        }

        public Task RestoreAsync(string backupFilePath)
        {
            return Task.CompletedTask;
        }

        public Task WipeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
