namespace StarterKit.Api.Common.Errors;

/// <summary>
/// Base class for expected, client-facing errors. <see cref="GlobalExceptionHandler"/> converts it to an
/// RFC 7807 problem response using <see cref="StatusCode"/>, <see cref="Title"/> and the exception message.
/// </summary>
public class AppException(int statusCode, string title, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public string Title { get; } = title;
}

public sealed class NotFoundException(string message = "The requested resource was not found.")
    : AppException(StatusCodes.Status404NotFound, "Not Found", message);

public sealed class ConflictException(string message = "The request conflicts with the current state of the resource.")
    : AppException(StatusCodes.Status409Conflict, "Conflict", message);

public sealed class ForbiddenException(string message = "You do not have permission to perform this action.")
    : AppException(StatusCodes.Status403Forbidden, "Forbidden", message);
