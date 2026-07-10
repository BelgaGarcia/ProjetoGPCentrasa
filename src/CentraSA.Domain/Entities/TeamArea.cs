using CentraSA.Domain.Enums;

namespace CentraSA.Domain.Entities;

public sealed class TeamArea
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public TeamAreaKind Kind { get; set; }

    public string ColorToken { get; set; } = "blue";

    public bool IsActive { get; set; } = true;

    public ICollection<Person> People { get; } = new List<Person>();
}
