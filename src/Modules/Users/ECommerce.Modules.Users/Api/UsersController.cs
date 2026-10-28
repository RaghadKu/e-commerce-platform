using ECommerce.Modules.Users.Application;
using ECommerce.SharedKernel.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Modules.Users.Api;

[ApiController]
[Route("api/users")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        var id = User.GetUserId();
        return await userService.GetByIdAsync(id, id, User.IsAdmin());
    }

    [HttpPut("me")]
    public async Task<ActionResult<UserDto>> UpdateMe(UpdateProfileRequest request)
        => await userService.UpdateProfileAsync(User.GetUserId(), request);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<UserDto>> GetById(Guid id)
        => await userService.GetByIdAsync(id, User.GetUserId(), User.IsAdmin());
}
