# Points Application — Functional and Data Compatibility Specification

## 1. Purpose and conformance target

This document specifies the observable behavior, navigation, calculations, persistence model, file storage, and interoperability requirements of the Points application. It is intended to be sufficient to reimplement the product in another UI or architectural stack—specifically including a native Android Studio/Kotlin implementation—while retaining the ability to open or import the existing `points.db3` SQLite database and its associated files without losing meaning.

A conforming replacement must preserve:

- every user-visible workflow described here;
- the distinction between the seven configurable home panes;
- the card hierarchy and all card-specific calculations;
- one-open-activity semantics, schedules, locks, goals, achievements, planners, metadata, reports, shortcuts, multipliers, notification logs, backups, and watch commands;
- table and column names, enum encodings, identifier values, foreign-key behavior, indexes, and timestamp semantics documented in section 18;
- the non-database attachment folders described in section 19;
- historical rows when a card is removed. A removal that would orphan meaningful transaction/history data must archive the subtype row instead of deleting it.

“Current application” below means the behavior represented by the source at the time of this specification. Some implemented features are deliberately hidden by feature flags or platform capability. They are still described because they are part of the application and data contract.

## 2. Product model

Points is a personal activity and value accounting application. Its core concept is a polymorphic `Card`. A base `Card` supplies a stable `CardID`, display order, title, and free-form tag string. Exactly one subtype row normally supplies the behavior:

- Time-At-Task (TAT): points accrue while the card is active.
- Step-Completion (SC): points accrue from counted step repetitions and the card can also record active time.
- Mission: a dated task with a completion prize, optional time-based accrual, and Stable/Degrade/Rot behavior.
- Budget: a recurring allowance in a named currency, with spending and optional conversion (“Cash In”) to global points.
- Achievement: a target evaluated from time, value, steps, another achievement, or a custom report.
- Value Tracker: a time series of numeric measurements.
- Event Tracker: a time series of occurrences grouped by day/week/month/year.

The application computes a global value for the active display range. TAT and Mission values are affected by the currently active custom multiplier. SC values are not multiplied by the custom multiplier. Budget cards contribute cashed-in value plus any negative remaining budget value. Hard Mode contributes an idle penalty when enabled. The total is refreshed while the home page is visible.

## 3. Startup, lifecycle, and navigation

On startup the application initializes or migrates the SQLite database, loads settings, constructs enabled home panes in configured order, loads all card families and related rows, restores the single open `Activity` as the active card, loads the active custom multiplier, builds dashboard shortcuts and goals, schedules notifications, and calculates the current global value.

The home page is the root page. Detail screens are pushed onto a navigation stack and return with Save/Done or back navigation. Many destructive actions are presented from the close/cancel button as an action sheet rather than a separate permanent delete button.

While Home is visible:

- a one-second timer updates `Now`, active-time displays, mission countdown/value state, budget countdowns, achievements, locks, and global value;
- automatic-export due checks run immediately and then every minute;
- missed notification counts refresh;
- a pending tap on an Android active-card notification navigates to and centers the target card;
- the first appearance may show the premium upgrade prompt.

Only one `Activity` may have a null end time in the entire database. Activating a second active card closes the existing open interval before opening the new interval.

## 4. Main view structure

### 4.1 Shell title bar

The system title area contains, left to right:

1. The formatted header date.
2. The active phase/card name beneath the date. It is gray when no card is active and otherwise colored according to the active card’s sign/value state.
3. A red exclamation mark when an available Rot mission currently has a negative value.
4. The active custom multiplier code, in amber, when a multiplier interval is open.
5. The global value, formatted to two decimal places. Negative values are red, values below 100 are orange, and larger nonnegative values use the positive color. Tapping the value opens the Leaderboard/Planner popup.

### 4.2 Expandable top action bar

This row can be hidden or shown from the ellipsis button on the pane-navigation row. It contains:

- Achievements: opens the full achievements list.
- Reports: opens saved SQL reports.
- Goals: opens the goal configuration list.
- Order mode: toggles card reorder controls. The button turns green when active. Supported panes show Up and Down buttons on each card. Reordering normalizes `Card.DisplayOrder` to zero-based list order and persists it.
- Settings: opens the settings menu.

The source contains disabled date-range toolbar code; the current visible UI does not show that button.

### 4.3 Pane navigation row

A horizontally scrolling list contains one small colored circular icon per enabled pane. Tapping an icon jumps the carousel to that pane. Pane presence and order come from `Setting` rows, with defaults shown below:

| Default order | Pane | Contents | Empty state |
|---:|---|---|---|
| 1 | Dashboard | shortcut grid | Create a card, then add a dashboard shortcut. |
| 2 | Main Quest | TAT and SC | Create a TAT or SC card. |
| 3 | Mission | Missions | Create a mission card. |
| 4 | Budgets | Budgets | Create a budget card. |
| 5 | Challenges & Pinned Achievements | pinned/challenge achievements | Create or pin an achievement. |
| 6 | Arcs | value and event trackers | Create a value or event tracker. |
| 7 | Goals | enabled goal progress rows | Configure goals to see progress here. |

A missed-notification badge appears to the right when missed notifications exist. Its text is the count and its color reflects status; tapping it opens the Notification Log. The final ellipsis button toggles the action bar above.

### 4.4 Carousel content

The central control is horizontally swipeable. Each non-dashboard pane has a title and vertically scrolling card list. The dashboard uses a four-column grid of 74×74 rounded shortcut buttons. Scrolling and programmatic jumps suppress competing interactions briefly so that navigation can materialize the pane and center the requested card.

### 4.5 Premium banner

When the subscription service reports non-premium status and the banner should be shown, a banner reads “Upgrade to Premium!” with an Upgrade button. The popup presents feature slides for achievements, Arcs, budgets, goals, Main Quest, reports, and trophies. Purchasing is not implemented by the hardcoded subscription service; the popup is informational in this build.

### 4.6 Bottom bar

- Active-card locator: a colored dot matching the active phase. It jumps to and centers the active card. It is inert if no card is active.
- Search: prompts for text and filters the current pane to cards whose title or tags contain the case-insensitive substring. Filtering affects `VisibleCards`, not persistence.
- Dashboard: jumps to the Dashboard pane.
- Add: creates the type appropriate to the current pane. Main Quest asks Time-At-Task or Step-Completion; Arcs asks Value Tracker or Event Tracker. Mission and Budget create their corresponding type directly. Achievements are added from the dedicated achievements screen. Dashboard and Goals have no direct card creation.

Latent commands also support positive/negative filtering, tag filtering, clearing filters, and sorting active cards by last-active time, although their home buttons are commented out in the current XAML.

The latent Date Range command opens a shared range picker. It has start date/time and end date/time controls plus quick Daily, Weekly, and Monthly range selection, Cancel, and Apply. Apply rejects an end before start, updates the process-wide range, header text, card values, goals, and global value. The default range is the current local daily range. Although the toolbar button is commented out, this behavior should remain available to preserve feature parity.

## 5. Shared card behavior

All cards retain `CardID`, `DisplayOrder`, `Title`, and the unparsed `Tags` string. Tags are displayed as entered and are searched by substring. Tag editors use a searchable multi-select screen built from known tags; the selected display is a string and can be cleared.

Tapping a card body opens its details form. Active-capable cards show a circular activity toggle. A short tap starts or stops activity. A long press opens an effective start/end editor so a user can backdate the transition. Starting an interval may first prompt for a TAT value rate and for required user-defined metadata. Lock evaluation can disable the toggle, reduce opacity, show a lock icon, recolor the title, and replace/augment the status with availability information.

