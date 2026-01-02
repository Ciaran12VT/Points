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
                    TatCardID        INTEGER PRIMARY KEY,
                    CardID           INTEGER NOT NULL,
                    ValuePerMinute   REAL    NOT NULL,
                    Status           TEXT    NOT NULL DEFAULT '',
                    Description      TEXT    NOT NULL DEFAULT '',
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
                    SubType            TEXT    NOT NULL DEFAULT '',

                    CreatedDate        TEXT    NOT NULL, -- ISO-8601 datetime
                    AvailableFromDate  TEXT    NOT NULL, -- ISO-8601 datetime
                    DueDate            TEXT    NOT NULL, -- ISO-8601 datetime

                    CompletedDate      TEXT    NULL,     -- ISO-8601 datetime
                    LastEarnedAt       TEXT    NULL,     -- ISO-8601 datetime

                    ScCardStepID       INTEGER NULL,

                    ProgressType       TEXT    NOT NULL DEFAULT '',
                    RangeAmount        INTEGER NULL,
                    Deadline           TEXT    NULL,     -- ISO-8601 datetime

                    TrophyURLs         TEXT    NOT NULL DEFAULT '',

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
                    ""End""         TEXT    NOT NULL, -- ISO-8601 datetime
                    ValuePerMinute  REAL    NOT NULL,
                    FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE
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
                CREATE INDEX IF NOT EXISTS IX_Activity_Start              ON Activity(Start);
                ";
        }

    }
}
