namespace CentraSA.Application.Common;

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(Exception innerException)
        : base("O registro foi alterado por outra operação.", innerException)
    {
    }
}
