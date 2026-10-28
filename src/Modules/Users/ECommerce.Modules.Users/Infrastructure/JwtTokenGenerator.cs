using System.Security.Claims;
using System.Text;
using ECommerce.Modules.Users.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ECommerce.Modules.Users.Infrastructure;

internal sealed class JwtTokenGenerator(IOptions<JwtOptions> options)
{
    public (string Token, DateTime ExpiresAtUtc) Generate(ApplicationUser user, IEnumerable<string> roles)
    {
        var jwt = options.Value;
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(jwt.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("email", user.Email ?? string.Empty),
            new("name", user.FullName),
            new("jti", Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                SecurityAlgorithms.HmacSha256)
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
