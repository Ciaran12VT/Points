using Points.Services.Reports;
using Xunit;

namespace Points.Tests.Reports;

public sealed class ReportSqlGuardTests
{
    [Fact]
    public void ValidateSelectStatement_AllowsSelect()
    {
        var sql = ReportSqlGuard.ValidateSelectStatement(" SELECT 1; ");

        Assert.Equal("SELECT 1;", sql);
    }

    [Fact]
    public void ValidateSelectStatement_AllowsSemicolonInsideStringLiteral()
    {
        var sql = ReportSqlGuard.ValidateSelectStatement("SELECT ';' AS Value");

        Assert.Equal("SELECT ';' AS Value", sql);
    }

    [Fact]
    public void ValidateSelectStatement_RejectsNonSelect()
    {
        Assert.Throws<InvalidOperationException>(
            () => ReportSqlGuard.ValidateSelectStatement("DELETE FROM Card"));
    }

    [Fact]
    public void ValidateSelectStatement_RejectsMultipleStatements()
    {
        Assert.Throws<InvalidOperationException>(
            () => ReportSqlGuard.ValidateSelectStatement("SELECT 1; SELECT 2"));
    }
}
