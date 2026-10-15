using System.Security.Claims;

namespace StarterKit.Api.Auth;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? Email { get; }
}

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId
        => Guid.TryParse(Principal?.FindFirstValue("sub"), out Guid id) ? id : null;

    public string? Email => Principal?.FindFirstValue("email");
}
