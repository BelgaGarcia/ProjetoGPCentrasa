namespace CentraSA.Application.Common;

public sealed class LookupConflictException(Exception innerException)
    : Exception("O cadastro auxiliar conflita com um registro existente.", innerException);
