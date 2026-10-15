using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace StarterKit.Api.Common.Endpoints;

public static class EndpointExtensions
{
    /// <summary>Scans <paramref name="assembly"/> for <see cref="IEndpoint"/> implementations and registers them.</summary>
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        ServiceDescriptor[] descriptors = assembly
            .DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))
            .ToArray();

        services.TryAddEnumerable(descriptors);
        return services;
    }

    /// <summary>Calls <see cref="IEndpoint.Map"/> on every registered endpoint.</summary>
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        foreach (IEndpoint endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>())
        {
            endpoint.Map(app);
        }

        return app;
    }
}
