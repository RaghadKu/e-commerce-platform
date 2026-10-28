using ECommerce.SharedKernel.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace ECommerce.Modules.Users.Application;

internal static class IdentityResultExtensions
{
    public static void ThrowIfFailed(this IdentityResult result)
    {
        if (result.Succeeded) return;

        if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
            throw new ConflictException("A user with this email already exists.");

        var errors = result.Errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

        throw new ValidationFailedException(errors);
    }
}
