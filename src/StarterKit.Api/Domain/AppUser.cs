using Microsoft.AspNetCore.Identity;

namespace StarterKit.Api.Domain;

public class AppUser : IdentityUser<Guid>
{
    public AppUser()
    {
        Id = Guid.CreateVersion7();
    }

    public string? DisplayName { get; set; }
}