When saving a new card, the base row is created before dependent rows. When editing, the object and dependent collections are upserted transactionally. When removing:

- if no transactional rows exist, dependent common data and the base card are physically deleted;
- if activity, repetitions, transactions, tracker values, or other protected history exists, the subtype `Status` becomes `Archived`; the base identity and history remain but normal card reads exclude it;
- metadata image folders are removed only on true deletion.

## 6. Time-At-Task cards

### 6.1 Card rendering and actions

A TAT card shows title, optional lock icon, selected value-rate name when a non-base rate is in use, activity toggle, status/lock status, tags, active time in the selected range, and calculated points in the range. Active time is orange. Value color follows the sign of value-per-minute. The toggle background represents active state and sign.

TAT value is the sum of each overlapping activity interval’s minutes multiplied by the value-per-minute captured on that interval, with user multiplier intervals applied by the global calculation layer. Historical intervals therefore do not change when the card’s current base rate is edited.

### 6.2 Details form

- Title: editable text.
- Close: opens Cancel plus Delete or Archive, depending on whether history exists. Choosing only Cancel leaves the form open.
- Status: read-only subtype status.
- Tags: read-only entry plus edit and clear buttons.
- Value Per Minute: invariant-culture numeric text. Invalid text saves as zero. A sign button toggles positive/negative; saving forces the base rate and every alternate rate to that sign.
- Current Accrued Value: read-only live value for the current range.
- Active Time: read-only live duration. Target button edits optional target active time; its icon becomes yellow when a target exists. Edit button opens the activity interval editor.
- Value Rates: shown only when the Value Rates feature is enabled. Add creates a row with blank name and zero rate. Each row has rate name, numeric rate, and delete. Rate rows are persisted in `TatCardValueRate`; activity creation captures the selected rate’s name/value.
- Schedule: shown when Schedules is enabled. Summary reports count; button opens the schedule list. A new unsaved card must be saved first.
- Locks: shown when Locks is enabled. Summary reports count; button opens the lock editor. A new unsaved card must be saved first.
- Metadata Fields: opens the per-card UDMD configuration; the card must already have a `CardID`.
- Description: multiline free text.
- Save: applies all fields, replaces the in-memory value-rate collection, persists through the parent workflow, and returns.

## 7. Step-Completion cards

### 7.1 Card rendering and actions

An SC card shows title, lock state, status, tags, active time, calculated step value, and the activity toggle. If it contains exactly one step, the card also shows that step’s repetition count and a green plus button. Tapping plus records one repetition at the current instant; locked cards disable it. Tapping the body opens details.

SC points in a range are the sum of stored `ScCardStepRep.StepValue` for repetitions whose timestamps fall in the range. The stored repetition value is a snapshot, protecting historical points from later edits to the step. The card’s sign is represented by `ScCard.ValuePerMinute` as `+1` or `-1`; saving applies the selected sign to value computation.

### 7.2 Details form

- Title, read-only Status, Tags with edit/clear, and Description behave like TAT.
- Current Accrued Value is live and color-coded. The adjacent sign button toggles the whole SC card between positive and negative.
- Active Time has target and interval-edit buttons.
- Schedule summary/button and Metadata Fields behave as described above.
- Steps: Add creates a step at the end with default value 1.0. Each row contains order, title, numeric value, count in the current range, decrement, and increment.
- Increment appends a repetition at the current UTC instant with the current step value. Decrement removes the most recent applicable repetition. Counts refresh immediately.
- Save renumbers steps sequentially from 1, preserves existing IDs and repetition collections, refreshes achievement evaluators, evaluates newly unlocked achievements, persists, and returns.
- Close offers Delete/Archive. There is no independent remove-step button in this form; step list persistence follows the saved collection.

## 8. Mission cards

### 8.1 Mission semantics

Subtypes are stored by name: `Stable`, `Degrade`, `Rot`.

- Stable awards `Value` once when completed within the queried range.
- Degrade starts at full Value at Available From and falls linearly to zero at Due By; completion awards the nonnegative value at completion time.
- Rot follows the same linear slope but may go below zero. Once overdue it continuously contributes a negative stream until completion or the current time.
- Failing a mission marks it complete/failed and contributes `-Value`.
- `ValuePerMinute` adds activity-derived value independently of the prize.
- Pending means current local time is before Available From. Pending cards display the availability window and cannot be completed.

### 8.2 Card rendering and actions

The card displays title, subtype badge/color, lock, activity toggle, Complete checkmark, status or Pending, tags, due-in text/color, active time, current prize/value-per-minute contribution, and estimated-versus-active completion percentage. The complete button is enabled only when available, not completed, and not locked. The activity toggle records time exactly as other active cards do.

Completing records a UTC `CompletedDate`, changes status to Complete, closes active activity if needed, triggers recalculation/achievements, and may prompt to share the change with a previously shared recipient.

### 8.3 Details form

- Title: editable unless complete.
- Share: visible for a saved mission. Creates a portable mission share package/text and invokes the platform share sheet. Save-before-share is enforced.
- Close: action sheet contains Delete and either Failed or Restore. Failed marks completion as failed; Restore returns a failed/completed mission to In-Progress and clears completion date.
- Status, Created Date, and Completed Date are read-only. Completed Date appears only for completed missions.
- Tags have edit/clear buttons.
- SubType picker: Stable, Degrade, Rot.
- Value and Value Per Minute: invariant numeric text.
- Event Date: checkbox controls whether a nullable event date is stored; date and time pickers are enabled only when checked.
- Available From and Due By: local wall-clock date/time pairs. If Available From is moved beyond Due By, the form automatically moves Due By to 224 hours after Available From. Save rejects Due By earlier than Available From.
- Estimated Time: required duration, edited through a duration picker; zero is rejected.
- Active Time: live display, target button, and interval editor.
- Resources: capture photo, select multiple images, select multiple files, display count, view/open resources, and clear with confirmation. New selections are staged and copied into the mission resource folder on Save. Clearing removes saved files.
- Description: multiline notes.
- Save: requires nonempty title, nonzero estimated time, valid numeric Value/VPM, and valid date ordering. It preserves creation/completion state, writes resources, detects changes, saves, optionally asks whether to share an update, and returns. Completed missions are read-only and do not show Save.

Mission import is a separate page. It previews incoming shared mission data, allows importing/saving it as a local mission, and avoids reusing local database IDs; `MissionGuid` is the cross-device identity.

## 9. Budget cards

### 9.1 Budget calculations and card

Daily scheduled top-ups repeat at their time of day from `StartDate` onward. At time `t`:

`balance = InitialBalance + all due daily top-ups - all Spend and CashIn currency amounts`

`remaining global value = balance × current ExchangeRate`

Cash In additionally contributes `currency amount × exchange rate` to global value in the transaction’s range. A budget’s global contribution is cashed-in value plus remaining global value only when the remaining value is negative.

The card shows title; Spend (red minus); optional Cash In (green currency symbol, feature-controlled); status; tags; a progress bar; percent remaining; remaining value in global units and named currency; cashed-in value; and next top-up countdown/amount. Tapping the body opens details.

Spend/Cash In opens an amount popup. The popup accepts an arithmetic expression and includes a calculator action. If UDMD fields exist, it also presents their controls. The amount must be a valid positive number. Saving inserts the transaction and its metadata atomically. Both transaction types subtract currency; only Cash In creates global value.

### 9.2 Details form

