
using Points.Services.Sqlite.Providers.Classes;
using Points.Services.Sqlite.Providers.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Services.Sqlite.Providers
{
    public sealed class PointsSchemaProvider : IPointsSchemaProvider
    {
        public DatabaseSchemaDefinition GetSchema()
        {
            return new DatabaseSchemaDefinition
            {
                Tables = new List<TableDefinition>
                {
                    BuildCardTable(),
                    BuildTatCardTable(),
                    BuildTatCardValueRateTable(),
                    BuildScCardTable(),
                    BuildScCardStepTable(),
                    BuildScCardStepRepTable(),
                    BuildMissionCardTable(),
                    BuildBudgetCardTable(),
                    BuildBudgetCardScheduledTopUpTable(),
                    BuildBudgetCardTransactionTable(),
                    BuildAchievementCardTable(),
                    BuildAchievementTrophyTable(),
                    BuildActivityTable(),
                    BuildValueTrackerCardTable(),
                    BuildEventTrackerCardTable(),
                    BuildTrackerValueTable(),
                    BuildStatusConditionTable(),
                    BuildThemeTable(),
                    BuildThemeConfigurationTable(),
                    BuildAppSettingsTable(),
                    BuildCardScheduleTable(),
                    BuildPlannerGoalTable(),
                    BuildLockTable(),
                    BuildLockScheduleTable(),
                    BuildLockTaskDependencyTable(),
                    BuildReportTable(),
                    BuildShortcutGroupTable(),
                    BuildShortcutTable()
                },
                Indexes = new List<IndexDefinition>
                {
                    new()
                    {
                        Name = "UX_ShortcutGroup_Name",
                        TableName = "ShortcutGroup",
                        IsUnique = true,
                        Columns = new List<string> { "Name" }
                    },

                    new()
                    {
                        Name = "IX_TatCard_CardID",
                        TableName = "TatCard",
                        Columns = new List<string> { "CardID" }
                    },
                    new()
                    {
                        Name = "IX_TatCardValueRate_TatCardID",
                        TableName = "TatCardValueRate",
                        Columns = new List<string> { "TatCardID" }
                    },

                    new()
                    {
                        Name = "IX_ScCard_CardID",
                        TableName = "ScCard",
                        Columns = new List<string> { "CardID" }
                    },
                    new()
                    {
                        Name = "IX_ScCardStep_ScCardID",
                        TableName = "ScCardStep",
                        Columns = new List<string> { "ScCardID" }
                    },
                    new()
                    {
                        Name = "IX_ScCardStepRep_TimeStamp",
                        TableName = "ScCardStepRep",
                        Columns = new List<string> { "TimeStamp" }
                    },

                    new()
                    {
                        Name = "IX_MissionCard_CardID",
                        TableName = "MissionCard",
                        Columns = new List<string> { "CardID" }
                    },
                    new()
                    {
                        Name = "IX_MissionCard_Status",
                        TableName = "MissionCard",
                        Columns = new List<string> { "Status" }
                    },
                    new()
                    {
                        Name = "IX_MissionCard_DueDate",
                        TableName = "MissionCard",
                        Columns = new List<string> { "DueDate" }
                    },

                    new()
                    {
                        Name = "IX_BudgetCard_CardID",
                        TableName = "BudgetCard",
                        Columns = new List<string> { "CardID" }
                    },
                    new()
                    {
                        Name = "IX_BudgetTxn_BudgetCardID",
                        TableName = "BudgetCardTransaction",
                        Columns = new List<string> { "BudgetCardID" }
                    },
                    new()
                    {
                        Name = "IX_BudgetTxn_TimeStamp",
                        TableName = "BudgetCardTransaction",
                        Columns = new List<string> { "TimeStamp" }
                    },

                    new()
                    {
                        Name = "IX_Achievement_CardID",
                        TableName = "AchievementCard",
                        Columns = new List<string> { "CardID" }
                    },
                    new()
                    {
                        Name = "IX_Achievement_ScCardStepID",
                        TableName = "AchievementCard",
                        Columns = new List<string> { "ScCardStepID" }
                    },
                    new()
                    {
                        Name = "IX_Trophy_AchievementID",
                        TableName = "AchievementTrophy",
                        Columns = new List<string> { "AchievementCardID" }
                    },

                    new()
                    {
                        Name = "IX_Activity_CardID",
                        TableName = "Activity",
                        Columns = new List<string> { "CardID" }
                    },
                    new()
                    {
                        Name = "UX_Activity_OneOpen",
                        TableName = "Activity",
                        IsUnique = true,
                        Columns = new List<string> { "1" },
                        WhereSql = "\"End\" IS NULL"
                    },
                    new()
                    {
                        Name = "IX_Activity_StartEnd",
                        TableName = "Activity",
                        Columns = new List<string> { "Start", "End" }
                    },

                    new()
                    {
                        Name = "IX_ValueTracker_CardID",
                        TableName = "ValueTrackerCard",
                        Columns = new List<string> { "CardID" }
                    },
                    new()
                    {
                        Name = "IX_EventTracker_CardID",
                        TableName = "EventTrackerCard",
                        Columns = new List<string> { "CardID" }
                    },
                    new()
                    {
                        Name = "IX_TrackerValue_CardID",
                        TableName = "TrackerValue",
                        Columns = new List<string> { "CardID" }
                    },
                    new()
                    {
                        Name = "IX_TrackerValue_TimeStamp",
                        TableName = "TrackerValue",
                        Columns = new List<string> { "TimeStamp" }
                    },

                    new()
                    {
                        Name = "IX_ThemeConfiguration_ThemeID",
                        TableName = "ThemeConfiguration",
                        Columns = new List<string> { "ThemeID" }
                    },
                    new()
                    {
                        Name = "IX_AppSettings_SelectedThemeID",
                        TableName = "AppSettings",
                        Columns = new List<string> { "SelectedThemeID" }
                    },
                    new()
                    {
                        Name = "IX_AppSettings_StatusConditionID",
                        TableName = "AppSettings",
                        Columns = new List<string> { "CurrentlyAppliedStatusConditionID" }
                    },

                    new()
                    {
                        Name = "IX_CardSchedule_CardId",
                        TableName = "CardSchedule",
                        Columns = new List<string> { "CardId" }
                    },

                    new()
                    {
                        Name = "IX_PlannerGoal_CardID",
                        TableName = "PlannerGoal",
                        Columns = new List<string> { "CardID" }
                    },
                    new()
                    {
                        Name = "IX_PlannerGoal_Enabled",
                        TableName = "PlannerGoal",
                        Columns = new List<string> { "Enabled" }
                    },

                    new()
                    {
                        Name = "IX_Lock_CardId",
                        TableName = "Lock",
                        Columns = new List<string> { "CardId" }
                    },
                    new()
                    {
                        Name = "IX_Lock_CardId_LockNumber",
                        TableName = "Lock",
                        Columns = new List<string> { "CardId", "LockNumber" }
                    },

                    new()
                    {
                        Name = "IX_LockSchedule_LockId",
                        TableName = "LockSchedule",
                        Columns = new List<string> { "LockId" }
                    },
                    new()
                    {
                        Name = "IX_LockSchedule_LockId_Frequency",
                        TableName = "LockSchedule",
                        Columns = new List<string> { "LockId", "FrequencyType" }
                    },
                    new()
                    {
                        Name = "IX_LockSchedule_DateRange",
                        TableName = "LockSchedule",
                        Columns = new List<string> { "FromDateTime", "ToDateTime" }
                    },

                    new()
                    {
                        Name = "IX_LockTaskDependency_LockId",
                        TableName = "LockTaskDependency",
                        Columns = new List<string> { "LockId" }
                    },
                    new()
                    {
                        Name = "IX_LockTaskDependency_TaskCard",
                        TableName = "LockTaskDependency",
                        Columns = new List<string> { "TaskDependencyCardId" }
                    },
                    new()
                    {
                        Name = "IX_LockTaskDependency_TaskCard_TimeScope",
                        TableName = "LockTaskDependency",
                        Columns = new List<string> { "TaskDependencyCardId", "TimeScope" }
                    },

                    new()
                    {
                        Name = "IX_ShortcutGroup_Order",
                        TableName = "ShortcutGroup",
                        Columns = new List<string> { "ShortcutGroupOrder", "ShortcutGroupId" }
                    },
                    new()
                    {
                        Name = "IX_Shortcut_Group_Order",
                        TableName = "Shortcut",
                        Columns = new List<string> { "ShortcutGroupId", "ShortcutOrder", "ShortcutId" }
                    },
                    new()
                    {
                        Name = "IX_Shortcut_TargetCardId",
                        TableName = "Shortcut",
                        Columns = new List<string> { "TargetCardId" }
                    },

                    new()
                    {
                        Name = "UX_Report_Title",
                        TableName = "Report",
                        IsUnique = true,
                        Columns = new List<string> { "Title" }
                    }
                }
            };
        }

        private static TableDefinition BuildCardTable() => new()
        {
            Name = "Card",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("CardID"),
                Text("Title", false, "''"),
                Text("Tags", false, "''")
            }
        };

        private static TableDefinition BuildTatCardTable() => new()
        {
            Name = "TatCard",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("TatCardID"),
                Int("CardID", false),
                Real("ValuePerMinute", false),
                Text("Status", false, "''"),
                Text("Description", false, "''"),
                Int("TargetActiveTimeSeconds", true)
            },
            TableConstraints = new List<string>
            {
                @"FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE"
            }
        };

        private static TableDefinition BuildTatCardValueRateTable() => new()
        {
            Name = "TatCardValueRate",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("TatCardValueRateID"),
                Int("TatCardID", false),
                Text("RateName", false, "''"),
                Real("ValuePerMinute", false)
            },
            TableConstraints = new List<string>
            {
                @"FOREIGN KEY (TatCardID) REFERENCES TatCard(TatCardID) ON DELETE CASCADE"
            }
        };

        private static TableDefinition BuildScCardTable() => new()
        {
            Name = "ScCard",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("ScCardID"),
                Int("CardID", false),
                Text("Status", false, "''"),
                Text("Description", false, "''")
            },
            TableConstraints = new List<string>
            {
                @"FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE"
            }
        };

        private static TableDefinition BuildScCardStepTable() => new()
        {
            Name = "ScCardStep",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("ScCardStepID"),
                Int("ScCardID", false),
                Int("SortOrder", false),
                Text("Title", false, "''"),
                Real("StepValue", false)
            },
            TableConstraints = new List<string>
            {
                @"FOREIGN KEY (ScCardID) REFERENCES ScCard(ScCardID) ON DELETE CASCADE"
            }
        };

        private static TableDefinition BuildScCardStepRepTable() => new()
        {
            Name = "ScCardStepRep",
            Columns = new List<Classes.ColumnDefinition>
            {
                Int("ScCardStepID", false),
                Text("TimeStamp", false),
                Real("StepValue", false)
            },
            TableConstraints = new List<string>
            {
                @"PRIMARY KEY (ScCardStepID, TimeStamp)",
                @"FOREIGN KEY (ScCardStepID) REFERENCES ScCardStep(ScCardStepID) ON DELETE CASCADE"
            }
        };

        private static TableDefinition BuildMissionCardTable() => new()
        {
            Name = "MissionCard",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("MissionCardID"),
                Int("CardID", false),
                Text("Status", false, "''"),
                Text("Description", false, "''"),
                Text("SubType", false, "''"),
                Real("Value", false),
                Text("CreatedDate", false),
                Text("AvailableFromDate", false),
                Text("DueDate", false),
                Text("CompletedDate", true),
                Text("EventDate", true),
                Text("EstCompletionTimeText", false, "''"),
                Int("IsFailed", false, "0"),
                Real("ValuePerMinute", false)
            },
            TableConstraints = new List<string>
            {
                @"FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE"
            }
        };

        private static TableDefinition BuildBudgetCardTable() => new()
        {
            Name = "BudgetCard",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("BudgetCardID"),
                Int("CardID", false),
                Text("Status", false, "''"),
                Text("Description", false, "''"),
                Text("Currency", false, "''"),
                Real("ExchangeRate", false),
                Text("StartDate", false),
                Real("InitialBalance", false)
            },
            TableConstraints = new List<string>
            {
                @"FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE"
            }
        };

        private static TableDefinition BuildBudgetCardScheduledTopUpTable() => new()
        {
            Name = "BudgetCardScheduledTopUp",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("BudgetCardScheduledTopUpID"),
                Int("BudgetCardID", false),
                Real("Amount", false),
                Int("TimeOfDaySeconds", false)
            },
            TableConstraints = new List<string>
            {
                @"FOREIGN KEY (BudgetCardID) REFERENCES BudgetCard(BudgetCardID) ON DELETE CASCADE"
            }
        };

        private static TableDefinition BuildBudgetCardTransactionTable() => new()
        {
            Name = "BudgetCardTransaction",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("BudgetCardTransactionID"),
                Int("BudgetCardID", false),
                Real("Amount", false),
                Text("Type", false, "''"),
                Text("TimeStamp", false)
            },
            TableConstraints = new List<string>
            {
                @"FOREIGN KEY (BudgetCardID) REFERENCES BudgetCard(BudgetCardID) ON DELETE CASCADE"
            }
        };

        private static TableDefinition BuildAchievementCardTable() => new()
        {
            Name = "AchievementCard",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("AchievementCardID"),
                Int("CardID", false),
                Text("Status", false, "''"),
                Text("Description", false, "''"),
                Text("GoalType", false, "''"),
                Text("DifficultyLevel", false, "'Easy'"),
                Text("CreatedDate", false),
                Text("LastEarnedAt", true),
                Int("TargetActiveTimeInSeconds", true),
                Int("TargetValue", true),
                Int("ScCardStepID", true),
                Text("CompletionType", false, "'Range'"),
                Text("RangeUnit", true),
                Int("RangeAmount", true),
                Text("DeadlineStart", true),
                Text("Deadline", true),
                Text("FinalizedAt", true),
                Real("FrozenCurrentValue", true),
                Text("TrophyURLs", false, "''"),
                Int("IsPinned", false, "0")
            },
            TableConstraints = new List<string>
            {
                @"FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE",
                @"FOREIGN KEY (ScCardStepID) REFERENCES ScCardStep(ScCardStepID) ON DELETE SET NULL"
            }
        };

        private static TableDefinition BuildAchievementTrophyTable() => new()
        {
            Name = "AchievementTrophy",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("TrophyID"),
                Int("AchievementCardID", false),
                Text("Title", false, "''"),
                Text("EarnedOn", false),
                Text("ImageSource", false, "''")
            },
            TableConstraints = new List<string>
            {
                @"FOREIGN KEY (AchievementCardID) REFERENCES AchievementCard(AchievementCardID) ON DELETE CASCADE"
            }
        };

        private static TableDefinition BuildActivityTable() => new()
        {
            Name = "Activity",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("ActivityID"),
                Int("CardID", false),
                Text("Start", false),
                Text("End", true),
                Text("ValueRateName", false),
                Real("ValuePerMinute", false)
            },
            TableConstraints = new List<string>
            {
                @"FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE",
                @"CHECK (""End"" IS NULL OR Start < ""End"")"
            }
        };

        private static TableDefinition BuildValueTrackerCardTable() => new()
        {
            Name = "ValueTrackerCard",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("ValueTrackerCardID"),
                Int("CardID", false),
                Text("Unit", false, "''"),
                Text("CreatedDate", false),
                Text("RangeStart", false),
                Int("ScheduleEvery", false, "1"),
                Text("ScheduleUnit", false, "'Week'")
            },
            TableConstraints = new List<string>
            {
                @"FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE"
            }
        };

        private static TableDefinition BuildEventTrackerCardTable() => new()
        {
            Name = "EventTrackerCard",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("EventTrackerCardID"),
                Int("CardID", false),
                Text("Unit", false, "''"),
                Text("CreatedDate", false),
                Text("RangeStart", false),
                Text("GroupByPeriod", false, "'Day'")
            },
            TableConstraints = new List<string>
            {
                @"FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE"
            }
        };

        private static TableDefinition BuildTrackerValueTable() => new()
        {
            Name = "TrackerValue",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("TrackerValueID"),
                Int("CardID", false),
                Text("TimeStamp", false),
                Real("Value", false)
            },
            TableConstraints = new List<string>
            {
                @"FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE"
            }
        };

        private static TableDefinition BuildStatusConditionTable() => new()
        {
            Name = "StatusCondition",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("StatusConditionID"),
                Text("StatusConditionName", false, "''"),
                Real("StatusConditionMultiplierValue", false, "1.0")
            }
        };

        private static TableDefinition BuildThemeTable() => new()
        {
            Name = "Theme",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("ThemeID"),
                Text("ThemeName", false, "''")
            }
        };

        private static TableDefinition BuildThemeConfigurationTable() => new()
        {
            Name = "ThemeConfiguration",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("ThemesConfigID"),
                Int("ThemeID", false),

                Text("MainPageBackColor", false, "''"),
                Text("MainPageForeColor", false, "''"),
                Text("GlobalFontFamily", false, "''"),

                Text("ScCardBackColor", false, "''"),
                Text("ScCardForeColor", false, "''"),
                Text("TatCardBackColor", false, "''"),
                Text("TatCardForeColor", false, "''"),
                Text("MissionCardBackColor", false, "''"),
                Text("MissionCardForeColor", false, "''"),
                Text("BudgetCardBackColor", false, "''"),
                Text("BudgetCardForeColor", false, "''"),

                Text("DueInLabelForeColor", false, "''"),

                Text("NegativeButtonBackColor", false, "''"),
                Text("NegativeButtonForeColor", false, "''"),
                Text("NegativeButtonStyle", false, "'Round'"),
                Real("NegativeButtonBorderThickness", false, "0"),
                Text("NegativeButtonBorderColor", false, "''"),

                Text("PositiveButtonBackColor", false, "''"),
                Text("PositiveButtonForeColor", false, "''"),
                Text("PositiveButtonStyle", false, "'Round'"),
                Real("PositiveButtonBorderThickness", false, "0"),
                Text("PositiveButtonBorderColor", false, "''"),

                Text("ActiveToggleOnBackColor", false, "''"),
                Text("ActiveToggleOnForeColor", false, "''"),
                Text("ActiveToggleOnButtonStyle", false, "'Round'"),
                Real("ActiveToggleOnButtonBorderThickness", false, "0"),
                Text("ActiveToggleOnButtonBorderColor", false, "''"),

                Text("ActiveToggleOffBackColor", false, "''"),
                Text("ActiveToggleOffForeColor", false, "''"),
                Text("ActiveToggleOffButtonStyle", false, "'Round'"),
                Real("ActiveToggleOffButtonBorderThickness", false, "0"),
                Text("ActiveToggleOffButtonBorderColor", false, "''"),

                Text("GlobalValueBelowZeroThresholdForeColor", false, "''"),
                Text("GlobalValueNonZeroBelowThresholdForeColor", false, "''"),
                Text("GlobalValueAboveThresholdForeColor", false, "''"),
                Text("GlobalValueAboveSecondaryThresholdForeColor", false, "''"),

                Text("CardBorderStyle", false, "'RoundedEdges'"),
                Real("CardBorderThickness", false, "0"),
                Text("CardBorderColor", false, "''"),

                Text("MainQuestSectionDisplayName", false, "''"),
                Text("MissionSectionDisplayName", false, "''"),
                Text("BudgetSectionDisplayName", false, "''"),
                Text("ArcsSectionDisplayName", false, "''"),
                Text("PinnedAchievementsSectionDisplayName", false, "''")
            },
            TableConstraints = new List<string>
            {
                @"FOREIGN KEY (ThemeID) REFERENCES Theme(ThemeID) ON DELETE CASCADE"
            }
        };

        private static TableDefinition BuildAppSettingsTable() => new()
        {
            Name = "AppSettings",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("SettingsID"),
                Int("HardModeEnabled", false, "0"),
                Real("HardModeDamagePerMinuteValue", false, "0"),
                Int("StatusConditionsEnabled", false, "0"),
                Int("CurrentlyAppliedStatusConditionID", true),
                Int("SelectedThemeID", true)
            },
            TableConstraints = new List<string>
            {
                @"CHECK (SettingsID = 1)",
                @"FOREIGN KEY (CurrentlyAppliedStatusConditionID) REFERENCES StatusCondition(StatusConditionID) ON DELETE SET NULL",
                @"FOREIGN KEY (SelectedThemeID) REFERENCES Theme(ThemeID) ON DELETE SET NULL"
            }
        };

        private static TableDefinition BuildCardScheduleTable() => new()
        {
            Name = "CardSchedule",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("ScheduleId"),
                Int("CardId", false),
                Int("IsEnabled", false, "1"),
                Text("Note", false, "''"),
                Int("FrequencyType", false),
                Int("FrequencyValue", false, "0"),
                Text("FromDateTime", false),
                Text("ToDateTime", true)
            }
        };

        private static TableDefinition BuildPlannerGoalTable() => new()
        {
            Name = "PlannerGoal",
            Columns = new List<Classes.ColumnDefinition>
            {
                Pk("PlannerGoalID"),
                Int("CardID", false),
                Text("TimeScope", false),
                Real("GoalHrs", false),
                Int("Enabled", false, "0"),
                Text("DeFactoStart", true),
                Text("DeFactoEnd", true)
            },
            TableConstraints = new List<string>
            {
                @"FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE",
                @"UNIQUE (CardID, TimeScope)"
            }
        };

        private static TableDefinition BuildLockTable() => new()
        {
            Name = "Lock",
            Columns = new List<Classes.ColumnDefinition>
            {
                PkAuto("LockId"),
                Int("LockNumber", false),
                Int("CardId", false),
                Text("TimeWindowStart", false),
                Text("TimeWindowEnd", false)
            }
        };

        private static TableDefinition BuildLockScheduleTable() => new()
        {
            Name = "LockSchedule",
            Columns = new List<Classes.ColumnDefinition>
            {
                PkAuto("ScheduleId"),
                Int("LockId", false),
                Int("FrequencyType", false),
                Int("FrequencyValue", false, "0"),
                Text("FromDateTime", false),
                Text("ToDateTime", true)
            }
        };

        private static TableDefinition BuildLockTaskDependencyTable() => new()
        {
            Name = "LockTaskDependency",
            Columns = new List<Classes.ColumnDefinition>
            {
                PkAuto("LockTaskDependencyId"),
                Int("LockId", false),
                Int("TaskDependencyCardId", false),
                Int("MetricType", false, "0"),
                Int("TimeScope", false, "0"),
                Real("GoalValue", false, "0"),
                Int("GoalValence", false, "0")
            }
        };

        private static TableDefinition BuildReportTable() => new()
        {
            Name = "Report",
            Columns = new List<Classes.ColumnDefinition>
            {
                PkAuto("Id"),
                Text("Title", false),
                Text("SQLQuery", false),
                Text("LastRunOn", true),
                Int("EligibleForAchievment", false, "0")
            }
        };

        private static TableDefinition BuildShortcutGroupTable() => new()
        {
            Name = "ShortcutGroup",
            Columns = new List<Classes.ColumnDefinition>
            {
                PkAuto("ShortcutGroupId"),
                Text("Name", false),
                Text("Color", false, "'#FF000000'"),
                Int("ShortcutGroupOrder", false, "0")
            }
        };

        private static TableDefinition BuildShortcutTable() => new()
        {
            Name = "Shortcut",
            Columns = new List<Classes.ColumnDefinition>
            {
                PkAuto("ShortcutId"),
                Text("IconChar", false, "''"),
                Int("TargetCardId", false),
                Int("ShortcutGroupId", false),
                Int("ShortcutOrder", false, "0")
            },
            TableConstraints = new List<string>
            {
                @"FOREIGN KEY (ShortcutGroupId) REFERENCES ShortcutGroup(ShortcutGroupId) ON DELETE CASCADE"
            }
        };

        private static Classes.ColumnDefinition Pk(string name) => new()
        {
            Name = name,
            SqlType = "INTEGER",
            IsPrimaryKey = true,
            IsAutoIncrement = false,
            IsNullable = false
        };

        private static Classes.ColumnDefinition PkAuto(string name) => new()
        {
            Name = name,
            SqlType = "INTEGER",
            IsPrimaryKey = true,
            IsAutoIncrement = true,
            IsNullable = false
        };

        private static Classes.ColumnDefinition Int(string name, bool nullable, string? defaultSql = null) => new()
        {
            Name = name,
            SqlType = "INTEGER",
            IsNullable = nullable,
            DefaultSql = defaultSql
        };

        private static Classes.ColumnDefinition Real(string name, bool nullable, string? defaultSql = null) => new()
        {
            Name = name,
            SqlType = "REAL",
            IsNullable = nullable,
            DefaultSql = defaultSql
        };

        private static Classes.ColumnDefinition Text(string name, bool nullable, string? defaultSql = null) => new()
        {
            Name = name,
            SqlType = "TEXT",
            IsNullable = nullable,
            DefaultSql = defaultSql
        };
    }
}
