namespace CentraSA.Domain.Common;

public interface IConcurrencyTracked
{
    long Version { get; set; }
}