- Title, read-only Status, Tags (plain editable entry in this form), Currency, and Description.
- Exchange Rate: invariant numeric, rounded to three decimal places; invalid becomes zero.
- Scheduled Top-Ups: rows contain Amount and Time. Add creates a zero-amount row at 07:00; remove deletes a row; valid rows are sorted by time on save and invalid amount rows are silently ignored.
- Transaction Log: lists every transaction with amount and timestamp. Tapping amount/type/time (platform handlers) edits them; info opens UDMD metadata when present; delete removes the working row. Save commits inserts/updates/deletes, then returns the edited list to Budget Details.
- Metadata Fields: configures transaction metadata for the budget card.
- Start Date and Start Time: combined as a local schedule definition.
- Balance: initial balance; invalid input becomes zero.
- Save applies fields and top-ups and returns. Close offers Delete/Archive.

Transaction metadata viewer displays field/value rows and can open an Image field from the card’s image metadata folder.

## 10. Achievements and trophies

### 10.1 Achievement list and cards

The dedicated Achievements page lists all achievements, opens the Trophy Room, toggles reorder mode, and adds achievements. Up/down controls persist `Card.DisplayOrder`. The home achievement pane contains pinned/challenge items.

Cards render title; lock/completed/failed marker; difficulty badge; status; secondary descriptor; optional lock-progress bar; target progress bar; active-time or current-value text; target text; and completion/range/deadline text. Tapping opens details.

Difficulty names are `Easy`, `Medium`, `Hard`, `Ridiculous`, `Special`. Target types are `ActiveTime`, `Value`, `Steps`, `Achievements`, `Custom`. Completion types are `Range` and `Deadline`; range units are `Minutes`, `Hours`, `Days`, `Weeks`, `Months`.

Deadline achievements have a start/end window and finalize exactly once. On finalization, `FinalizedAt` and `FrozenCurrentValue` preserve the result. Completed or failed finalized deadline achievements become read-only. Range achievements can earn repeatedly by range and store multiple trophy instances.

### 10.2 Achievement details

- Title, Pin checkbox, read-only status, and validation/read-only messages.
- Close: read-only finalized items simply close; editable items offer Delete.
- Tags: multi-select edit and clear.
- Target Type and Difficulty pickers.
- ActiveTime target: duration editor (`hh:mm:ss`).
- Value target: positive number.
- Steps target: SC step picker plus positive target; if no steps exist, show an instructional empty message.
- Achievements target: multi-select achievement title plus positive target; empty message if none exist.
- Custom target: report picker plus positive target. The current build seeds placeholder report choices “Report 1” and “Report 2”; a port should connect this to saved reports without altering stored target fields.
- Completion Type.
- Range: unit plus positive whole-number amount.
- Deadline: start and end local date/time. Start may not be later than end.
- Trophies: add photo, add arbitrary file, count saved and staged files, view trophies, clear. Range accepts multiple staged files; Deadline keeps one staged trophy.
- Save validates range amount, target value, and deadline ordering, writes the card, then copies trophy files.

Trophy Room is a grid of earned trophies showing image, title, and earned date. Open leads to a full viewer with Save As, Share, and Delete. Trophy files live outside SQLite; `AchievementTrophy.ImageSource` is the path/source reference.

## 11. Arcs: value and event trackers

Both tracker cards render title, a green plus button, sparkline, average, period, latest value, and trend arrow. Tap plus adds at the current time; long press adds at a selected time. If metadata is configured, entry first prompts for it. Tap the body for details.

### 11.1 Value Tracker details

- Title (required), Unit, Start date.
- Initial values: comma/newline-separated invariant numbers. Invalid tokens are ignored. Parsed values replace/set the initial series using the model’s scheduling semantics.
- A record-only schedule row (“Every N [unit] from start date”) is present in XAML; the authoritative persisted fields are `ScheduleEvery` and `ScheduleUnit` (`Week` default). The page also has the general Card Schedule summary/editor.
- Metadata Fields and read-only recorded metadata history (`timestamp: field: value`).
- Delete/Archive button, Cancel, OK. Save trims title/unit and rejects blank title.

### 11.2 Event Tracker details

- Title (required), Start date.
- Aggregate period picker: Day, Week, Month, Year; required.
- Initial events: comma/newline-separated local date-times. Parseable values are sorted; invalid tokens are ignored.
- Metadata Fields and recorded metadata history.
- Delete/Archive, Cancel, OK.

Event tracker plus records an event value (normally 1) at the selected instant. Card totals/sparkline aggregate `TrackerValue` rows into the configured periods.

## 12. Goals

Goals apply only to Main Quest active cards (TAT and SC). Goal configuration has a Daily/Weekly/Monthly picker and one progress row per eligible card. A row is selectable to open Edit Goal and includes an enable checkbox.

Edit Goal contains:

- Goal Value: for TAT this is target hours; for SC it is target points.
- Use De Facto Times: when enabled, expected progress is calculated only across a custom daily start/end window.
- De Facto Start and End time pickers.
- Done returns the edited row.

Save writes only rows with target greater than zero. A de-facto row is saved only when start is before end. The unique database identity is `(CardID, TimeScope)`. The home Goals pane shows enabled goals as a custom progress card: title, total target, current overlay, expected-by-now marker, and TAT projected points/percentage or SC point target. Daily starts at local midnight; weekly starts Monday; monthly starts on day one.

## 13. Dashboard shortcuts

The Dashboard displays groups ordered by `ShortcutGroupOrder`, with shortcuts within each group ordered by `ShortcutOrder`. Layout is four columns; placeholders maintain grid geometry but are hidden. A tap navigates to and centers the target card/goal/achievement. Long press opens Shortcut Details.

Shortcut Details fields:

- Icon (maximum four characters).
- Target Type: MainQuest, Mission, Budget, Achievement, Arc, Goal.
- Target Card filtered by selected type; empty-state text if none.
- Shortcut Order integer.
- Group name selected/edited through a group picker.
- Group Order integer.
- Group Color with preview/hex and palette: black, DodgerBlue, green, orange, red, purple, teal, gray, stored as `#AARRGGBB`.
- Error message and Done.

Group names are unique case according to the SQLite unique index. Saving creates or updates the group and shortcut. Closing offers deletion for an existing shortcut. A shortcut does not have an FK to `Card`; ports must tolerate a target whose card was archived/deleted and omit or disable it.

Watch configuration lists dashboard shortcuts eligible for Wear OS: the target must be an actionable card and must not have required metadata. Switches select an ordered set of card IDs; Save serializes it to the `WatchShortcutCardIds` setting. Refresh reloads eligibility.

## 14. Schedules, notifications, and locks

### 14.1 Card schedules

Schedule list shows summary and date range with Edit/Delete, Add Schedule, and Done. Schedule Edit fields are Frequency, conditional Every N, Start date/time, Has end date plus end date/time, Enabled, optional Note, live Preview, validation error, Cancel, Save.

`FrequencyType` is stored as integer ordinal:

0 Once; 1 EveryDays; 2–8 EveryMonday through EverySunday; 9 EveryWeekday; 10 EveryWeeks; 11 EveryMonths; 12 EveryYears.

Once ignores frequency value and end. Weekday-specific patterns ignore frequency value. EveryWeeks uses the start weekday/time; EveryMonths uses its day/time; EveryYears uses month/day/time. Recurring definitions are local wall-clock values. The occurrence calculator resolves them, and the notification coordinator creates platform alarms and log rows.

Notification log statuses are exactly `Created`, `Scheduled`, `Sent`, `Missed`, `Missed (seen)`. The log screen has status tabs with badges, Refresh, paged infinite loading, and cards showing title, status, note, CreatedAt, ScheduledAt, ScheduleFor, SentAt, and error. Opening the missed log marks visible missed rows as seen. Android presents alarms/notifications and supports navigation back to the active target card; other configured platforms may use no-op presenters.

