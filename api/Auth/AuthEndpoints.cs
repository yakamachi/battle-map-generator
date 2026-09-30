using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace BattleMapGenerator.Api.Auth;

public static class AuthEndpoints
{
    public const string AuthRateLimitPolicy = "auth";

    // Register and login are public and hash a password, so they share one per-IP limit.
    // Logout and me are not limited: they cost no hashing and me needs a session.
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register", Register)
            .WithName("Register")
            .RequireRateLimiting(AuthRateLimitPolicy)
            .Produces(StatusCodes.Status429TooManyRequests);
        app.MapPost("/api/auth/login", Login)
            .WithName("Login")
            .RequireRateLimiting(AuthRateLimitPolicy)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);
        app.MapPost("/api/auth/logout", Logout)
            .WithName("Logout");
        app.MapGet("/api/auth/me", Me)
            .WithName("GetCurrentAccount")
            .RequireAuthorization()
            .Produces(StatusCodes.Status401Unauthorized);
        return app;
    }

    private static async Task<Results<Ok<AccountInfo>, ValidationProblem>> Register(
        AuthRequest request, UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn)
    {
        // A JSON body may leave a field out or send null; Identity then reports it as invalid or too short.
        var email = request.Email ?? string.Empty;
        var user = new IdentityUser { UserName = email, Email = email };

        var result = await users.CreateAsync(user, request.Password ?? string.Empty);
        if (!result.Succeeded)
        {
            return TypedResults.ValidationProblem(ToFieldErrors(result, email, users.ErrorDescriber));
        }

        await signIn.SignInAsync(user, isPersistent: true);
        return TypedResults.Ok(new AccountInfo(email));
    }

    // Wrong password, unknown email and a locked-out account are indistinguishable to the caller.
    private static async Task<Results<Ok<AccountInfo>, UnauthorizedHttpResult>> Login(
        AuthRequest request, UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn)
    {
        var user = await users.FindByEmailAsync(request.Email ?? string.Empty);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var result = await signIn.PasswordSignInAsync(user, request.Password ?? string.Empty, isPersistent: true, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(new AccountInfo(user.Email!));
    }

    // Also answers 204 when no one is logged in, so the client never has to check first.
    private static async Task<NoContent> Logout(SignInManager<IdentityUser> signIn)
    {
        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }

    // The email comes from the session cookie's claims: the caller's own account and no other.
    private static Results<Ok<AccountInfo>, UnauthorizedHttpResult> Me(ClaimsPrincipal user)
    {
        var email = user.FindFirstValue(ClaimTypes.Email);
        return email is null ? TypedResults.Unauthorized() : TypedResults.Ok(new AccountInfo(email));
    }

    // Identity reports the user name and the email separately; here they are the same value, so
    // everything that is not about the password is an email error, without repeated messages.
    private static Dictionary<string, string[]> ToFieldErrors(IdentityResult result, string email, IdentityErrorDescriber describer)
    {
        var passwordErrors = result.Errors.Where(e => e.Code.StartsWith("Password", StringComparison.Ordinal)).ToList();
        var emailErrors = result.Errors.Except(passwordErrors).ToList();
        if (emailErrors.Any(e => e.Code.EndsWith("Email", StringComparison.Ordinal)))
        {
            emailErrors.RemoveAll(e => e.Code.EndsWith("UserName", StringComparison.Ordinal));
        }

        var errors = new Dictionary<string, string[]>();
        if (emailErrors.Count > 0)
        {
            // A character the user name does not allow is reported as "Username ... is invalid";
            // to the caller it is the email that is wrong.
            errors["email"] = [.. emailErrors.Select(e => e.Code == nameof(IdentityErrorDescriber.InvalidUserName)
                ? describer.InvalidEmail(email).Description
                : e.Description)];
        }
        if (passwordErrors.Count > 0)
        {
            errors["password"] = [.. passwordErrors.Select(e => e.Description)];
        }
        return errors;
    }
}
