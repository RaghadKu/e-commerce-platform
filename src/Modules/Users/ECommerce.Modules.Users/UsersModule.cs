
using System.Text;
using ECommerce.Modules.Users.Application;
using ECommerce.Modules.Users.Domain;
using ECommerce.Modules.Users.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ECommerce.Modules.Users;

public static class UsersModule
{
    public static IServiceCollection AddUsersModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // JWT Options
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Login Security Options
        services.AddOptions<LoginSecurityOptions>()
            .Bind(configuration.GetSection(LoginSecurityOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Database
        services.AddDbContext<UsersDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>()
                .GetConnectionString("MainDb")
                ?? throw new InvalidOperationException(
                    "Connection string 'MainDb' is not configured.");

            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    UsersDbContext.Schema));
        });

        // Identity
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;

            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
        })
        .AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<UsersDbContext>();

        // Identity Lockout
        services.AddOptions<IdentityOptions>()
            .Configure<IOptions<LoginSecurityOptions>>(
                (identity, security) =>
                {
                    identity.Lockout.AllowedForNewUsers = true;

                    identity.Lockout.MaxFailedAccessAttempts =
                        security.Value.MaxFailedAccessAttempts;

                    identity.Lockout.DefaultLockoutTimeSpan =
                        TimeSpan.FromMinutes(
                            security.Value.LockoutMinutes);
                });

        // JWT Bearer Options
        services.AddOptions<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwt.Issuer,

                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,

                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwt.Key)),

                        ValidAlgorithms =
                            [SecurityAlgorithms.HmacSha256],

                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),

                        NameClaimType = "name",
                        RoleClaimType = "role"
                    };
            });

        // Services
        services.AddSingleton<JwtTokenGenerator>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        // Hosted Services
        services.AddHostedService<UsersDbInitializer>();
        services.AddHostedService<AdminsDbSeeder>();

        // Seeders
        services.AddScoped<IUsersDbSeeder, CustomerSeeder>();

        return services;
    }
}