### 14.2 Locks

A card may have ordered locks. Each lock contains:

- Lock number/order.
- One local time window (start/end).
- Zero or more lock schedules, edited with the same schedule editor.
- Zero or more task dependencies.

The editor can add/remove locks, add/edit/remove schedules, edit the time window, and add/edit/remove dependencies, then Cancel or Save.

Dependency fields are Task card, Metric (`ActiveTime`=0, `Points`=1), TimeScope (`Daily`=0, `Weekly`=1, `Monthly`=2), numeric Target, and Condition (`MustBeGreaterThan`=0 or `MustBeLessThan`=1). A lock is active when its schedule/time window applies and dependencies do not satisfy the unlocking condition. Lock UI exposes the next available time where possible.

## 15. User-defined metadata (UDMD)

Metadata is configured per card but captured against a transaction entity. Supported entity types are exactly `Activity`, `BudgetTransaction`, and `TrackerValue`.

Metadata configuration supports Add Field, Save All, per-field Save Field, and Deactivate. Each field has Field Name, Field Type, Required, Display Order, and Active state. Types are:

- Text: free entry.
- Dropdown: picker backed by active ordered dropdown values; configuration accepts newline-separated choices.
- Number: numeric entry, normalized to invariant `G17` text.
- Date: date picker, stored as normalized date/local date-time text.
- Boolean: switch, stored canonically as true/false text.
- Image: Capture and Pick buttons plus filename; file is copied to the card image folder and only the safe filename is stored.

Field names are unique per card. Dropdown values are unique per config. Removing a used definition deactivates it rather than destroying transaction meaning. Save enforces required fields, type validity, active definition, allowed dropdown membership, safe image names, file existence, and existence of the referenced activity/transaction/value.

An activity start prompt can combine Value Rate selection with metadata. Budget and tracker entry prompts can combine amount/value/time and metadata. Cancel deletes images staged during that prompt; successful save retains them.

### 15.1 Shared editors

- Multi-select picker: searchable available values with per-row Add, a selected-text entry, Clear, Cancel, and OK. It supports read-only selected text for tag/achievement pickers while still modifying through Add/Clear.
- Duration picker: hours, minutes, and seconds controls; returns a nonnegative duration or cancellation. Hours may exceed 23 for elapsed durations.
- Date/time picker sheet: reusable modal for choosing an instant in local UI time, with Cancel/confirm semantics.
- Activity interval editor: shows start, end (including an open interval), calculated hours, metadata summary, and delete for each row. Save validates ordering/overlaps, applies inserts/updates/deletes, and preserves metadata for retained IDs.
- Time-window editor: Start and End local times plus Done.
- Task-dependency editor: Task, Metric, TimeScope, Target, Condition, Done; it disables/annotates task selection if no saved eligible card exists.

## 16. Leaderboard and Planner popup

Tapping global value opens a popup with Leaderboard and Planner tabs.

Leaderboard is for the current local day. Each active-card row shows title, hours today, percent of tracked time, percent of the day, and points today. Headers are tappable sort controls and indicate direction. “Dead Air” represents elapsed day time not covered by tracked activity. Summary shows refresh time and tracked totals.

Planner provides previous/next date, date picker, Today, zoom out/in/Fit, summary, Add Task, and Add Event. The timeline has Tasks and Events lanes, each split into Planned and Actual, with hour guides.

- Planned Task: eligible TAT/SC/Mission card, planned start, planned end. Tasks on a day may not overlap.
- Planned Event: SC step repetition with count, Mission complete, or Mission fail, at a local planned time.
- Existing planned items can be edited or deleted.
- Actual task slices derive from Activity intervals. Actual events derive from SC repetitions and mission completion/failure.
- Match statuses are Planned, FullMatch, PartialMatch, Missing, UnplannedActual, shown with distinct colors (green/orange/red/blue for the principal compared states).

Each local date has at most one `Planner` row. Saving the day replaces/upserts its ordered task/event set transactionally.

## 17. Reports, multipliers, settings, backup, and support screens

### 17.1 Reports

Reports page lists title-only report cards and Add. Details has Title, SQL editor, Execute, Copy Results, read-only results/status editor, Save, and Delete.

Only one `SELECT` or `WITH` statement is accepted. A second statement after a semicolon is rejected, with quoted text/comments parsed so internal semicolons are safe. Execution is read-only, limited to 500 rows, and interrupted after the positive `ReportQueryTimeoutMilliseconds` setting (default 5000 ms). Rows are represented with pipe-separated cells in the VM, displayed in a generated grid, and copied as correctly escaped CSV. Save updates `LastRunOn` to UTC. Report titles are unique.

### 17.2 Multipliers and Hard Mode

Hard Mode switch enables idle penalty. The numeric magnitude is always persisted as a negative points-per-minute value. While no activity is open, the service opens one `HardModePenaltyInterval`; activation closes it. Historical intervals snapshot their VPM.

Custom Multiplier creation fields are Name, Code (max three), Description, and Multiply By (>0). Existing rows are editable and have active switch, validation, Save, and Delete. Codes are case-insensitively unique. Only one activation interval may be open. Turning one on closes any other; interval rows snapshot name/code/description/factor so history survives later edits/deletion. Home shows the active code. Only TAT and Mission values are multiplied.

### 17.3 Settings menu

The Settings menu opens Database, Multipliers, Modules & Features, Watch App Config, Defaults and Misc, Notifications Log, and Tutorial.

Modules & Features has enabled switches and integer screen orders for all seven panes, plus feature switches for Locks, Schedules, Value Rates, and Cash In. On Android it also shows Dead Air Notification and its dependent Dead Air Alert Noise switch; both default off. Dead Air Alert Noise cannot be turned on unless Dead Air Notification is on, and turning the parent off clears the child. The page-level Save button is the commit point. Save is disabled until settings finish loading and while another save is running. Save requires every order to be a whole number.

When Dead Air Notification is enabled and no activity is open, Android keeps the existing low-importance ongoing notification visible with the title `Dead Air` and a live `HH:mm:ss` elapsed timer for the current uninterrupted Dead Air interval. Hours are total hours and may exceed 24. The interval begins at the most recent closed activity end, or at current local midnight when no activity exists; future or invalid start times are clamped to the current instant. An active-card notification always takes precedence. Dead Air notification state is presentation-only: it creates no activity, changes no value or penalty, affects no achievement or target, and does not alter watch state. The setting is hidden and behaviorally inert on non-Android platforms.

When Dead Air Alert Noise is saved on, the uninterrupted Dead Air interval produces a 250 ms alert at 30 seconds, a 750 ms alert at 45 seconds, and from 60 seconds onward a continuous cycle of 750 ms alert followed by 250 ms silence. Thresholds use greater-than-or-equal comparisons. If observation jumps across thresholds, only the highest newly applicable alert runs. Newly enabling the option or restoring notification access after 30 or 45 seconds does not replay missed one-shot alerts; doing so at or after 60 seconds starts the continuous cycle. Android redelivery of an already armed, matching Dead Air interval restores its durable handled markers and may emit only the highest currently due unhandled pre-60-second cue. Reapplying the same Dead Air start does not duplicate alerts, while a new start resets alert state. Re-enabling before 60 seconds waits for the next future threshold, and re-enabling at or after 60 seconds resumes the continuous cycle. Each service interval anchors the initial UTC-derived duration to Android's monotonic elapsed-real-time clock, and queued observations are coalesced and evaluated using the current elapsed time.

