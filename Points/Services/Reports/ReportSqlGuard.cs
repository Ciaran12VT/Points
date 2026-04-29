namespace Points.Services.Reports
{
    internal static class ReportSqlGuard
    {
        public const int DefaultMaxRows = 500;
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

        public static string ValidateSelectStatement(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return string.Empty;

            var trimmed = sql.Trim();
            if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only SELECT statements are allowed.");
            }

            var terminator = FindFirstStatementTerminator(trimmed);
            if (terminator >= 0 && HasNonWhitespaceAfter(trimmed, terminator + 1))
                throw new InvalidOperationException("Reports must contain a single SELECT statement.");

            return trimmed;
        }

        private static int FindFirstStatementTerminator(string sql)
        {
            var inSingleQuote = false;
            var inDoubleQuote = false;
            var inLineComment = false;
            var inBlockComment = false;

            for (var i = 0; i < sql.Length; i++)
            {
                var current = sql[i];
                var next = i + 1 < sql.Length ? sql[i + 1] : '\0';

                if (inLineComment)
                {
                    if (current == '\r' || current == '\n')
                        inLineComment = false;

                    continue;
                }

                if (inBlockComment)
                {
                    if (current == '*' && next == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }

                    continue;
                }

                if (!inSingleQuote && !inDoubleQuote)
                {
                    if (current == '-' && next == '-')
                    {
                        inLineComment = true;
                        i++;
                        continue;
                    }

                    if (current == '/' && next == '*')
                    {
                        inBlockComment = true;
                        i++;
                        continue;
                    }
                }

                if (!inDoubleQuote && current == '\'')
                {
                    if (inSingleQuote && next == '\'')
                    {
                        i++;
                        continue;
                    }

                    inSingleQuote = !inSingleQuote;
                    continue;
                }

                if (!inSingleQuote && current == '"')
                {
                    if (inDoubleQuote && next == '"')
                    {
                        i++;
                        continue;
                    }

                    inDoubleQuote = !inDoubleQuote;
                    continue;
                }

                if (!inSingleQuote && !inDoubleQuote && current == ';')
                    return i;
            }

            return -1;
        }

        private static bool HasNonWhitespaceAfter(string value, int start)
        {
            for (var i = start; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                    return true;
            }

            return false;
        }
    }
}
