namespace CentraSA.Application.Common;

public sealed class DuplicateSupportTicketNumberException(Exception innerException) : Exception(
    "Já existe um chamado com esse número.",
    innerException);