Dead Air alert audio is allowed only while the app's Android notification permission, app-level notifications, Active card notification channel, and ongoing foreground notification are available. It uses Android's alarm audio usage and cooperates with audio focus; focus loss pauses or stops playback and a continuing alert resumes after focus is regained. If notification access is blocked externally, audio stops without clearing the saved preference. The settings page prevents an off-to-on change while blocked, but a previously saved on switch remains removable and is shown with a paused explanation and a link to Android notification settings. Restoring visibility makes the saved preference effective again. Saving Dead Air Alert Noise off stops continuous audio promptly while leaving the Dead Air notification present; disabling the parent, starting an active card, receiving invalid state, or destroying the foreground service also stops it. The foreground service supports the app being backgrounded or removed from Recents while Android keeps the service alive; this feature adds no device-reboot, force-stop, or user-stopped-service restoration behavior.

Defaults and Misc has Username and Mission defaults: Tags; SubType; Value; VPM; Event date offset (Today + N days); Event time; Event checked; Available From offset/time; Due By offset/time; Estimated Time. Blank optional defaults remain unset. Numeric, integer offset, 24-hour time, and duration validation messages are independent. These values are applied only to newly created missions.

Tutorial is a programmatic scrollable guide with screenshots and explanations for TAT, SC, Missions, Budgets, Achievements, and related concepts.

### 17.4 Database maintenance and backup

Wipe requires typing exactly `Wipe db`. The current wipe script clears notification logs, shortcuts, multiplier activation/history, multipliers, and Hard Mode intervals; it does not generically drop the schema.

Manual Export asks which resources to include and a destination. Available resource keys are:

- `database` → `database/points.db3`;
- `achievement_trophies` → `folders/trophies`;
- `mission_resources` → `folders/resources`;
- `image_metadata` → `folders/ImageMetadata`.

The result is a version-1 `PointsBackup` ZIP with `manifest.json`. Import accepts the ZIP and legacy standalone database files, previews available resources, asks which to restore, confirms replacement, closes database connections, replaces selected resources, and reinitializes. Android folder import is intentionally unsupported because Storage Access Framework folder grants cannot enumerate arbitrary backup layouts; import the ZIP instead.

Automatic Export configuration selects resources, uses the common Schedule editor, chooses Device Storage or feature-flagged Google Drive, and asks for positive retention count. The summary displays status, next run/error, resources, schedule, destination, retention, and last run. Enable/Disable, History, and conditional Google Drive Reconnect are provided. Config is JSON; history is JSON Lines, not SQLite. Runs retain only the newest configured number of backups.

## 18. SQLite compatibility specification

### 18.1 General rules

Database file name is `points.db3`. Enable `PRAGMA foreign_keys=ON` on every connection. SQLite uses dynamic types, but a port must bind the declared logical type shown below. IDs must be preserved exactly during import. Do not renumber base or subtype identifiers.

Boolean values are INTEGER 0/1. Enums are stored either as their case-sensitive names in TEXT columns or ordinal integers where stated. Empty strings and NULL are semantically different; preserve both. Tables without declared foreign keys still have logical relationships and must be cleaned by service logic.

### 18.2 Tables and fields

#### Core cards

`Card(CardID INTEGER PK, DisplayOrder INTEGER NOT NULL DEFAULT 0, Title TEXT NOT NULL DEFAULT '', Tags TEXT NOT NULL DEFAULT '')`.

Subtype relationship is logical one-to-one via `CardID`; subtype primary keys are independent and must also be preserved.

#### TAT and activity

- `TatCard(TatCardID PK, CardID NOT NULL FK Card ON DELETE CASCADE, ValuePerMinute REAL NOT NULL, Status TEXT NOT NULL DEFAULT '', Description TEXT NOT NULL DEFAULT '', TargetActiveTimeSeconds INTEGER NULL)`.
- `TatCardValueRate(TatCardValueRateID PK, TatCardID NOT NULL FK TatCard ON DELETE CASCADE, RateName TEXT NOT NULL DEFAULT '', ValuePerMinute REAL NOT NULL)`.
- `Activity(ActivityID PK, CardID NOT NULL FK Card ON DELETE CASCADE, Start TEXT NOT NULL, End TEXT NULL, ValueRateName TEXT NOT NULL, ValuePerMinute REAL NOT NULL, CHECK End IS NULL OR Start < End)`.
- Unique partial index permits at most one row globally where `End IS NULL`.

#### SC

- `ScCard(ScCardID PK, CardID NOT NULL FK Card ON DELETE CASCADE, Status TEXT NOT NULL DEFAULT '', Description TEXT NOT NULL DEFAULT '')`.
- `ScCardStep(ScCardStepID PK, ScCardID NOT NULL FK ScCard ON DELETE CASCADE, SortOrder INTEGER NOT NULL, Title TEXT NOT NULL DEFAULT '', StepValue REAL NOT NULL)`.
- `ScCardStepRep(ScCardStepID NOT NULL FK ScCardStep ON DELETE CASCADE, TimeStamp TEXT NOT NULL, StepValue REAL NOT NULL, PRIMARY KEY(ScCardStepID, TimeStamp))`. Same-step repetitions cannot share the exact serialized timestamp.

#### Missions

`MissionCard(MissionCardID PK, CardID NOT NULL FK Card ON DELETE CASCADE, MissionGuid TEXT NOT NULL DEFAULT '', Status TEXT NOT NULL DEFAULT '', Description TEXT NOT NULL DEFAULT '', SharedWith TEXT NULL, SubType TEXT NOT NULL DEFAULT '', Value REAL NOT NULL, CreatedDate TEXT NOT NULL, AvailableFromDate TEXT NOT NULL, DueDate TEXT NOT NULL, CompletedDate TEXT NULL, EventDate TEXT NULL, EstCompletionTimeText TEXT NOT NULL DEFAULT '', IsFailed INTEGER NOT NULL DEFAULT 0, ValuePerMinute REAL NOT NULL)`.

`EstCompletionTimeText` is a duration string, normally total-hours `H:mm:ss`; do not reinterpret it as a date.

`MissionGuid` has a unique index installed by the startup migration. Older databases receive the column as nullable, blank/null values are backfilled with GUIDs in canonical `D` form, and then uniqueness is enforced. Treat it as required in all new writes even if an upgraded database reports nullable DDL.

#### Budgets

- `BudgetCard(BudgetCardID PK, CardID NOT NULL FK Card ON DELETE CASCADE, Status TEXT NOT NULL DEFAULT '', Description TEXT NOT NULL DEFAULT '', Currency TEXT NOT NULL DEFAULT '', ExchangeRate REAL NOT NULL, StartDate TEXT NOT NULL, InitialBalance REAL NOT NULL)`.
- `BudgetCardScheduledTopUp(BudgetCardScheduledTopUpID PK, BudgetCardID NOT NULL FK BudgetCard ON DELETE CASCADE, Amount REAL NOT NULL, TimeOfDaySeconds INTEGER NOT NULL)`; seconds since local midnight.
- `BudgetCardTransaction(BudgetCardTransactionID PK, BudgetCardID NOT NULL FK BudgetCard ON DELETE CASCADE, Amount REAL NOT NULL, Type TEXT NOT NULL DEFAULT '', TimeStamp TEXT NOT NULL)`. Type is `Spend` or `CashIn`. Global cash-in amount is derived at read time as `Amount × current BudgetCard.ExchangeRate`; it is not stored.

#### Achievements

