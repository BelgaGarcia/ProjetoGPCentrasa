namespace CentraSA.Domain.Entities;

public sealed class Person
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public Guid? TeamAreaId { get; set; }

    public TeamArea? TeamArea { get; set; }

    public bool IsActive { get; set; } = true;
}
