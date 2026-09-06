using System.Numerics;

namespace Analysis.Domain.Scoring;

public sealed class DecimalMath(NumericPolicy policy)
{
    public decimal Round(decimal value) => decimal.Round(value, policy.Places, MidpointRounding.ToEven);
    public decimal Final(decimal value) => decimal.Round(value, policy.ScorePlaces, MidpointRounding.ToEven);
    public decimal Divide(decimal numerator, decimal denominator) => Round(checked(numerator / denominator));
    public decimal Change(decimal value, decimal baseline) => Round(checked(Divide(value, baseline) - 1));
    public decimal Clip(decimal value, decimal threshold) => Math.Clamp(Divide(value, threshold), -1, 1);
    public decimal Sum(IEnumerable<decimal> values)
    { decimal sum = 0; foreach (var value in values) sum = checked(sum + value); return sum; }

    public decimal Sqrt(decimal value)
    {
        if (value < 0) throw new ArithmeticException("Negative square root.");
        // Recover the exact decimal coefficient without a decimal scaling multiplication.
        var bits = decimal.GetBits(value);
        var coefficient = new BigInteger((uint)bits[0]) + (new BigInteger((uint)bits[1]) << 32) +
            (new BigInteger((uint)bits[2]) << 64);
        var scale = (bits[3] >> 16) & 0xff;
        var power = 2 * policy.Places - scale;
        if (power < 0) throw new ArithmeticException("Unsupported square-root scale.");
        var n = coefficient * BigInteger.Pow(10, power);
        var floor = IntegerSqrt(n);
        var comparison = (4 * n).CompareTo(BigInteger.Pow(2 * floor + 1, 2));
        if (comparison > 0 || comparison == 0 && !floor.IsEven) floor++;
        var digits = floor.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(policy.Places + 1, '0');
        return ExactDecimal.Parse(digits.Insert(digits.Length - policy.Places, "."));
    }
    private static BigInteger IntegerSqrt(BigInteger value)
    {
        if (value <= 1) return value;
        var current = BigInteger.One << checked((int)((value.GetBitLength() + 1) / 2));
        while (true)
        {
            var next = (current + value / current) / 2;
            if (next >= current) return current;
            current = next;
        }
    }
}
