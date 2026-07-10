using System.Globalization;

namespace CentraSA.Domain.Rules;

public static class SmudCodeNormalizer
{
    public static string Normalize(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        string compactCode = code.Trim().Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

        if (!compactCode.StartsWith("SMUD", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException("O código deve começar com SMUD.");
        }

        string numericPart = compactCode[4..];
        if (numericPart.Length == 0 || numericPart.Any(character => !char.IsAsciiDigit(character)))
        {
            throw new FormatException("O código SMUD deve terminar com dígitos.");
        }

        if (!long.TryParse(numericPart, NumberStyles.None, CultureInfo.InvariantCulture, out long number) || number <= 0)
        {
            throw new FormatException("O número do SMUD deve ser maior que zero.");
        }

        return $"SMUD{number.ToString("D3", CultureInfo.InvariantCulture)}";
    }
}
