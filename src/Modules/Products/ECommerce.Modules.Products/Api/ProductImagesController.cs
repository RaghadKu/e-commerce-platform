using ECommerce.Modules.Products.Application;
using ECommerce.Modules.Products.Application.Services;
using ECommerce.SharedKernel.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Modules.Products.Api;

[ApiController]
[Route("api/products/{productId:guid}/images")]
public sealed class ProductImagesController(IProductImageService images) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType<ProductImageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ProductImageDto>> Upload(Guid productId, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var image = await images.AddAsync(productId, stream, file.FileName, file.Length, ct);
        return Created(image.Url, image);
    }

    [HttpGet("{imageId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(Guid productId, Guid imageId, CancellationToken ct)
    {
        var file = await images.GetAsync(productId, imageId, ct);
        return File(file.Content, file.ContentType);
    }

    [HttpDelete("{imageId:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid productId, Guid imageId, CancellationToken ct)
    {
        await images.DeleteAsync(productId, imageId, ct);
        return NoContent();
    }
}
