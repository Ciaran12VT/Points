# Time Handling Contract

This document defines the intended time contract for the application. Every
stored or compared temporal value should be classified before implementation:
UTC instant, local date, local time, or local schedule definition.

## Rules

1. Persist historical instants in UTC only.
   - Store as ISO-8601 round-trip text with a UTC designator, for example
     `2026-04-28T14:30:00.0000000Z`.
   - Convert to the current UI timezone only for display or picker editing.
   - Do not use `DateTime.SpecifyKind` to convert local values to UTC.

2. Preserve wall-clock user intent as wall-clock data.
   - Dates the user means as calendar dates should be stored as `yyyy-MM-dd`.
   - Times the user means as time-of-day should be stored as `HH:mm:ss`.
   - Recurring schedules should store local schedule definitions and resolve
     each occurrence to UTC only when creating/querying an actual instant.

3. Centralize all conversions.
   - DB serialization/parsing should go through `StrictTimeSerializer`.
   - UI date/time picker values are local values until explicitly converted.
   - Date range filters are local UI ranges and should be converted to UTC
     before querying UTC instant columns.

4. DST must be explicit.
   - Invalid local times during spring-forward transitions must be handled by
     policy, not accidentally normalized.
   - Ambiguous local times during fall-back transitions must be resolved by
     policy, not guessed by `DateTime`.

## Field Classification

| Area | Field(s) | Classification | Persistence target |
| --- | --- | --- | --- |
| Activity | `Activity.Start`, `Activity.End` | UTC instant | UTC ISO text |
| Step reps | `ScCardStepRep.TimeStamp` | UTC instant | UTC ISO text |
| Tracker values | `TrackerValue.TimeStamp` | UTC instant | UTC ISO text |
| Value trackers | `ValueTracker.CreatedDate`, `ValueTracker.RangeStart` | Local UI/default date-time today; convert to range boundary when querying instants | Review during refactor |
| Event trackers | `EventTracker.CreatedDate`, `EventTracker.RangeStart` | Local UI/default date-time today; convert to range boundary when querying instants | Review during refactor |
| Budget transactions | `BudgetCardTransaction.TimeStamp` | UTC instant | UTC ISO text |
| Budget definition | `BudgetCard.StartDate` | Local schedule/start definition | Local date-time or date plus time |
| Budget top-ups | `BudgetCardScheduledTopUp.TimeOfDaySeconds` | Local time | Seconds since local midnight |
| Missions | `MissionCard.CreatedDate` | UTC instant | UTC ISO text |
| Missions | `MissionCard.AvailableFromDate`, `MissionCard.DueDate`, `MissionCard.EventDate` | Local wall-clock deadline/event definition | Local date-time plus timezone policy |
| Missions | `MissionCard.CompletedDate` | UTC instant | UTC ISO text |
| Achievements | `AchievementCard.CreatedDate` | UTC instant | UTC ISO text |
| Achievements | `AchievementCard.LastEarnedAt`, `AchievementCard.FinalizedAt`, `AchievementTrophy.EarnedOn` | UTC instant | UTC ISO text |
| Achievements | `AchievementCard.DeadlineStart`, `AchievementCard.Deadline` | Local wall-clock deadline definition | Local date-time plus timezone policy |
| Card schedules | `CardSchedule.FromDateTime`, `CardSchedule.ToDateTime` | Local schedule definition | Local date-time plus timezone id when available |
| Notification logs | `NotificationLog.CreatedAt`, `ScheduledAt`, `ScheduleFor`, `SentAt`, `UpdatedAt` | UTC instant occurrence/log data | UTC ISO text |
| Planner | `Planner.PlannerDate` | Local date | `yyyy-MM-dd` |
| Planner | `Planner.CreatedAt`, `Planner.UpdatedAt` | UTC instant | UTC ISO text |
| Planner | `PlannerTask.PlannedStart`, `PlannerTask.PlannedEnd`, `PlannerEvent.PlannedTime` | Local planner date-time | Local date-time, scoped to `PlannerDate` |
| Locks | `Lock.TimeWindowStart`, `Lock.TimeWindowEnd` | Local time | `HH:mm:ss` |
| Lock schedules | `LockSchedule.FromDateTime`, `LockSchedule.ToDateTime` | Local schedule definition | Local date-time plus timezone id when available |
| Goals | `Goal.DeFactoStart`, `Goal.DeFactoEnd` | Local time | `HH:mm:ss` |
| Reports | `Report.LastRunOn` | UTC instant | UTC ISO text |
| UDMD date fields | `UdmdTrans.FieldValue` for `Date` fields | Local date unless configured as an instant | `yyyy-MM-dd` for date-only values |
| Backups | Manifest `CreatedAtUtc` | UTC instant | UTC ISO text |
| Backups | Export package filename timestamp | Local display/filename value | Local formatted text |

## Implementation Notes

- Existing data may contain a mixture of UTC, local, and unspecified
  `DateTime` values. Migration code must preserve legacy reads until all
  instant columns are normalized.
- `LegacyTimeReader` is the compatibility helper for old rows:
  no-offset legacy instant values are interpreted as local wall-clock values
  in the device timezone and converted to UTC; wall-clock schedule definitions
  with old offsets preserve the written clock time and ignore the offset.
- Future instant-oriented model/API names should prefer a `Utc` suffix.
- Future wall-clock-oriented model/API names should prefer `Local`, `DateOnly`,
  or `TimeOnly` semantics instead of plain `DateTime`.
- `DateTime.Now`, `DateTime.Today`, `.ToString("o")`, `DateTime.Parse`, and
  `DateTime.SpecifyKind` should be considered temporary legacy patterns until
  the central time boundary exists.
- `StrictTimeSerializer` is the strict DB-facing helper for new work:
  `SerializeUtcInstant` rejects non-UTC `DateTime`s, while
  `SerializeUtcInstantFromLocal` is the explicit conversion path for local UI
  picker values.

## Guardrails

- `Points.Tests/Time/TimeHandlingGuardrailTests.cs` scans application source
  for direct current-time APIs, direct timezone conversion shortcuts, direct
  parsing, and raw `DateTime.ToString("o")` persistence writes.
- The guardrails use explicit per-file allowances for legacy code still being
  retired. New occurrences should normally be fixed by using `IClock`,
  `ITimeZoneService`, `StrictTimeSerializer`, `LegacyTimeReader`, or a
  domain-specific helper that names whether a value is an instant or a
  wall-clock value.
- If an allowance must be changed, update it together with a short explanation
  in the refactor notes or pull request so the remaining debt stays visible.
