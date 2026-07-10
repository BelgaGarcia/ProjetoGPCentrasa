using Microsoft.AspNetCore.Identity;

namespace CentraSA.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTime CreatedAtUtc { get; set; }
}
