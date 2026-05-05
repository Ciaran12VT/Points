using Points.Global;
using Points.Models;
using Points.Services.Persistence;
using Points.Services.Sqlite;
using Points.Services.Time;

namespace Points.Services.Multipliers
{
    public sealed class SqliteUserMultiplierService : IUserMultiplierService
    {
        private const double Epsilon = 0.0000001d;

        private readonly ISqliteConnectionContext _context;

        public SqliteUserMultiplierService(ISqliteConnectionContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<UserMultiplierModel>> GetMultipliersAsync()
        {
            await _context.InitializeAsync();

            var active = await GetOpenIntervalRowAsync();
            var activeId = active?.UserMultiplierID;

            var rows = await _context.Db.QueryAsync<UserMultiplierRow>(
                @"
                SELECT UserMultiplierID, Name, Code, Description, MultiplyBy
                FROM UserMultiplier
                ORDER BY Name COLLATE NOCASE, UserMultiplierID;");

            return rows
                .Select(row =>
                {
                    var model = ToModel(row);
                    model.IsActive = activeId.HasValue && activeId.Value == model.Id;
                    return model;
                })
                .ToList();
        }

        public async Task<UserMultiplierModel?> GetActiveMultiplierAsync()
        {
            await _context.InitializeAsync();

            var open = await GetOpenIntervalRowAsync();
            var active = open == null
                ? null
                : new UserMultiplierModel
                {
                    Id = open.UserMultiplierID ?? 0,
                    Name = open.Name,
                    Code = open.Code,
                    Description = open.Description,
                    MultiplyBy = open.MultiplyBy,
                    IsActive = true
                };

            MultiplierRuntimeState.SetActive(active);
            return active;
        }

        public async Task<UserMultiplierModel> SaveMultiplierAsync(UserMultiplierModel multiplier, DateTime utcNow)
        {
            if (multiplier == null)
                throw new ArgumentNullException(nameof(multiplier));

            await _context.InitializeAsync();

            utcNow = StrictTimeSerializer.RequireUtcInstant(utcNow, nameof(utcNow));
            var normalized = Normalize(multiplier);
            await EnsureCodeIsUniqueAsync(normalized.Code, normalized.Id);

            var nowIso = StrictTimeSerializer.SerializeUtcInstant(utcNow);
            var wasActive = await IsMultiplierActiveAsync(normalized.Id);

            if (normalized.Id <= 0)
            {
                await _context.Db.ExecuteAsync(
                    @"
                    INSERT INTO UserMultiplier (Name, Code, Description, MultiplyBy, CreatedAtUtc, UpdatedAtUtc)
                    VALUES (?, ?, ?, ?, ?, ?);",
                    normalized.Name,
                    normalized.Code,
                    normalized.Description,
                    normalized.MultiplyBy,
                    nowIso,
                    nowIso);

                normalized.Id = await _context.Db.ExecuteScalarAsync<int>("SELECT last_insert_rowid();");
            }
            else
            {
                var existingCount = await _context.Db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM UserMultiplier WHERE UserMultiplierID = ?;",
                    normalized.Id);

                if (existingCount == 0)
                    throw new InvalidOperationException($"Multiplier '{normalized.Id}' does not exist.");

                await _context.Db.ExecuteAsync(
                    @"
                    UPDATE UserMultiplier
                    SET Name = ?,
                        Code = ?,
                        Description = ?,
                        MultiplyBy = ?,
                        UpdatedAtUtc = ?
                    WHERE UserMultiplierID = ?;",
                    normalized.Name,
                    normalized.Code,
                    normalized.Description,
                    normalized.MultiplyBy,
                    nowIso,
                    normalized.Id);
            }

            if (wasActive || multiplier.IsActive)
                await SetActiveMultiplierAsync(normalized.Id, utcNow);

            return normalized;
        }

        public async Task DeleteMultiplierAsync(int multiplierId, DateTime utcNow)
        {
            await _context.InitializeAsync();
            utcNow = StrictTimeSerializer.RequireUtcInstant(utcNow, nameof(utcNow));

            var wasActive = await IsMultiplierActiveAsync(multiplierId);
            if (wasActive)
                await SetActiveMultiplierAsync(null, utcNow);

            await _context.Db.ExecuteAsync(
                "DELETE FROM UserMultiplier WHERE UserMultiplierID = ?;",
                multiplierId);
        }

        public async Task SetActiveMultiplierAsync(int? multiplierId, DateTime utcNow)
        {
            await _context.InitializeAsync();
            utcNow = StrictTimeSerializer.RequireUtcInstant(utcNow, nameof(utcNow));

            if (!multiplierId.HasValue)
            {
                await CloseOpenIntervalAsync(utcNow);
                MultiplierRuntimeState.Clear();
                return;
            }

            var row = await GetMultiplierRowAsync(multiplierId.Value)
                ?? throw new InvalidOperationException($"Multiplier '{multiplierId.Value}' does not exist.");

            var nowIso = StrictTimeSerializer.SerializeUtcInstant(utcNow);

            await _context.RunInTransactionAsync(tran =>
            {
                var open = tran.Query<UserMultiplierActivationIntervalRow>(
                    @"
                    SELECT UserMultiplierActivationIntervalID, UserMultiplierID, Name, Code, Description, MultiplyBy, Start, ""End""
                    FROM UserMultiplierActivationInterval
                    WHERE ""End"" IS NULL
                    ORDER BY Start DESC
                    LIMIT 1;")
                    .FirstOrDefault();

                if (open != null && IsSameOpenInterval(open, row))
                    return;

                if (open != null)
                {
                    tran.Execute(
                        @"
                        UPDATE UserMultiplierActivationInterval
                        SET ""End"" = ?
                        WHERE UserMultiplierActivationIntervalID = ?;",
                        nowIso,
                        open.UserMultiplierActivationIntervalID);
                }

                tran.Execute(
                    @"
                    INSERT INTO UserMultiplierActivationInterval
                        (UserMultiplierID, Name, Code, Description, MultiplyBy, Start, ""End"")
                    VALUES (?, ?, ?, ?, ?, ?, NULL);",
                    row.UserMultiplierID,
                    row.Name,
                    row.Code,
                    row.Description,
                    row.MultiplyBy,
                    nowIso);
            });

            MultiplierRuntimeState.SetActive(ToModel(row, isActive: true));
        }

        private async Task<UserMultiplierRow?> GetMultiplierRowAsync(int multiplierId)
        {
            var rows = await _context.Db.QueryAsync<UserMultiplierRow>(
                @"
                SELECT UserMultiplierID, Name, Code, Description, MultiplyBy
                FROM UserMultiplier
                WHERE UserMultiplierID = ?;",
                multiplierId);

            return rows.FirstOrDefault();
        }

        private async Task<UserMultiplierActivationIntervalRow?> GetOpenIntervalRowAsync()
        {
            var rows = await _context.Db.QueryAsync<UserMultiplierActivationIntervalRow>(
                @"
                SELECT UserMultiplierActivationIntervalID, UserMultiplierID, Name, Code, Description, MultiplyBy, Start, ""End""
                FROM UserMultiplierActivationInterval
                WHERE ""End"" IS NULL
                ORDER BY Start DESC
                LIMIT 1;");

            return rows.FirstOrDefault();
        }

        private async Task<bool> IsMultiplierActiveAsync(int multiplierId)
        {
            if (multiplierId <= 0)
                return false;

            var activeId = await _context.Db.ExecuteScalarAsync<int?>(
                @"
                SELECT UserMultiplierID
                FROM UserMultiplierActivationInterval
                WHERE ""End"" IS NULL
                ORDER BY Start DESC
                LIMIT 1;");

            return activeId.HasValue && activeId.Value == multiplierId;
        }

        private async Task CloseOpenIntervalAsync(DateTime utcNow)
        {
            var nowIso = StrictTimeSerializer.SerializeUtcInstant(utcNow);

            await _context.Db.ExecuteAsync(
                @"
                UPDATE UserMultiplierActivationInterval
                SET ""End"" = ?
                WHERE ""End"" IS NULL;",
                nowIso);
        }

        private async Task EnsureCodeIsUniqueAsync(string code, int id)
        {
            var duplicateCount = await _context.Db.ExecuteScalarAsync<int>(
                @"
                SELECT COUNT(*)
                FROM UserMultiplier
                WHERE Code = ? COLLATE NOCASE
                  AND UserMultiplierID <> ?;",
                code,
                id);

            if (duplicateCount > 0)
                throw new InvalidOperationException($"A multiplier with code '{code}' already exists.");
        }

        private static UserMultiplierModel Normalize(UserMultiplierModel input)
        {
            var name = (input.Name ?? string.Empty).Trim();
            var code = (input.Code ?? string.Empty).Trim().ToUpperInvariant();
            var description = (input.Description ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Multiplier name is required.");

            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("Multiplier code is required.");

            if (code.Length > 3)
                throw new InvalidOperationException("Multiplier code must be 3 characters or fewer.");

            if (double.IsNaN(input.MultiplyBy) || double.IsInfinity(input.MultiplyBy) || input.MultiplyBy <= Epsilon)
                throw new InvalidOperationException("Multiply By must be greater than 0.");

            return new UserMultiplierModel
            {
                Id = input.Id,
                Name = name,
                Code = code,
                Description = description,
                MultiplyBy = input.MultiplyBy,
                IsActive = input.IsActive
            };
        }

        private static bool IsSameOpenInterval(UserMultiplierActivationIntervalRow open, UserMultiplierRow row)
        {
            return open.UserMultiplierID == row.UserMultiplierID
                && string.Equals(open.Name, row.Name, StringComparison.Ordinal)
                && string.Equals(open.Code, row.Code, StringComparison.Ordinal)
                && string.Equals(open.Description, row.Description, StringComparison.Ordinal)
                && Math.Abs(open.MultiplyBy - row.MultiplyBy) < Epsilon;
        }

        private static UserMultiplierModel ToModel(UserMultiplierRow row, bool isActive = false)
        {
            return new UserMultiplierModel
            {
                Id = row.UserMultiplierID,
                Name = row.Name,
                Code = row.Code,
                Description = row.Description,
                MultiplyBy = row.MultiplyBy,
                IsActive = isActive
            };
        }

        private sealed class UserMultiplierRow
        {
            public int UserMultiplierID { get; set; }
            public string Name { get; set; } = "";
            public string Code { get; set; } = "";
            public string Description { get; set; } = "";
            public double MultiplyBy { get; set; }
        }

        private sealed class UserMultiplierActivationIntervalRow
        {
            public int UserMultiplierActivationIntervalID { get; set; }
            public int? UserMultiplierID { get; set; }
            public string Name { get; set; } = "";
            public string Code { get; set; } = "";
            public string Description { get; set; } = "";
            public double MultiplyBy { get; set; }
            public string Start { get; set; } = "";
            public string? End { get; set; }
        }
    }
}
