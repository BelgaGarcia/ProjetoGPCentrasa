namespace CentraSA.Domain.Rules;

public static class TicketNumberNormalizer
{
    public static string Normalize(string ticketNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketNumber);

        string normalized = ticketNumber.Trim();
        if (normalized.Any(character => !char.IsAsciiDigit(character)))
        {
            throw new FormatException("O número do chamado deve conter somente dígitos.");
        }

        return normalized;
    }
}
