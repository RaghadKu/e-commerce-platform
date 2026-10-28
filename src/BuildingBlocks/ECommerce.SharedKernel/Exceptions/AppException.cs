namespace ECommerce.SharedKernel.Exceptions;

public abstract class AppException(int statusCode, string title, string detail) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
}

public sealed class UnauthorizedException(string detail) : AppException(401, "Unauthorized", detail);
public sealed class ForbiddenException(string detail) : AppException(403, "Forbidden", detail);
public sealed class NotFoundException(string detail) : AppException(404, "Not Found", detail);
public sealed class ConflictException(string detail) : AppException(409, "Conflict", detail);
public sealed class LockedException(string detail) : AppException(423, "Locked", detail);

public sealed class ValidationFailedException(IDictionary<string, string[]> errors)
    : AppException(400, "One or more validation errors occurred.", "See the 'errors' property for details.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
