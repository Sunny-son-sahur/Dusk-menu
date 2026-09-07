using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dusk.Launcher.Auth;

/// <summary>
/// Access gate. After login the user must be a member of the Dusk server
/// and hold a specific role before the launcher unlocks.
///
/// Membership is checked directly against the user's own token
/// (/users/@me/guilds, "guilds" scope — no restricted scopes involved).
/// The role cannot be read over plain OAuth for small apps (Discord
/// restricts guilds.members.read to apps in 100+ servers), so the role is
/// verified by the always-on role service (a Discord bot hosted on Railway)
/// which reads guild member data over the bot API.
///
/// Fill in the four values below:
///   ServerGuildId  — your server's ID  (copy from server settings)
///   RequiredRoleId — the role ID       (right-click the role -> copy ID)
///   InviteUrl      — your permanent server invite
///   RoleServiceUrl — the Railway URL of dusk-role-service once deployed
/// </summary>
public static class AccessGate
{
    public const string ServerGuildId  = "1543035598736986184";
    public const string RequiredRoleId = "1543067837638254592";
    public const string InviteUrl      = "https://discord.gg/Ftfapzsr5m";
    public const string RoleServiceUrl = "https://YOUR_APP.up.railway.app";

    public enum Status
    {
        Allowed,
        NotLoggedIn,
        NotInServer,
        MissingRole,
        ServiceError,
    }

    public sealed record AccessResult(Status Status, DiscordAuth.AuthToken? Token, string? RoleName);

    public static async Task<AccessResult> CheckAsync()
    {
        var token = await DiscordAuth.EnsureFreshAsync();
        if (token is null)
            return new AccessResult(Status.NotLoggedIn, null, null);

        if (!await InServerAsync(token))
            return new AccessResult(Status.NotInServer, token, null);

        var (hasRole, serviceOk, roleName) = await HasRoleAsync(token);
        if (!serviceOk)
            return new AccessResult(Status.ServiceError, token, null);
        if (!hasRole)
            return new AccessResult(Status.MissingRole, token, roleName);

        return new AccessResult(Status.Allowed, token, null);
    }

    private static async Task<bool> InServerAsync(DiscordAuth.AuthToken token)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var resp = await http.GetAsync($"{DiscordAuth.ApiBase}/users/@me/guilds");
        if (!resp.IsSuccessStatusCode)
            return false;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        foreach (var guild in doc.RootElement.EnumerateArray())
        {
            if (guild.TryGetProperty("id", out var id) && id.GetString() == ServerGuildId)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Asks the Railway bot service whether this user holds the role.
    /// Returns (allowed, serviceOk, roleName). A service error is treated as
    /// a deny (fail closed) with serviceOk=false so the UI can say
    /// "verification service offline" instead of "you lack the role".
    /// </summary>
    private static async Task<(bool allowed, bool serviceOk, string? roleName)> HasRoleAsync(DiscordAuth.AuthToken token)
    {
        if (RoleServiceUrl.Contains("YOUR_"))
            return (false, false, null);

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var resp = await http.GetAsync($"{RoleServiceUrl}/access?uid={token.UserId}");
            if (!resp.IsSuccessStatusCode)
                return (false, false, null);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            bool allowed = root.TryGetProperty("allowed", out var a) && a.GetBoolean();
            string? roleName = root.TryGetProperty("roleName", out var rn) ? rn.GetString() : null;
            return (allowed, true, roleName);
        }
        catch (HttpRequestException)
        {
            return (false, false, null);
        }
        catch (TaskCanceledException)
        {
            return (false, false, null);
        }
        catch (JsonException)
        {
            return (false, false, null);
        }
    }

    public static bool IsConfigured()
    {
        return !ServerGuildId.Contains("YOUR_")
            && !RequiredRoleId.Contains("YOUR_")
            && !InviteUrl.Contains("YOUR_")
            && !RoleServiceUrl.Contains("YOUR_");
    }
}