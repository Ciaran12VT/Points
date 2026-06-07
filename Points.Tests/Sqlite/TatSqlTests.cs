using Points.Services.Sqlite.Queries;
using Xunit;

namespace Points.Tests.Sqlite;

public sealed class TatSqlTests
{
    [Fact]
    public void GetTatModelDataById_DoesNotHaveATrailingProjectionComma()
    {
        var normalizedSql = TatSql.GetTatModelDataById
            .Replace("\r", "")
            .Replace("\n", " ");

        Assert.DoesNotContain(",                 FROM", normalizedSql);
        Assert.DoesNotContain(", FROM", normalizedSql);
    }

    [Fact]
    public void GetTatModelDataById_ProjectsTargetActiveTime()
    {
        Assert.Contains("t.TargetActiveTimeSeconds AS TargetActiveTimeSeconds", TatSql.GetTatModelDataById);
    }
}
