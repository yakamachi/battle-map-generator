using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BattleMapGenerator.Api.Tests.Infrastructure;

namespace BattleMapGenerator.Api.Tests;

// Register and login share one rate-limit bucket per app instance (TestServer has no client IP),
// so each test builds its own factory and stays under 10 register-or-login calls, except the 429 test.
// Accounts get unique emails, so the shared database needs no cleanup.
[Collection(SqlServerCollection.Name)]
public sealed class AuthEndpointTests(SqlServerFixture sql)
{
    private const string RegisterPath = "/api/auth/register";
    private const string LoginPath = "/api/auth/login";
    private const string LogoutPath = "/api/auth/logout";
    private const string MePath = "/api/auth/me";


    [Fact]
    public async Task Register_signs_the_account_in_and_me_returns_its_email()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = factory.CreateClient();
        var email = NewEmail();

        var registered = await client.PostAsJsonAsync(RegisterPath, Credentials(email));

        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        Assert.Equal(email, await ReadEmailAsync(registered));

        var me = await client.GetAsync(MePath);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(email, await ReadEmailAsync(me));
    }

    [Fact]
    public async Task Register_with_an_existing_email_returns_400_keyed_by_email()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var email = NewEmail();
        await factory.CreateLoggedInClientAsync(email);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(RegisterPath, Credentials(email));

        var errors = await ReadValidationErrorsAsync(response);
        Assert.Equal(["email"], errors.Keys);
        Assert.Contains("already taken", Assert.Single(errors["email"]));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(MePath)).StatusCode);
    }

    // The email is also the user name, so only letters, digits and -._@+ are accepted.
    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a!b@example.com")]
    public async Task Register_with_an_invalid_email_returns_400_keyed_by_email(string email)
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(RegisterPath, Credentials(email));

        var errors = await ReadValidationErrorsAsync(response);
        Assert.Equal(["email"], errors.Keys);
        Assert.Equal([$"Email '{email}' is invalid."], errors["email"]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(MePath)).StatusCode);
    }

    [Fact]
    public async Task Register_with_a_seven_character_password_returns_400_keyed_by_password()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(RegisterPath, Credentials(NewEmail(), "1234567"));

        var errors = await ReadValidationErrorsAsync(response);
        Assert.Equal(["password"], errors.Keys);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(MePath)).StatusCode);
    }

    // Length is the only password rule: eight characters of one kind are enough.
    [Fact]
    public async Task Register_accepts_an_eight_character_password_without_composition_rules()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(RegisterPath, Credentials(NewEmail(), "aaaaaaaa"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_the_right_password_returns_200_and_me_then_returns_the_account()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var email = NewEmail();
        await factory.CreateLoggedInClientAsync(email);
        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(LoginPath, Credentials(email));

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(email, await ReadEmailAsync(login));
        var me = await client.GetAsync(MePath);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(email, await ReadEmailAsync(me));
    }

    [Fact]
    public async Task Login_with_a_wrong_password_and_with_an_unknown_email_return_an_identical_401()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var email = NewEmail();
        await factory.CreateLoggedInClientAsync(email);
        var client = factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync(LoginPath, Credentials(email, "wrong-password"));
        var unknownEmail = await client.PostAsJsonAsync(LoginPath, Credentials(NewEmail()));

        foreach (var response in new[] { wrongPassword, unknownEmail })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsByteArrayAsync());
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.Null(response.Headers.Location);
        }
        Assert.Equal(HeaderNames(wrongPassword), HeaderNames(unknownEmail));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(MePath)).StatusCode);
    }

    [Fact]
    public async Task The_sixth_login_after_five_wrong_passwords_returns_401_even_with_the_right_password()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var email = NewEmail();
        await factory.CreateLoggedInClientAsync(email);
        var client = factory.CreateClient();

        for (var i = 1; i <= 5; i++)
        {
            var wrong = await client.PostAsJsonAsync(LoginPath, Credentials(email, "wrong-password"));
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        var lockedOut = await client.PostAsJsonAsync(LoginPath, Credentials(email));

        Assert.Equal(HttpStatusCode.Unauthorized, lockedOut.StatusCode);
        Assert.Empty(await lockedOut.Content.ReadAsByteArrayAsync());
        Assert.False(lockedOut.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Logout_returns_204_and_me_then_returns_401()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(MePath)).StatusCode);

        var logout = await client.PostAsync(LogoutPath, content: null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(MePath)).StatusCode);
    }

    [Fact]
    public async Task Logout_without_a_session_returns_204()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = factory.CreateClient();

        var logout = await client.PostAsync(LogoutPath, content: null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    // A cookie scheme's usual answer is a redirect to a login page; an API caller must get a plain 401.
    [Fact]
    public async Task Anonymous_me_returns_401_without_a_redirect_or_the_spa_shell()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.DoesNotContain(ApiFactory.SpaShellMarker, await response.Content.ReadAsStringAsync());
    }

    // TestServer speaks plain HTTP, so Secure is absent here; it follows the request scheme.
    [Fact]
    public async Task The_session_cookie_is_http_only_same_site_lax_and_persistent()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(RegisterPath, Credentials(NewEmail()));

        var attributes = SessionCookie(response).Split(';').Skip(1).Select(part => part.Trim().ToLowerInvariant()).ToList();
        Assert.Contains("httponly", attributes);
        Assert.Contains("samesite=lax", attributes);
        Assert.Contains(attributes, attribute => attribute.StartsWith("expires=", StringComparison.Ordinal));
    }

    // Restart simulation: the second instance reads the key ring from the database and accepts the cookie.
    [Fact]
    public async Task A_cookie_issued_by_one_instance_is_accepted_by_a_fresh_instance()
    {
        var email = NewEmail();
        string cookie;
        await using (var first = new ApiFactory(sql.ConnectionString))
        {
            var response = await first.CreateClient().PostAsJsonAsync(RegisterPath, Credentials(email));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            cookie = SessionCookie(response).Split(';')[0];
        }

        await using var second = new ApiFactory(sql.ConnectionString);
        var request = new HttpRequestMessage(HttpMethod.Get, MePath);
        request.Headers.Add("Cookie", cookie);

        var me = await second.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(email, await ReadEmailAsync(me));
    }

    [Fact]
    public async Task Two_accounts_each_see_only_their_own_email_from_me()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var firstEmail = NewEmail();
        var secondEmail = NewEmail();
        var first = await factory.CreateLoggedInClientAsync(firstEmail);
        var second = await factory.CreateLoggedInClientAsync(secondEmail);

        Assert.Equal(firstEmail, await ReadEmailAsync(await first.GetAsync(MePath)));
        Assert.Equal(secondEmail, await ReadEmailAsync(await second.GetAsync(MePath)));
    }

    [Fact]
    public async Task The_eleventh_register_or_login_within_a_minute_returns_429_with_retry_after()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var email = NewEmail();
        await factory.CreateLoggedInClientAsync(email);
        var client = factory.CreateClient();

        // Register above was the first call; nine logins by different accounts use up the rest.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(LoginPath, Credentials(email))).StatusCode);
        for (var i = 3; i <= 10; i++)
        {
            var allowed = await client.PostAsJsonAsync(LoginPath, Credentials(NewEmail()));
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var rejectedLogin = await client.PostAsJsonAsync(LoginPath, Credentials(email));
        var rejectedRegister = await client.PostAsJsonAsync(RegisterPath, Credentials(NewEmail()));

        foreach (var rejected in new[] { rejectedLogin, rejectedRegister })
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.True(rejected.Headers.RetryAfter?.Delta is { } delay && delay > TimeSpan.Zero && delay <= TimeSpan.FromMinutes(1),
                $"Retry-After was '{rejected.Headers.RetryAfter}'.");
        }

        // Logout and me are outside the limit.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(MePath)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(LogoutPath, content: null)).StatusCode);
    }

    // The database lesson: a request without a session cookie is refused without touching the database.
    [Fact]
    public async Task Startup_and_anonymous_me_and_logout_answer_quickly_with_an_unreachable_database()
    {
        var stopwatch = Stopwatch.StartNew();
        await using var factory = new ApiFactory(ApiFactory.UnreachableConnectionString);
        var client = factory.CreateClient();

        var me = await client.GetAsync(MePath);
        var logout = await client.PostAsync(LogoutPath, content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Startup, me and logout took {stopwatch.Elapsed}.");
    }

    // Static files are served before authentication, so a session cookie on them never loads the key
    // ring. A database of its own starts with no key; the first read of a cookie creates one.
    [Fact]
    public async Task A_session_cookie_on_a_static_file_does_not_load_the_key_ring()
    {
        string cookie;
        await using (var issuer = new ApiFactory(sql.ConnectionString))
        {
            var registered = await issuer.CreateClient().PostAsJsonAsync(RegisterPath, Credentials(NewEmail()));
            cookie = SessionCookie(registered).Split(';')[0];
        }

        var connectionString = await sql.CreateMigratedDatabaseAsync();
        await using var factory = new ApiFactory(connectionString);
        var client = factory.CreateClient(new() { HandleCookies = false });

        var file = new HttpRequestMessage(HttpMethod.Get, "/index.html");
        file.Headers.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(file)).StatusCode);
        Assert.Equal(0, await SqlServerFixture.CountKeysAsync(connectionString));

        // The same cookie on an API route is read, which is what loads the key ring.
        var me = new HttpRequestMessage(HttpMethod.Get, MePath);
        me.Headers.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(me)).StatusCode);
        Assert.Equal(1, await SqlServerFixture.CountKeysAsync(connectionString));
    }

    [Fact]
    public async Task Register_with_an_email_over_256_characters_returns_400_keyed_by_email()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(RegisterPath, Credentials(new string('a', 245) + "@example.com"));

        var errors = await ReadValidationErrorsAsync(response);
        Assert.Equal(["email"], errors.Keys);
        Assert.Equal(["Email must be at most 256 characters."], errors["email"]);
    }

    [Fact]
    public async Task Register_with_an_empty_password_returns_one_message()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(RegisterPath, Credentials(NewEmail(), ""));

        var errors = await ReadValidationErrorsAsync(response);
        Assert.Equal(["password"], errors.Keys);
        Assert.Equal(["Passwords must be at least 8 characters."], errors["password"]);
    }

    // A password is capped before it reaches the hasher: register says so, login answers its usual 401.
    [Fact]
    public async Task A_password_over_128_characters_is_refused_by_register_and_login()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = factory.CreateClient();
        var email = NewEmail();
        var longest = new string('p', 128);
        var tooLong = new string('p', 129);

        var errors = await ReadValidationErrorsAsync(await client.PostAsJsonAsync(RegisterPath, Credentials(email, tooLong)));
        Assert.Equal(["password"], errors.Keys);
        Assert.Equal(["Passwords must be at most 128 characters."], errors["password"]);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(RegisterPath, Credentials(email, longest))).StatusCode);
        await client.PostAsync(LogoutPath, content: null);

        var login = await client.PostAsJsonAsync(LoginPath, Credentials(email, tooLong));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.False(login.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(LoginPath, Credentials(email, longest))).StatusCode);
    }

    // There are no antiforgery tokens: the endpoints take JSON only, which a cross-site form cannot send.
    [Theory]
    [InlineData(RegisterPath, "application/x-www-form-urlencoded")]
    [InlineData(RegisterPath, "text/plain")]
    [InlineData(LoginPath, "application/x-www-form-urlencoded")]
    [InlineData(LoginPath, "text/plain")]
    public async Task Register_and_login_refuse_bodies_a_cross_site_form_can_send(string path, string contentType)
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();
        var email = (await ReadEmailAsync(await client.GetAsync(MePath)))!;
        await client.PostAsync(LogoutPath, content: null);

        var body = contentType == "text/plain"
            ? $$"""{"email":"{{email}}","password":"{{ApiFactory.TestPassword}}"}"""
            : $"email={Uri.EscapeDataString(email)}&password={ApiFactory.TestPassword}";
        var response = await client.PostAsync(path, new StringContent(body, null, contentType));

        Assert.False(response.IsSuccessStatusCode, $"{path} accepted {contentType} with {(int)response.StatusCode}.");
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(MePath)).StatusCode);
    }

    private static string NewEmail() => ApiFactory.NewEmail();

    private static object Credentials(string email, string password = ApiFactory.TestPassword) => new { email, password };

    private static async Task<string?> ReadEmailAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("email").GetString();
    }

    private static async Task<Dictionary<string, string[]>> ReadValidationErrorsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("errors").EnumerateObject().ToDictionary(
            field => field.Name,
            field => field.Value.EnumerateArray().Select(message => message.GetString()!).ToArray());
    }

    private static string SessionCookie(HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));

    private static IEnumerable<string> HeaderNames(HttpResponseMessage response) =>
        response.Headers.Concat(response.Content.Headers).Select(header => header.Key).Order();
}