- `AchievementCard(AchievementCardID PK, CardID NOT NULL FK Card ON DELETE CASCADE, Status TEXT NOT NULL DEFAULT '', Description TEXT NOT NULL DEFAULT '', TargetType TEXT NOT NULL DEFAULT '', DifficultyLevel TEXT NOT NULL DEFAULT 'Easy', CreatedDate TEXT NOT NULL, LastEarnedAt TEXT NULL, TargetActiveTimeInSeconds INTEGER NULL, TargetValue INTEGER NULL, ScCardStepID INTEGER NULL FK ScCardStep ON DELETE SET NULL, CompletionType TEXT NOT NULL DEFAULT 'Range', RangeUnit TEXT NULL, RangeAmount INTEGER NULL, DeadlineStart TEXT NULL, Deadline TEXT NULL, FinalizedAt TEXT NULL, FrozenCurrentValue REAL NULL, TrophyURLs TEXT NOT NULL DEFAULT '', IsPinned INTEGER NOT NULL DEFAULT 0)`.
- `AchievementTrophy(TrophyID PK, AchievementCardID NOT NULL FK AchievementCard ON DELETE CASCADE, Title TEXT NOT NULL DEFAULT '', EarnedOn TEXT NOT NULL, ImageSource TEXT NOT NULL DEFAULT '')`.

Some target-specific UI values (such as selected achievement title/custom report name) are represented by the current model but have no dedicated DDL column. A compatible implementation must not fabricate columns in the reusable v1 database. If extending the schema, add a migration and retain old-reader behavior.

#### Multipliers and Hard Mode

- `HardModePenaltyInterval(HardModePenaltyIntervalID PK, Start TEXT NOT NULL, End TEXT NULL, ValuePerMinute REAL NOT NULL, CHECK End IS NULL OR Start <= End)`; unique partial index on the one open row.
- `UserMultiplier(UserMultiplierID INTEGER PK AUTOINCREMENT, Name, Code, Description TEXT NOT NULL, MultiplyBy REAL NOT NULL DEFAULT 1.0, CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NOT NULL, CHECK length(Code)<=3, CHECK MultiplyBy>0)`; unique `Code COLLATE NOCASE`.
- `UserMultiplierActivationInterval(UserMultiplierActivationIntervalID INTEGER PK AUTOINCREMENT, UserMultiplierID INTEGER NULL FK UserMultiplier ON DELETE SET NULL, Name, Code, Description TEXT NOT NULL, MultiplyBy REAL NOT NULL, Start TEXT NOT NULL, End TEXT NULL, same code/factor/end checks)`; one open row globally.

#### Trackers and metadata

- `ValueTrackerCard(ValueTrackerCardID PK, CardID NOT NULL FK Card ON DELETE CASCADE, Status TEXT NOT NULL DEFAULT '', Unit TEXT NOT NULL DEFAULT '', CreatedDate TEXT NOT NULL, RangeStart TEXT NOT NULL, ScheduleEvery INTEGER NOT NULL DEFAULT 1, ScheduleUnit TEXT NOT NULL DEFAULT 'Week')`.
- `EventTrackerCard(EventTrackerCardID PK, CardID NOT NULL FK Card ON DELETE CASCADE, Status TEXT NOT NULL DEFAULT '', Unit TEXT NOT NULL DEFAULT '', CreatedDate TEXT NOT NULL, RangeStart TEXT NOT NULL, GroupByPeriod TEXT NOT NULL DEFAULT 'Day')`.
- `TrackerValue(TrackerValueID PK, CardID NOT NULL FK Card ON DELETE CASCADE, TimeStamp TEXT NOT NULL, Value REAL NOT NULL)`.
- `UdmdConfig(UdmdConfigID INTEGER PK AUTOINCREMENT, CardID NOT NULL FK Card ON DELETE CASCADE, FieldName TEXT NOT NULL, FieldType TEXT NOT NULL, IsRequired INTEGER NOT NULL DEFAULT 0, DisplayOrder INTEGER NOT NULL DEFAULT 0, IsActive INTEGER NOT NULL DEFAULT 1)`, unique `(CardID,FieldName)`.
- `UdmdDropdown(UdmdDropdownID INTEGER PK AUTOINCREMENT, UdmdConfigID NOT NULL FK UdmdConfig ON DELETE CASCADE, DropdownValue TEXT NOT NULL, DisplayOrder INTEGER NOT NULL DEFAULT 0, IsActive INTEGER NOT NULL DEFAULT 1)`, unique `(UdmdConfigID,DropdownValue)`.
- `UdmdTrans(UdmdTransID INTEGER PK AUTOINCREMENT, CardID NOT NULL FK Card ON DELETE CASCADE, UdmdConfigID NOT NULL FK UdmdConfig ON DELETE CASCADE, RelatedEntityType TEXT NOT NULL, RelatedEntityId INTEGER NOT NULL, FieldValue TEXT NOT NULL)`, unique `(RelatedEntityType,RelatedEntityId,UdmdConfigID)`.

#### Scheduling and notifications

- `CardSchedule(ScheduleId PK, CardId INTEGER NOT NULL, IsEnabled INTEGER NOT NULL DEFAULT 1, Note TEXT NOT NULL DEFAULT '', FrequencyType INTEGER NOT NULL, FrequencyValue INTEGER NOT NULL DEFAULT 0, FromDateTime TEXT NOT NULL, ToDateTime TEXT NULL)`. Logical Card relationship; no DDL FK.
- `NotificationLog(NotificationLogId INTEGER PK AUTOINCREMENT, ScheduleId, CardId INTEGER NOT NULL, CardTitle, Note TEXT NOT NULL DEFAULT '', Status TEXT NOT NULL DEFAULT 'Created', CreatedAt TEXT NOT NULL, ScheduledAt TEXT NULL, ScheduleFor TEXT NOT NULL, SentAt TEXT NULL, UpdatedAt TEXT NOT NULL, Error TEXT NULL)`. Unique `(ScheduleId,ScheduleFor)` and status CHECK enumerated above.

#### Goals and planner

- `Goal(GoalID PK, CardID NOT NULL FK Card ON DELETE CASCADE, TimeScope TEXT NOT NULL, GoalHrs REAL NOT NULL, Enabled INTEGER NOT NULL DEFAULT 0, DeFactoStart TEXT NULL, DeFactoEnd TEXT NULL)`, unique `(CardID,TimeScope)`.
- `Planner(PlannerID PK, PlannerDate TEXT NOT NULL UNIQUE, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL)`.
- `PlannerTask(PlannerTaskID PK, PlannerID NOT NULL FK Planner ON DELETE CASCADE, CardID NOT NULL FK Card ON DELETE CASCADE, CardKind TEXT NOT NULL, PlannedStart TEXT NOT NULL, PlannedEnd TEXT NOT NULL, CHECK PlannedStart < PlannedEnd)`.
- `PlannerEvent(PlannerEventID PK, PlannerID NOT NULL FK Planner ON DELETE CASCADE, EventKind TEXT NOT NULL, CardID NOT NULL FK Card ON DELETE CASCADE, ScCardStepID INTEGER NULL FK ScCardStep ON DELETE SET NULL, PlannedTime TEXT NOT NULL, PlannedCount INTEGER NOT NULL DEFAULT 1)`.

#### Locks

