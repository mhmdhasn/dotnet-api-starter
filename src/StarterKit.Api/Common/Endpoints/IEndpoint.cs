namespace StarterKit.Api.Common.Endpoints;

/// <summary>
/// Implement this interface to register minimal API routes. Implementations are discovered
/// automatically by <see cref="EndpointExtensions.AddEndpoints"/>, so adding a feature never
/// requires touching Program.cs.
/// </summary>
public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}
