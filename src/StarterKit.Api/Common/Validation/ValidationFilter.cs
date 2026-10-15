using FluentValidation;

namespace StarterKit.Api.Common.Validation;

/// <summary>
/// Endpoint filter that runs the registered <see cref="IValidator{T}"/> for the request argument of type
/// <typeparamref name="T"/> and short-circuits with an RFC 7807 validation problem (HTTP 400) on failure.
/// </summary>
public sealed class ValidationFilter<T> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        IValidator<T>? validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is null)
        {
            return await next(context);
        }

        T? argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = ["A request body is required."]
            });
        }

        var result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
        if (!result.IsValid)
        {
            return Results.ValidationProblem(result.ToDictionary());
        }

        return await next(context);
    }
}