- `Lock(LockId INTEGER PK AUTOINCREMENT, LockNumber INTEGER NOT NULL, CardId INTEGER NOT NULL, TimeWindowStart TEXT NOT NULL, TimeWindowEnd TEXT NOT NULL)`.
- `LockSchedule(ScheduleId INTEGER PK AUTOINCREMENT, LockId INTEGER NOT NULL, FrequencyType INTEGER NOT NULL, FrequencyValue INTEGER NOT NULL DEFAULT 0, FromDateTime TEXT NOT NULL, ToDateTime TEXT NULL)`.
- `LockTaskDependency(LockTaskDependencyId INTEGER PK AUTOINCREMENT, LockId INTEGER NOT NULL, TaskDependencyCardId INTEGER NOT NULL, MetricType INTEGER NOT NULL DEFAULT 0, TimeScope INTEGER NOT NULL DEFAULT 0, TargetValue REAL NOT NULL DEFAULT 0, TargetValence INTEGER NOT NULL DEFAULT 0)`.

These are logical relationships without DDL FKs; preserve orphan-tolerant reads and explicit cleanup.

#### Reports, shortcuts, settings, migrations, watch

- `Report(Id INTEGER PK AUTOINCREMENT, Title TEXT NOT NULL UNIQUE, SQLQuery TEXT NOT NULL, LastRunOn TEXT NULL, EligibleForAchievment INTEGER NOT NULL DEFAULT 0)`; note the historical misspelling `Achievment` is part of the schema.
- `ShortcutGroup(ShortcutGroupId INTEGER PK AUTOINCREMENT, Name TEXT NOT NULL UNIQUE, Color TEXT NOT NULL DEFAULT '#FF000000', ShortcutGroupOrder INTEGER NOT NULL DEFAULT 0)`.
- `Shortcut(ShortcutId INTEGER PK AUTOINCREMENT, IconChar TEXT NOT NULL DEFAULT '', TargetCardId INTEGER NOT NULL, ShortcutGroupId INTEGER NOT NULL FK ShortcutGroup ON DELETE CASCADE, ShortcutOrder INTEGER NOT NULL DEFAULT 0)`.
- `Setting(SettingKey TEXT PK, SettingValue TEXT NOT NULL DEFAULT '', ValueType TEXT NOT NULL DEFAULT 'string', Category TEXT NOT NULL DEFAULT '', DisplayName TEXT NOT NULL DEFAULT '', Description TEXT NOT NULL DEFAULT '', IsUserEditable INTEGER NOT NULL DEFAULT 1, SortOrder INTEGER NOT NULL DEFAULT 0)`.
- `SchemaMigration(MigrationKey TEXT PK, AppliedAtUtc TEXT NOT NULL)`.
- `WatchProcessedEvent(EventId TEXT PK, BaseSnapshotId TEXT NOT NULL DEFAULT '', CreatedAtUtc TEXT NOT NULL DEFAULT '', ProcessedAtUtc TEXT NOT NULL DEFAULT '', Status TEXT NOT NULL DEFAULT '', Message TEXT NOT NULL DEFAULT '')`.

### 18.3 Built-in settings and defaults

Preserve unknown setting rows. Built-ins include:

- Multipliers: `HardModeEnabled=false`, `HardModeDamagePerMinuteValue=-0.2`, `StatusConditionsEnabled=false`, `CurrentlyAppliedStatusConditionId=''`.
- Appearance: `SelectedThemeId=''` (theme/status models exist but no corresponding v1 DDL tables; treat as reserved).
- Pane enabled/order pairs: Dashboard true/1, MainQuest true/2, Mission true/3, Budgets true/4, Achievements true/5, Arcs true/6, Goals true/7.
- Features: `LocksActive=true`, `SchedulesActive=true`, `ValueRatesActive=true`, `CashInActive=true`, `DeadAirNotificationEnabled=false`, `DeadAirAlertNoiseEnabled=false` (the final two are Android UI only).
- Watch: `WatchShortcutCardIds=[]` JSON.
- Defaults: `MissionType=true` (legacy), `ValueRatesValuePerMinute=1.0`, `AchievementNameRegex=^(?<name>.+?)(\s*\#(?<tags>.+))?$`.
- Defaults/Misc: `Username` and all `MissionDefault*` keys described in section 17, blank except Event checked=false.
- Database: `ReportQueryTimeoutMilliseconds=5000`.

`SettingValue` is always the canonical raw text. `ValueType` guides parsing (`string`, bool/int/nullable-int/double variants). A Kotlin port should not split typed values into new columns.

### 18.4 Required indexes and migration behavior

In addition to primary/unique constraints already stated, retain the creation script’s lookup indexes: subtype-by-CardID; TAT rates by TatCardID; SC steps by ScCardID and reps by timestamp; Mission by CardID/status/due date; Budget transactions by BudgetCardID/timestamp; Achievement by CardID/step and Trophy by achievement; Activity and Hard Mode by start/end; multiplier activation by start/end; tracker subtype/value by CardID and values by timestamp; UDMD transaction by CardID/config/related entity; schedules by CardId; notifications by status+ScheduleFor and ScheduleId; goals by CardID/enabled; planner children by planner/card/time; locks by card/number, schedule frequency/date range, and dependency card/scope; shortcuts by group/order and target; reports by title; watch events by processed time.

The following uniqueness rules are behaviorally significant and must be recreated exactly:

- one open Activity globally;
- one open Hard Mode penalty interval globally;
- one open user-multiplier activation globally;
- multiplier code case-insensitive uniqueness;
- MissionGuid uniqueness;
- one notification row per `(ScheduleId, ScheduleFor)`;
- one goal per `(CardID, TimeScope)`;
- one planner per local date;
- one UDMD field name per card, dropdown value per field, and metadata value per related entity/config;
- unique shortcut-group name and report title.

Exact index names expected by the current creation/migration path are:

`IX_TatCard_CardID`, `IX_TatCardValueRate_TatCardID`, `IX_ScCard_CardID`, `IX_ScCardStep_ScCardID`, `IX_ScCardStepRep_TimeStamp`, `IX_MissionCard_CardID`, `IX_MissionCard_Status`, `IX_MissionCard_DueDate`, `UX_MissionCard_MissionGuid`, `IX_BudgetCard_CardID`, `IX_BudgetTxn_BudgetCardID`, `IX_BudgetTxn_TimeStamp`, `IX_Achievement_CardID`, `IX_Achievement_ScCardStepID`, `IX_Trophy_AchievementID`, `IX_Activity_CardID`, `UX_Activity_OneOpen`, `IX_Activity_StartEnd`, `UX_HardModePenalty_OneOpen`, `IX_HardModePenalty_StartEnd`, `UX_UserMultiplier_Code`, `UX_UserMultiplierActivation_OneOpen`, `IX_UserMultiplierActivation_StartEnd`, `IX_ValueTracker_CardID`, `IX_EventTracker_CardID`, `IX_TrackerValue_CardID`, `IX_TrackerValue_TimeStamp`, `UX_UdmdConfig_CardID_FieldName`, `UX_UdmdDropdown_Config_Value`, `UX_UdmdTrans_Related_Config`, `IX_UdmdTrans_CardID`, `IX_UdmdTrans_UdmdConfigID`, `IX_UdmdTrans_Related`, `IX_CardSchedule_CardId`, `UX_NotificationLog_ScheduleOccurrence`, `IX_NotificationLog_StatusScheduleFor`, `IX_NotificationLog_ScheduleId`, `IX_Goal_CardID`, `IX_Goal_Enabled`, `UX_Planner_Date`, `IX_PlannerTask_PlannerID`, `IX_PlannerTask_CardID`, `IX_PlannerTask_StartEnd`, `IX_PlannerEvent_PlannerID`, `IX_PlannerEvent_CardID`, `IX_PlannerEvent_ScCardStepID`, `IX_PlannerEvent_PlannedTime`, `IX_Lock_CardId`, `IX_Lock_CardId_LockNumber`, `IX_LockSchedule_LockId`, `IX_LockSchedule_LockId_Frequency`, `IX_LockSchedule_DateRange`, `IX_LockTaskDependency_LockId`, `IX_LockTaskDependency_TaskCard`, `IX_LockTaskDependency_TaskCard_TimeScope`, `IX_ShortcutGroup_Order`, `IX_Shortcut_Group_Order`, `IX_Shortcut_TargetCardId`, `UX_ShortcutGroup_Name`, `UX_Report_Title`, and `IX_WatchProcessedEvent_ProcessedAtUtc`.

