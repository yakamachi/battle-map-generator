namespace BattleMapGenerator.Api.Auth;

// The body of register and login. The email is also the account's user name.
public sealed record AuthRequest(string Email, string Password);

// What the API says about an account, and only ever about the caller's own.
public sealed record AccountInfo(string Email);
