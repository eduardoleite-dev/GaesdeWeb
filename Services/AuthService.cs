using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GaesdeWeb.Models;

namespace GaesdeWeb.Services;

public sealed class AuthService
{
    private readonly ApiService api;
    private readonly SessionService session;

    public AuthService(ApiService api, SessionService session)
    {
        this.api = api;
        this.session = session;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        var response = await api.Client.PostAsJsonAsync("api/Auth/login", new LoginRequest(username, password));
        if (!response.IsSuccessStatusCode)
            return false;

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (result is null || string.IsNullOrWhiteSpace(result.Token) || result.ExpiresAt <= DateTimeOffset.UtcNow)
            return false;

        var claims = ReadClaims(result.Token);
        await session.SetAsync(new SessionData(result.Token, result.ExpiresAt, result.Username, result.UserId)
        {
            AccessLevel = claims.AccessLevel,
            Role = claims.Role
        });
        return true;
    }

    private static (AccessLevel AccessLevel, string Role) ReadClaims(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2)
                return (AccessLevel.Unknown, "Usuário");

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            var root = document.RootElement;
            var accessLevel = ParseAccessLevel(ReadClaim(root, "nivel_acesso", "accessLevel", "access_level"));
            var role = ReadClaim(root, "role", "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
            return (
                accessLevel,
                string.IsNullOrWhiteSpace(role) ? "Usuário" : role);
        }
        catch
        {
            return (AccessLevel.Unknown, "Usuário");
        }
    }

    private static AccessLevel ParseAccessLevel(string value)
    {
        if (int.TryParse(value, out var numericValue) && Enum.IsDefined(typeof(AccessLevel), numericValue))
            return (AccessLevel)numericValue;

        return Enum.TryParse<AccessLevel>(value, true, out var accessLevel)
            ? accessLevel
            : AccessLevel.Unknown;
    }

    private static string ReadClaim(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var claim))
                continue;

            if (claim.ValueKind == JsonValueKind.String)
                return claim.GetString() ?? "-";

            if (claim.ValueKind == JsonValueKind.Number)
                return claim.ToString();
        }

        return "-";
    }
}