Startup is idempotent: run the full `CREATE TABLE/INDEX IF NOT EXISTS` script, add/rename legacy columns when missing, rebuild the old NotificationLog table when its status CHECK cannot accept `Missed (seen)`, backfill Mission GUIDs, and run the keyed time migration `2026-04-time-handling-normalization-v1` once. That migration normalizes known instant, local-date, local-time, and local-date-time columns in place and records completion in `SchemaMigration`. A replacement must recognize already-migrated databases and must not normalize them again as if their UTC text were local.

### 18.5 Time encoding contract

Never treat all TEXT times alike.

- UTC historical instants use ISO-8601 round-trip text with `Z` or explicit offset; new writes should be UTC, for example `2026-04-28T14:30:00.0000000Z`. This includes Activity/SC rep/TrackerValue/Budget transaction instants; created/completed/finalized/earned timestamps; notification log instants; multiplier and Hard Mode intervals; report last run; planner created/updated; watch event times.
- Local dates use `yyyy-MM-dd`: PlannerDate and date-only intent.
- Local times use `HH:mm:ss`: lock windows and goal de-facto times. Budget top-up time uses integer seconds from midnight.
- Local wall-clock date-times use `yyyy-MM-dd'T'HH:mm:ss.fffffff` without `Z`: card/lock schedules, mission availability/due/event, achievement deadline windows, budget start, planner planned times.
- Legacy rows may contain no-offset instants or offset-bearing wall-clock values. Compatible reads must emulate `LegacyTimeReader`: no-offset legacy instant values are interpreted in the device timezone then converted to UTC; offset-bearing schedule values preserve their written clock time and ignore the offset.
- For DST gaps, shift forward; for ambiguous fall-back times, choose the earlier instant, matching current defaults.

## 19. Non-database files and package transfer

The application data root contains:

- `db/points.db3`;
- `trophies/AchievementID_<achievement id>/...`;
- `resources/MissionID_<mission id>/...`;
- `ImageMetadata/CardID_<card id>/...`;
- `exports/` and `exports/scheduled/`;
- `backup_automation/backup_automation.json`;
- `backup_automation/backup_automation.log.jsonl`;
- optional `backup_automation/google_drive_oauth_client.json`; OAuth tokens themselves use secure platform storage.

Moving only SQLite loses trophy binaries, mission attachments, and metadata images. A lossless migration must transfer the database plus those three attachment trees, preserving filenames and ID-derived directory names. The standard ZIP export is the preferred interchange format.

## 20. Android/Kotlin reconstruction requirements

A native port may use Compose, Fragments, Room, repositories, clean architecture, or another layout, but database compatibility should use one of these strategies:

1. Open the existing SQLite file directly with exact table/column names and custom mappers; or
2. Use Room entities whose `tableName`, `columnInfo`, indices, foreign keys, and enum converters match this schema exactly, with migrations that never destructively recreate user tables.

Required implementation rules:

- Copy/import the DB while all connections are closed; run `PRAGMA foreign_key_check` and integrity checks after replacement.
- Do not let Room auto-generate a conflicting schema or rewrite TEXT enum/timestamp values.
- Use `Long` for every SQLite identifier even where current C# models use `int`.
- Use `Double` for REAL. Preserve NaN/infinity defensively even though UI entry normally prevents them.
- Centralize `Instant`, `LocalDate`, `LocalTime`, and `LocalDateTime` converters according to section 18.4.
- Preserve snapshot fields (`Activity.ValuePerMinute`, rep `StepValue`, multiplier interval details). Never recalculate historical snapshots from current definitions.
- Maintain the global unique-open constraints for Activity, Hard Mode, and multiplier activation inside transactions.
- Perform card plus dependent-row saves in a transaction; save a metadata-bearing activity/budget/tracker transaction and its `UdmdTrans` rows atomically.
- Keep archive-on-delete behavior and filter `Status='Archived'` from normal card lists.
- Use WorkManager/alarm APIs for scheduled backups and notifications, rescheduling after boot/timezone change.
- Store attachment files in app-private storage with the same logical folder naming, and include them in export/import.
- Treat report SQL as hostile input: read-only connection/query-only pragma where possible, one SELECT/CTE statement, row cap, timeout/cancellation.
- Add migration tests that load a real exported `points.db3`, read every table, round-trip without changes, and compare row counts, IDs, raw enum strings, timestamp strings, and attachment hashes.

## 21. Acceptance test checklist

A replacement is functionally equivalent when the following pass:

1. Import an existing backup and display every enabled pane in stored order with all cards in `DisplayOrder`.
2. Start TAT, switch to SC/Mission, stop, backdate via long press, and verify exactly one open Activity and unchanged historical rates.
3. Add/decrement SC reps and verify snapshot values and achievement refresh.
4. Complete/fail/restore each Mission subtype before/after due dates and match value calculations.
5. Run Budget top-ups across midnight, Spend/Cash In with metadata, edit the log, and match balance/global totals.
6. Finalize deadline and repeat range achievements; open/share/delete trophy files.
7. Record tracker values/events now and at selected times with every metadata type.
8. Configure schedules through every frequency, generate notifications, mark missed as seen, reboot, and verify rescheduling.
9. Configure locks with time, recurrence, and both dependency valences; verify UI and activation enforcement.
10. Set goals for all scopes/de-facto windows and compare current/expected markers.
11. Create, reorder, invoke, edit, and delete shortcuts; synchronize eligible Watch IDs.
12. Toggle Hard Mode and custom multiplier; compare interval history and global value.
13. Execute valid SELECT/CTE reports; reject writes/multiple statements; enforce cap and timeout; copy valid CSV.
14. Export all resources, import over a clean install, and compare SQLite rows plus attachment hashes.
15. Exercise DST gap/overlap, timezone change, legacy no-offset timestamps, month/year recurrence, and open-interval recovery.

## 22. Source-of-truth and known constraints

This specification is derived from the implemented XAML/code-behind, view models, services, models, SQL creation/migration scripts, and tests. Where a UI control and database differ, the database contract in section 18 is authoritative for transfer compatibility, while the workflow sections are authoritative for user behavior.

Notable constraints to retain or explicitly migrate:

- Several logical relationships intentionally lack SQLite foreign keys.
- Achievement target UI contains values not fully represented by v1 DDL.
- `Report.EligibleForAchievment` is misspelled in the existing schema.
- Budget cash-in historical global amount is derived using the budget’s current exchange rate, because the DB stores only currency amount/type/time.
- Date-header view exists but currently renders placeholder content and is not a functional card family.
- Google Drive storage UI and automatic-export visibility are feature-flag controlled.
- Status-condition/theme configuration models are reserved/legacy and have settings keys but no tables in the current creation script.

Any intentional correction to these constraints should be delivered as a versioned database migration plus an import compatibility layer, not as an implicit reinterpretation of existing rows.
