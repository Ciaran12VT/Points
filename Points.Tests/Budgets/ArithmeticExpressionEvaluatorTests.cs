using Points.Services.Calculations;
using Xunit;

namespace Points.Tests.Budgets;

public sealed class ArithmeticExpressionEvaluatorTests
{
    [Theory]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("(10 - 2.5) / 3", 2.5)]
    [InlineData("-1 + 2", 1)]
    [InlineData(".5 * 8", 4)]
    [InlineData("1e2 + 5", 105)]
    public void TryEvaluate_ComputesValidExpressions(string expression, double expected)
    {
        var parsed = ArithmeticExpressionEvaluator.TryEvaluate(expression, out var result);

        Assert.True(parsed);
        Assert.Equal(expected, result, precision: 10);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1 +")]
    [InlineData("1 / 0")]
    [InlineData("abc")]
    [InlineData("(1 + 2")]
    public void TryEvaluate_RejectsInvalidExpressions(string expression)
    {
        var parsed = ArithmeticExpressionEvaluator.TryEvaluate(expression, out _);

        Assert.False(parsed);
    }
}
