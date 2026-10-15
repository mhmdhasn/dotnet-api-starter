namespace StarterKit.Api.Common.Validation;

public static class ValidationExtensions
{
    /// <summary>
    /// Validates the request argument of type <typeparamref name="T"/> with its FluentValidation validator
    /// and documents the 400 validation-problem response.
    /// </summary>
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder)
        => builder
            .AddEndpointFilter<ValidationFilter<T>>()
            .ProducesValidationProblem();
}
