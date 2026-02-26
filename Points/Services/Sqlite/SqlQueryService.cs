using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Services.Sqlite
{
    public static class SqlQueryService
    {
        public static string GenerateDbCreationScript()
        {
            return @"
                PRAGMA foreign_keys = ON;

                -- =========================
                -- Core / base entity
                -- =========================
                CREATE TABLE IF NOT EXISTS Card (
                    CardID   INTEGER PRIMARY KEY,
                    Title    TEXT    NOT NULL DEFAULT '',
                    Tags     TEXT    NOT NULL DEFAULT ''
                );

                -- =========================
                -- TAT
                -- =========================
                CREATE TABLE IF NOT EXISTS TatCard (
                    TatCardID               INTEGER PRIMARY KEY,
                    CardID                  INTEGER NOT NULL,
                    ValuePerMinute          REAL    NOT NULL,
                    Status                  TEXT    NOT NULL DEFAULT '',
                    Description             TEXT    NOT NULL DEFAULT '',
                    TargetActiveTimeSeconds INTEGER NULL,
                    FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS TatCardValueRate (
                    TatCardValueRateID INTEGER PRIMARY KEY,
                    TatCardID          INTEGER NOT NULL,
                    RateName           TEXT    NOT NULL DEFAULT '',
                    ValuePerMinute     REAL    NOT NULL,
                    FOREIGN KEY (TatCardID) REFERENCES TatCard(TatCardID) ON DELETE CASCADE
                );

                -- =========================
                -- SC
                -- =========================
                CREATE TABLE IF NOT EXISTS ScCard (
                    ScCardID      INTEGER PRIMARY KEY,
                    CardID        INTEGER NOT NULL,
                    Status        TEXT    NOT NULL DEFAULT '',
                    Description   TEXT    NOT NULL DEFAULT '',
                    FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ScCardStep (
                    ScCardStepID  INTEGER PRIMARY KEY,
                    ScCardID      INTEGER NOT NULL,
                    SortOrder       INTEGER NOT NULL,
                    Title         TEXT    NOT NULL DEFAULT '',
                    StepValue     REAL    NOT NULL,
                    FOREIGN KEY (ScCardID) REFERENCES ScCard(ScCardID) ON DELETE CASCADE
                );

                -- No separate ID in model: composite primary key works well here.
                CREATE TABLE IF NOT EXISTS ScCardStepRep (
                    ScCardStepID  INTEGER NOT NULL,
                    TimeStamp     TEXT    NOT NULL,  -- ISO-8601 datetime
                    StepValue     REAL    NOT NULL,
                    PRIMARY KEY (ScCardStepID, TimeStamp),
                    FOREIGN KEY (ScCardStepID) REFERENCES ScCardStep(ScCardStepID) ON DELETE CASCADE
                );

                -- =========================
                -- Mission
                -- =========================
                CREATE TABLE IF NOT EXISTS MissionCard (
                    MissionCardID          INTEGER PRIMARY KEY,
                    CardID                 INTEGER NOT NULL,

                    Status                 TEXT    NOT NULL DEFAULT '',
                    Description            TEXT    NOT NULL DEFAULT '',
                    SubType                TEXT    NOT NULL DEFAULT '',

                    Value                  REAL    NOT NULL,

                    CreatedDate            TEXT    NOT NULL, -- ISO-8601 datetime
                    AvailableFromDate      TEXT    NOT NULL, -- ISO-8601 datetime
                    DueDate                TEXT    NOT NULL, -- ISO-8601 datetime
                    CompletedDate          TEXT    NULL,     -- ISO-8601 datetime
                    EventDate              TEXT    NULL,     -- ISO-8601 datetime 

                    EstCompletionTimeText  TEXT    NOT NULL DEFAULT '',

                    IsFailed               INTEGER NOT NULL DEFAULT 0, -- bool
                    ValuePerMinute         REAL    NOT NULL,

                    FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE
                );

                -- =========================
                -- Budget
                -- =========================
                CREATE TABLE IF NOT EXISTS BudgetCard (
                    BudgetCardID     INTEGER PRIMARY KEY,
                    CardID           INTEGER NOT NULL,

                    Status           TEXT    NOT NULL DEFAULT '',
                    Description      TEXT    NOT NULL DEFAULT '',

                    Currency         TEXT    NOT NULL DEFAULT '',
                    ExchangeRate     REAL    NOT NULL,

                    StartDate        TEXT    NOT NULL,  -- ISO-8601 date/datetime
                    InitialBalance   REAL    NOT NULL,

                    FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS BudgetCardScheduledTopUp (
                    BudgetCardScheduledTopUpID INTEGER PRIMARY KEY,
                    BudgetCardID               INTEGER NOT NULL,
                    Amount                     REAL    NOT NULL,
                    TimeOfDaySeconds           INTEGER NOT NULL, -- TimeSpan stored as seconds from 00:00:00
                    FOREIGN KEY (BudgetCardID) REFERENCES BudgetCard(BudgetCardID) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS BudgetCardTransaction (
                    BudgetCardTransactionID INTEGER PRIMARY KEY,
                    BudgetCardID            INTEGER NOT NULL,
                    Amount                  REAL    NOT NULL,
                    Type                    TEXT    NOT NULL DEFAULT '',
                    TimeStamp               TEXT    NOT NULL, -- ISO-8601 datetime
                    FOREIGN KEY (BudgetCardID) REFERENCES BudgetCard(BudgetCardID) ON DELETE CASCADE
                );

                -- =========================
                -- Achievements
                -- =========================
                CREATE TABLE IF NOT EXISTS AchievementCard (
                    AchievementCardID      INTEGER PRIMARY KEY,
                    CardID             INTEGER NOT NULL,

                    Status             TEXT    NOT NULL DEFAULT '',
                    Description        TEXT    NOT NULL DEFAULT '',
                    GoalType           TEXT    NOT NULL DEFAULT '',
                    DifficultyLevel    TEXT    NOT NULL DEFAULT 'Easy', 

                    CreatedDate        TEXT    NOT NULL, -- ISO-8601 datetime
                    LastEarnedAt       TEXT    NULL,     -- ISO-8601 datetime

                    -- Only For GoalType = ActiveTime
                    TargetActiveTimeInSeconds  INTEGER NULL, 

                    -- Only for GoalType = Value
                    TargetValue        INTEGER NULL, 

                    -- Only for GoalType = Step
                    ScCardStepID       INTEGER NULL,    

                    CompletionType     TEXT    NOT NULL DEFAULT 'Range',

                    --Only for CompletionType = Range
                    RangeUnit          TEXT NULL,
                    RangeAmount        INTEGER NULL,

                    --Only for CompletionType = Deadline
                    Deadline           TEXT    NULL,     -- ISO-8601 datetime

                    TrophyURLs         TEXT    NOT NULL DEFAULT '',
                    IsPinned           INTEGER     NOT NULL DEFAULT 0,

                    FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE,
                    FOREIGN KEY (ScCardStepID) REFERENCES ScCardStep(ScCardStepID) ON DELETE SET NULL
                );

                CREATE TABLE IF NOT EXISTS AchievementTrophy (
                    TrophyID       INTEGER PRIMARY KEY,
                    AchievementCardID  INTEGER NOT NULL,
                    Title          TEXT    NOT NULL DEFAULT '',
                    EarnedOn       TEXT    NOT NULL, -- ISO-8601 date/datetime
                    ImageSource    TEXT    NOT NULL DEFAULT '',
                    FOREIGN KEY (AchievementCardID) REFERENCES AchievementCard(AchievementCardID) ON DELETE CASCADE
                );

                -- =========================
                -- Activity (time slices)
                -- =========================
                CREATE TABLE IF NOT EXISTS Activity (
                    ActivityID      INTEGER PRIMARY KEY,
                    CardID          INTEGER NOT NULL,
                    Start           TEXT    NOT NULL, -- ISO-8601 datetime
                    ""End""           TEXT    NULL,     -- NULL = open
                    ValueRateName   TEXT    NOT NULL,
                    ValuePerMinute  REAL    NOT NULL,
                    FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE,

                    -- Optional sanity check: if End exists, Start must be < End
                    CHECK (""End"" IS NULL OR Start < ""End"")
                );

                -- =========================
                -- Trackers
                -- =========================

                CREATE TABLE IF NOT EXISTS ValueTrackerCard (
                    ValueTrackerCardID  INTEGER PRIMARY KEY,
                    CardID              INTEGER NOT NULL,

                    Unit                TEXT    NOT NULL DEFAULT '',
                    CreatedDate         TEXT    NOT NULL, -- ISO-8601 datetime
                    RangeStart          TEXT    NOT NULL, -- ISO-8601 datetime

                    ScheduleEvery       INTEGER NOT NULL DEFAULT 1,
                    ScheduleUnit        TEXT    NOT NULL DEFAULT 'Week',

                    FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS EventTrackerCard (
                    EventTrackerCardID  INTEGER PRIMARY KEY,
                    CardID              INTEGER NOT NULL,

                    Unit                TEXT    NOT NULL DEFAULT '',
                    CreatedDate         TEXT    NOT NULL, -- ISO-8601 datetime
                    RangeStart          TEXT    NOT NULL, -- ISO-8601 datetime

                    GroupByPeriod       TEXT    NOT NULL DEFAULT 'Day',

                    FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE
                );

                -- Stores raw points/events for BOTH tracker types (linked via CardID)
                CREATE TABLE IF NOT EXISTS TrackerValue (
                    TrackerValueID  INTEGER PRIMARY KEY,
                    CardID          INTEGER NOT NULL,
                    TimeStamp       TEXT    NOT NULL, -- ISO-8601 datetime
                    Value           REAL    NOT NULL,
                    FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE
                );


                -- =========================
                -- Settings / StatusConditions / Themes
                -- =========================

                CREATE TABLE IF NOT EXISTS StatusCondition (
                    StatusConditionID            INTEGER PRIMARY KEY,
                    StatusConditionName          TEXT    NOT NULL DEFAULT '',
                    StatusConditionMultiplierValue REAL   NOT NULL DEFAULT 1.0
                );

                CREATE TABLE IF NOT EXISTS Theme (
                    ThemeID      INTEGER PRIMARY KEY,
                    ThemeName    TEXT    NOT NULL DEFAULT ''
                );

                CREATE TABLE IF NOT EXISTS ThemeConfiguration (
                    ThemesConfigID  INTEGER PRIMARY KEY,
                    ThemeID         INTEGER NOT NULL,

                    -- Main page
                    MainPageBackColor  TEXT NOT NULL DEFAULT '',
                    MainPageForeColor  TEXT NOT NULL DEFAULT '',
                    GlobalFontFamily   TEXT NOT NULL DEFAULT '',

                    -- Cards
                    ScCardBackColor      TEXT NOT NULL DEFAULT '',
                    ScCardForeColor      TEXT NOT NULL DEFAULT '',
                    TatCardBackColor     TEXT NOT NULL DEFAULT '',
                    TatCardForeColor     TEXT NOT NULL DEFAULT '',
                    MissionCardBackColor TEXT NOT NULL DEFAULT '',
                    MissionCardForeColor TEXT NOT NULL DEFAULT '',
                    BudgetCardBackColor  TEXT NOT NULL DEFAULT '',
                    BudgetCardForeColor  TEXT NOT NULL DEFAULT '',

                    -- Labels
                    DueInLabelForeColor  TEXT NOT NULL DEFAULT '',

                    -- Negative button
                    NegativeButtonBackColor        TEXT NOT NULL DEFAULT '',
                    NegativeButtonForeColor        TEXT NOT NULL DEFAULT '',
                    NegativeButtonStyle            TEXT NOT NULL DEFAULT 'Round',
                    NegativeButtonBorderThickness  REAL NOT NULL DEFAULT 0,
                    NegativeButtonBorderColor      TEXT NOT NULL DEFAULT '',

                    -- Positive button
                    PositiveButtonBackColor        TEXT NOT NULL DEFAULT '',
                    PositiveButtonForeColor        TEXT NOT NULL DEFAULT '',
                    PositiveButtonStyle            TEXT NOT NULL DEFAULT 'Round',
                    PositiveButtonBorderThickness  REAL NOT NULL DEFAULT 0,
                    PositiveButtonBorderColor      TEXT NOT NULL DEFAULT '',

                    -- Active toggle ON
                    ActiveToggleOnBackColor              TEXT NOT NULL DEFAULT '',
                    ActiveToggleOnForeColor              TEXT NOT NULL DEFAULT '',
                    ActiveToggleOnButtonStyle            TEXT NOT NULL DEFAULT 'Round',
                    ActiveToggleOnButtonBorderThickness  REAL NOT NULL DEFAULT 0,
                    ActiveToggleOnButtonBorderColor      TEXT NOT NULL DEFAULT '',

                    -- Active toggle OFF
                    ActiveToggleOffBackColor              TEXT NOT NULL DEFAULT '',
                    ActiveToggleOffForeColor              TEXT NOT NULL DEFAULT '',
                    ActiveToggleOffButtonStyle            TEXT NOT NULL DEFAULT 'Round',
                    ActiveToggleOffButtonBorderThickness  REAL NOT NULL DEFAULT 0,
                    ActiveToggleOffButtonBorderColor      TEXT NOT NULL DEFAULT '',

                    -- Global value thresholds
                    GlobalValueBelowZeroThresholdForeColor             TEXT NOT NULL DEFAULT '',
                    GlobalValueNonZeroBelowThresholdForeColor          TEXT NOT NULL DEFAULT '',
                    GlobalValueAboveThresholdForeColor                 TEXT NOT NULL DEFAULT '',
                    GlobalValueAboveSecondaryThresholdForeColor        TEXT NOT NULL DEFAULT '',

                    -- Card border
                    CardBorderStyle       TEXT NOT NULL DEFAULT 'RoundedEdges',
                    CardBorderThickness   REAL NOT NULL DEFAULT 0,
                    CardBorderColor       TEXT NOT NULL DEFAULT '',

                    -- Section display names
                    MainQuestSectionDisplayName            TEXT NOT NULL DEFAULT '',
                    MissionSectionDisplayName              TEXT NOT NULL DEFAULT '',
                    BudgetSectionDisplayName               TEXT NOT NULL DEFAULT '',
                    ArcsSectionDisplayName                 TEXT NOT NULL DEFAULT '',
                    PinnedAchievementsSectionDisplayName   TEXT NOT NULL DEFAULT '',

                    FOREIGN KEY (ThemeID) REFERENCES Theme(ThemeID) ON DELETE CASCADE
                );

                -- Singleton settings row pattern: one row with SettingsID=1
                CREATE TABLE IF NOT EXISTS AppSettings (
                    SettingsID  INTEGER PRIMARY KEY CHECK (SettingsID = 1),

                    HardModeEnabled              INTEGER NOT NULL DEFAULT 0,
                    HardModeDamagePerMinuteValue REAL    NOT NULL DEFAULT 0, -- store as NEGATIVE

                    StatusConditionsEnabled         INTEGER NOT NULL DEFAULT 0,
                    CurrentlyAppliedStatusConditionID INTEGER NULL,

                    SelectedThemeID INTEGER NULL,

                    FOREIGN KEY (CurrentlyAppliedStatusConditionID) REFERENCES StatusCondition(StatusConditionID) ON DELETE SET NULL,
                    FOREIGN KEY (SelectedThemeID) REFERENCES Theme(ThemeID) ON DELETE SET NULL
                );

                CREATE TABLE IF NOT EXISTS CardSchedule (
                    ScheduleId     INTEGER PRIMARY KEY,
                    CardId         INTEGER NOT NULL,
                    IsEnabled      INTEGER NOT NULL DEFAULT 1,
                    Note          TEXT    NOT NULL DEFAULT '',
                    FrequencyType  INTEGER NOT NULL,
                    FrequencyValue INTEGER NOT NULL DEFAULT 0,
                    FromDateTime   TEXT    NOT NULL, -- ISO-8601
                    ToDateTime     TEXT    NULL      -- ISO-8601 or NULL
                );

                -- =========================
                -- Planner
                -- =========================
                -- Stores per-card goal configuration used by the planner UI.
                -- One row per (CardID, TimeScope).
                CREATE TABLE IF NOT EXISTS PlannerGoal (
                    PlannerGoalID INTEGER PRIMARY KEY,
                    CardID        INTEGER NOT NULL,
                    TimeScope     TEXT    NOT NULL, -- enum as string, e.g. Daily/Weekly/Monthly
                    GoalHrs       REAL    NOT NULL,
                    Enabled       INTEGER NOT NULL DEFAULT 0, -- bool (0/1)
                    DeFactoStart  TEXT    NULL, -- TimeOnly as ""HH:mm:ss""
                    DeFactoEnd    TEXT    NULL, -- TimeOnly as ""HH:mm:ss""
                    FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE,
                    UNIQUE (CardID, TimeScope)
                );

                -- =========================
                -- Locks
                -- =========================
                CREATE TABLE IF NOT EXISTS Lock (
                    LockId           INTEGER PRIMARY KEY AUTOINCREMENT,
                    LockNumber       INTEGER NOT NULL,
                    CardId           INTEGER NOT NULL,
                    TimeWindowStart  TEXT NOT NULL, -- ISO-8601
                    TimeWindowEnd    TEXT NOT NULL  -- ISO-8601
                );

                CREATE TABLE IF NOT EXISTS LockSchedule (
                    ScheduleId      INTEGER PRIMARY KEY AUTOINCREMENT,
                    LockId          INTEGER NOT NULL,
                    FrequencyType   INTEGER NOT NULL, -- 0=Daily,1=Weekly,2=Monthly
                    FrequencyValue  INTEGER NOT NULL DEFAULT 0,
                    FromDateTime    TEXT NOT NULL,    -- ISO-8601
                    ToDateTime      TEXT NULL         -- ISO-8601 or NULL
                );

                CREATE TABLE IF NOT EXISTS LockTaskDependency (
                    LockTaskDependencyId INTEGER PRIMARY KEY AUTOINCREMENT,
                    LockId               INTEGER NOT NULL,
                    TaskDependencyCardId INTEGER NOT NULL,
                    MetricType           INTEGER NOT NULL DEFAULT 0, -- 0=ActiveTime,1=Points
                    TimeScope            INTEGER NOT NULL DEFAULT 0,  -- 0=Daily,1=Weekly,2=Monthly
                    GoalValue            REAL NOT NULL DEFAULT 0,
                    GoalValence          INTEGER NOT NULL DEFAULT 0  -- 0=MustBeGreaterThan, 1=MustBeLessThan
                );

                -- =========================
                -- Helpful indexes
                -- =========================
                CREATE INDEX IF NOT EXISTS IX_TatCard_CardID              ON TatCard(CardID);
                CREATE INDEX IF NOT EXISTS IX_TatCardValueRate_TatCardID  ON TatCardValueRate(TatCardID);

                CREATE INDEX IF NOT EXISTS IX_ScCard_CardID               ON ScCard(CardID);
                CREATE INDEX IF NOT EXISTS IX_ScCardStep_ScCardID         ON ScCardStep(ScCardID);
                CREATE INDEX IF NOT EXISTS IX_ScCardStepRep_TimeStamp     ON ScCardStepRep(TimeStamp);

                CREATE INDEX IF NOT EXISTS IX_MissionCard_CardID          ON MissionCard(CardID);
                CREATE INDEX IF NOT EXISTS IX_MissionCard_Status          ON MissionCard(Status);
                CREATE INDEX IF NOT EXISTS IX_MissionCard_DueDate         ON MissionCard(DueDate);

                CREATE INDEX IF NOT EXISTS IX_BudgetCard_CardID           ON BudgetCard(CardID);
                CREATE INDEX IF NOT EXISTS IX_BudgetTxn_BudgetCardID      ON BudgetCardTransaction(BudgetCardID);
                CREATE INDEX IF NOT EXISTS IX_BudgetTxn_TimeStamp         ON BudgetCardTransaction(TimeStamp);

                CREATE INDEX IF NOT EXISTS IX_Achievement_CardID          ON AchievementCard(CardID);
                CREATE INDEX IF NOT EXISTS IX_Achievement_ScCardStepID    ON AchievementCard(ScCardStepID);
                CREATE INDEX IF NOT EXISTS IX_Trophy_AchievementID        ON AchievementTrophy(AchievementCardID);

                CREATE INDEX IF NOT EXISTS IX_Activity_CardID             ON Activity(CardID);
                -- At most ONE open activity in the whole DB
                CREATE UNIQUE INDEX IF NOT EXISTS UX_Activity_OneOpen
                ON Activity(1)
                WHERE ""End"" IS NULL;

                -- =========================
                -- Dashboard Shortcuts
                -- =========================
                CREATE TABLE IF NOT EXISTS ShortcutGroup (
                    ShortcutGroupId     INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name                TEXT    NOT NULL,
                    Color               TEXT    NOT NULL DEFAULT '#FF000000', -- store as #AARRGGBB
                    ShortcutGroupOrder  INTEGER NOT NULL DEFAULT 0
                );

                -- Optional but strongly recommended: prevent duplicate group names
                CREATE UNIQUE INDEX IF NOT EXISTS UX_ShortcutGroup_Name
                ON ShortcutGroup(Name);

                CREATE TABLE IF NOT EXISTS Shortcut (
                    ShortcutId        INTEGER PRIMARY KEY AUTOINCREMENT,
                    IconChar          TEXT    NOT NULL DEFAULT '',
                    TargetCardId      INTEGER NOT NULL,
                    ShortcutGroupId   INTEGER NOT NULL,
                    ShortcutOrder     INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (ShortcutGroupId) REFERENCES ShortcutGroup(ShortcutGroupId) ON DELETE CASCADE
                );

                -- Helpful for overlap queries
                CREATE INDEX IF NOT EXISTS IX_Activity_StartEnd
                ON Activity(Start, ""End"");

                CREATE INDEX IF NOT EXISTS IX_ValueTracker_CardID      ON ValueTrackerCard(CardID);
                CREATE INDEX IF NOT EXISTS IX_EventTracker_CardID      ON EventTrackerCard(CardID);

                CREATE INDEX IF NOT EXISTS IX_TrackerValue_CardID      ON TrackerValue(CardID);
                CREATE INDEX IF NOT EXISTS IX_TrackerValue_TimeStamp   ON TrackerValue(TimeStamp);

                CREATE INDEX IF NOT EXISTS IX_ThemeConfiguration_ThemeID ON ThemeConfiguration(ThemeID);
                CREATE INDEX IF NOT EXISTS IX_AppSettings_SelectedThemeID ON AppSettings(SelectedThemeID);
                CREATE INDEX IF NOT EXISTS IX_AppSettings_StatusConditionID ON AppSettings(CurrentlyAppliedStatusConditionID);

                CREATE INDEX IF NOT EXISTS IX_CardSchedule_CardId ON CardSchedule(CardId);

                CREATE INDEX IF NOT EXISTS IX_PlannerGoal_CardID       ON PlannerGoal(CardID);
                CREATE INDEX IF NOT EXISTS IX_PlannerGoal_Enabled ON PlannerGoal(Enabled);

                -- Lookup locks by card
                CREATE INDEX IF NOT EXISTS IX_Lock_CardId ON Lock(CardId);

                -- Ensure fast ordered retrieval per card
                CREATE INDEX IF NOT EXISTS IX_Lock_CardId_LockNumber ON Lock(CardId, LockNumber);


                -- Lookup schedules for a lock
                CREATE INDEX IF NOT EXISTS IX_LockSchedule_LockId ON LockSchedule(LockId);

                -- Optional: optimise evaluation by frequency
                CREATE INDEX IF NOT EXISTS IX_LockSchedule_LockId_Frequency ON LockSchedule(LockId, FrequencyType);

                -- Optional: optimise date-range checks
                CREATE INDEX IF NOT EXISTS IX_LockSchedule_DateRange ON LockSchedule(FromDateTime, ToDateTime);

                -- Lookup dependencies per lock
                CREATE INDEX IF NOT EXISTS IX_LockTaskDependency_LockId ON LockTaskDependency(LockId);

                -- Optimise dependency evaluation by card
                CREATE INDEX IF NOT EXISTS IX_LockTaskDependency_TaskCard ON LockTaskDependency(TaskDependencyCardId);

                -- Optimise scoped dependency checks
                CREATE INDEX IF NOT EXISTS IX_LockTaskDependency_TaskCard_TimeScope ON LockTaskDependency(TaskDependencyCardId, TimeScope);

                -- Dashboard ordering retrieval
                CREATE INDEX IF NOT EXISTS IX_ShortcutGroup_Order ON ShortcutGroup(ShortcutGroupOrder, ShortcutGroupId);

                CREATE INDEX IF NOT EXISTS IX_Shortcut_Group_Order ON Shortcut(ShortcutGroupId, ShortcutOrder, ShortcutId);

                -- Optional: quicker reverse lookup / diagnostics
                CREATE INDEX IF NOT EXISTS IX_Shortcut_TargetCardId ON Shortcut(TargetCardId);

                ";
        }

        public static string GenerateDbWipeDataScript()
        {
            // Wipes data only (keeps tables + indexes). Uses FK OFF to avoid delete-order constraints.
            return @"
                    DELETE FROM Shortcut;
                ";
        }


    }
}
