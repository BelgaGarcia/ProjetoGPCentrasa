namespace CentraSA.Application.Common;

public sealed class DuplicateSmudCodeException(Exception innerException) : Exception(
    "Já existe um SMUD com esse código.",
    innerException);
