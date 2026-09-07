using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;

// Dusk Role Service — always-on helper for the launcher's access gate.
//
// The launcher verifies that a user is a member of the Dusk server directly
// through the user's own OAuth token. Reading the *role* over OAuth requires
// the guilds.members.read scope, which Discord only grants to apps in 100+
// servers. So this little service sits on Railway with the bot token and
// answers one question: "does user X have the role?"
//
//   GET /health                -> { ok: true }
//   GET /access?uid=123456789  -> { allowed, inServer, hasRole, pending, roleName, reason }
//
// Env vars (set in Railway):
//   DISCORD_BOT_TOKEN  - bot token from Discord Developer Portal -> Bot
//   GUILD_ID           - the Dusk server id
//   ROLE_ID            - the id of the role that unlocks the launcher

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string botToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN") ?? "";
string guildId  = Environment.GetEnvironmentVariable("GUILD_ID") ?? "";
string roleId   = Environment.GetEnvironmentVariable("ROLE_ID") ?? "";

using var http = new HttpClient();
if (botToken != "")
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", botToken);

// Short-lived caches so we don't hammer the Discord API for identical calls.
var accessCache = new ConcurrentDictionary<string, (bool allowed, string reason, string? roleName, DateTime until)>();
var rolesCache  = new ConcurrentDictionary<string, (Dictionary<string, string> byId, DateTime until)>();

app.MapGet("/health", () => Results.Ok(new { ok = true }));

app.MapGet("/access", async (string uid) =>
{
    if (botToken == "" || guildId == "" || roleId == "")
        return Results.Json(new { allowed = false, reason = "service_not_configured" },
            statusCode: StatusCodes.Status500InternalServerError);

    if (accessCache.TryGetValue(uid, out var hit) && hit.until > DateTime.UtcNow)
        return Results.Ok(new { allowed = hit.allowed, inServer = true, hasRole = hit.allowed,
            pending = false, roleName = hit.roleName, reason = hit.reason });

    var resp = await http.GetAsync($"https://discord.com/api/v10/guilds/{guildId}/members/{uid}");

    if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        accessCache[uid] = (false, "not_in_server", null, DateTime.UtcNow.AddSeconds(60));
        return Results.Ok(new { allowed = false, inServer = false, hasRole = false,
            pending = false, roleName = (string?)null, reason = "not_in_server" });
    }

    if (!resp.IsSuccessStatusCode)
        return Results.Json(new { allowed = false, reason = $"discord_error_{resp.StatusCode}" },
            statusCode: StatusCodes.Status502BadGateway);

    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
    var root = doc.RootElement;

    bool pending = root.TryGetProperty("pending", out var p) && p.GetBoolean();
    bool hasRole = false;
    if (root.TryGetProperty("roles", out var roles))
    {
        foreach (var r in roles.EnumerateArray())
        {
            if (r.GetString() == roleId)
            {
                hasRole = true;
                break;
            }
        }
    }

    string? roleName = await GetRoleNameAsync();
    bool allowed = hasRole && !pending;
    string reason = allowed ? "allowed" : pending ? "pending" : "missing_role";

    accessCache[uid] = (allowed, reason, roleName, DateTime.UtcNow.AddSeconds(60));
    return Results.Ok(new { allowed, inServer = true, hasRole, pending, roleName, reason });
});

// Resolve the role id -> display name once, refresh every 5 minutes.
async Task<string?> GetRoleNameAsync()
{
    if (rolesCache.TryGetValue(roleId, out var hit) && hit.until > DateTime.UtcNow)
        return hit.byId.TryGetValue(roleId, out var n) ? n : null;

    var resp = await http.GetAsync($"https://discord.com/api/v10/guilds/{guildId}/roles");
    var byId = new Dictionary<string, string>();
    if (resp.IsSuccessStatusCode)
    {
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        foreach (var role in doc.RootElement.EnumerateArray())
        {
            if (role.TryGetProperty("id", out var id) && role.TryGetProperty("name", out var name_))
                byId[id.GetString()!] = name_.GetString()!;
        }
    }
    rolesCache[roleId] = (byId, DateTime.UtcNow.AddMinutes(5));
    return byId.TryGetValue(roleId, out var rname) ? rname : null;
}

app.Run();