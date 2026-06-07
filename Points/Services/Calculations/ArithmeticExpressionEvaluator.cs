using System.Globalization;

namespace Points.Services.Calculations;

public static class ArithmeticExpressionEvaluator
{
    public static bool TryEvaluate(string? expression, out double result)
    {
        result = 0;

        if (string.IsNullOrWhiteSpace(expression))
            return false;

        var parser = new Parser(expression);
        if (!parser.TryParseExpression(out result))
            return false;

        parser.SkipWhitespace();
        return parser.IsAtEnd && double.IsFinite(result);
    }

    public static string FormatResult(double result)
    {
        return result.ToString("G15", CultureInfo.InvariantCulture);
    }

    private sealed class Parser
    {
        private readonly string _text;
        private int _index;

        public Parser(string text)
        {
            _text = text;
        }

        public bool IsAtEnd => _index >= _text.Length;

        public bool TryParseExpression(out double value)
        {
            if (!TryParseTerm(out value))
                return false;

            while (true)
            {
                SkipWhitespace();

                if (TryConsume('+'))
                {
                    if (!TryParseTerm(out var right))
                        return false;

                    value += right;
                }
                else if (TryConsume('-'))
                {
                    if (!TryParseTerm(out var right))
                        return false;

                    value -= right;
                }
                else
                {
                    return double.IsFinite(value);
                }
            }
        }

        public void SkipWhitespace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(_text[_index]))
                _index++;
        }

        private bool TryParseTerm(out double value)
        {
            if (!TryParseFactor(out value))
                return false;

            while (true)
            {
                SkipWhitespace();

                if (TryConsume('*'))
                {
                    if (!TryParseFactor(out var right))
                        return false;

                    value *= right;
                }
                else if (TryConsume('/'))
                {
                    if (!TryParseFactor(out var right) || right == 0)
                        return false;

                    value /= right;
                }
                else
                {
                    return double.IsFinite(value);
                }
            }
        }

        private bool TryParseFactor(out double value)
        {
            SkipWhitespace();

            if (TryConsume('+'))
                return TryParseFactor(out value);

            if (TryConsume('-'))
            {
                if (!TryParseFactor(out value))
                    return false;

                value = -value;
                return double.IsFinite(value);
            }

            if (TryConsume('('))
            {
                if (!TryParseExpression(out value))
                    return false;

                SkipWhitespace();
                return TryConsume(')') && double.IsFinite(value);
            }

            return TryParseNumber(out value);
        }

        private bool TryParseNumber(out double value)
        {
            value = 0;
            SkipWhitespace();

            var start = _index;
            var hasDigits = false;
            var hasDecimal = false;
            var currentDecimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

            while (!IsAtEnd && char.IsDigit(_text[_index]))
            {
                hasDigits = true;
                _index++;
            }

            if (TryConsumeDecimalSeparator(currentDecimalSeparator, ref hasDecimal))
            {
                while (!IsAtEnd && char.IsDigit(_text[_index]))
                {
                    hasDigits = true;
                    _index++;
                }
            }

            if (!hasDigits)
            {
                _index = start;
                return false;
            }

            TryConsumeExponent();

            var token = _text[start.._index];
            if (currentDecimalSeparator != ".")
                token = token.Replace(currentDecimalSeparator, ".", StringComparison.Ordinal);

            return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                   double.IsFinite(value);
        }

        private bool TryConsumeDecimalSeparator(string currentDecimalSeparator, ref bool hasDecimal)
        {
            if (TryConsume('.'))
            {
                hasDecimal = true;
                return true;
            }

            if (currentDecimalSeparator == "." ||
                !StartsWith(currentDecimalSeparator) ||
                hasDecimal)
            {
                return false;
            }

            _index += currentDecimalSeparator.Length;
            hasDecimal = true;
            return true;
        }

        private void TryConsumeExponent()
        {
            if (IsAtEnd || (_text[_index] != 'e' && _text[_index] != 'E'))
                return;

            var exponentStart = _index;
            _index++;

            if (!IsAtEnd && (_text[_index] == '+' || _text[_index] == '-'))
                _index++;

            var hasExponentDigits = false;
            while (!IsAtEnd && char.IsDigit(_text[_index]))
            {
                hasExponentDigits = true;
                _index++;
            }

            if (!hasExponentDigits)
                _index = exponentStart;
        }

        private bool TryConsume(char value)
        {
            if (IsAtEnd || _text[_index] != value)
                return false;

            _index++;
            return true;
        }

        private bool StartsWith(string value)
        {
            return _text.AsSpan(_index).StartsWith(value.AsSpan(), StringComparison.Ordinal);
        }
    }
}
