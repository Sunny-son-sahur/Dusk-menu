using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dusk.Launcher.Auth;

/// <summary>
/// Discord OAuth2 gate. Opens the authorize page in the default browser,
/// catches the redirect on a loopback listener, exchanges the code for a
/// token, and caches it in %APPDATA%\Dusk Launcher\auth.json.
///
/// Fill in DISCORD_CLIENT_ID and DISCORD_CLIENT_SECRET with your Discord
/// application (Settings -> OAuth2 -> create a client). Redirect URL must be:
///     http://127.0.0.1:48153/callback
/// </summary>
public static class DiscordAuth
{
    // Discord application values (OAuth2 -> General).
public const string ClientId     = "1546375353591926836";
    public const string ClientSecret = "lXFMzYu3CXN1AimquebkzIbBCU0GXNIb";

    private const int  CallbackPort = 48153;
    private const string CallbackPath = "/callback";
    private static readonly string RedirectUri = $"http://127.0.0.1:{CallbackPort}{CallbackPath}";
    private const string AuthorizeUrl = "https://discord.com/api/oauth2/authorize";
    private const string TokenUrl     = "https://discord.com/api/oauth2/token";
    internal const string ApiBase     = "https://discord.com/api";

    private static readonly string TokenPath = Path.Combine(App.DataDir, "auth.json");

    public sealed class AuthToken
    {
        public string? AccessToken  { get; set; }
        public string? RefreshToken { get; set; }
        public string? TokenType    { get; set; }
        public int      ExpiresIn   { get; set; }
        public long?    ExpiresAt   { get; set; }
        public string?  UserId      { get; set; }
        public string?  Username    { get; set; }
        public string?  GlobalName  { get; set; }
        public string?  AvatarUrl   { get; set; }
    }

    public static AuthToken? LoadToken()
    {
        if (!File.Exists(TokenPath))
            return null;
        try
        {
            var token = JsonSerializer.Deserialize<AuthToken>(File.ReadAllText(TokenPath));
            if (token?.AccessToken is null)
                return null;
            return token; // may be expired; EnsureFreshAsync() refreshes it
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns a token guaranteed fresh for the next few minutes. If the
    /// stored token is stale (or near expiry) it is silently refreshed using
    /// the refresh token. Returns null only when there is no usable login.
    /// </summary>
    public static async Task<AuthToken?> EnsureFreshAsync()
    {
        var token = LoadToken();
        if (token?.RefreshToken is null || token.AccessToken is null)
            return null;

        // Valid for the next minute -> use as-is.
        if (token.ExpiresAt is long at && DateTimeOffset.UtcNow.ToUnixTimeSeconds() < at - 60)
            return token;

        var fresh = await RefreshAsync(token);
        if (fresh is null)
            ClearToken(); // refresh failed -> force a fresh login next time
        return fresh;
    }

    public static async Task<AuthToken?> RefreshAsync(AuthToken token)
    {
        using var http = new HttpClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"]     = ClientId,
            ["client_secret"] = ClientSecret,
            ["grant_type"]    = "refresh_token",
            ["refresh_token"] = token.RefreshToken ?? "",
        });

        HttpResponseMessage resp;
        try
        {
            resp = await http.PostAsync(TokenUrl, form);
        }
        catch (HttpRequestException)
        {
            return null;
        }

        if (!resp.IsSuccessStatusCode)
            return null;

        var fresh = await resp.Content.ReadFromJsonAsync<AuthToken>();
        if (fresh?.AccessToken is null)
            return null;

        // Discord rotates refresh tokens; carry over the old one if absent.
        fresh.RefreshToken ??= token.RefreshToken;
        // Identity is not part of the token response.
        fresh.UserId     = token.UserId;
        fresh.Username   = token.Username;
        fresh.GlobalName = token.GlobalName;
        fresh.AvatarUrl  = token.AvatarUrl;

        SaveToken(fresh);
        return fresh;
    }

    public static void SaveToken(AuthToken token)
    {
        token.ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + token.ExpiresIn;
        File.WriteAllText(TokenPath, JsonSerializer.Serialize(token, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void ClearToken()
    {
        if (File.Exists(TokenPath))
            File.Delete(TokenPath);
    }

    /// <summary>
    /// Runs the browser auth flow. Returns the token on success, null if the
    /// user cancelled.
    /// </summary>
    public static async Task<AuthToken?> AuthorizeAsync()
    {
        var state = Guid.NewGuid().ToString("N");
        string url = $"{AuthorizeUrl}?client_id={ClientId}" +
                     $"&response_type=code" +
                     $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                     $"&scope=identify%20guilds" +
                     $"&state={state}";

        using var listener = new HttpListener();
        listener.Prefixes.Add(RedirectUri);
        listener.Start();

        // Open the Discord authorize page in the default browser.
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
        {
            UseShellExecute = true
        });

        // Wait for the redirect (task finishes on the callback or timeout).
        var code = await WaitForCallbackAsync(listener, state);
        if (code is null)
            return null;

        var token = await ExchangeCodeAsync(code);
        if (token?.AccessToken is null)
            return null;

        await FetchIdentityAsync(token);
        SaveToken(token);
        return token;
    }

    private static async Task<string?> WaitForCallbackAsync(HttpListener listener, string state)
    {
        while (listener.IsListening)
        {
            try
            {
                var ctx = await listener.GetContextAsync().WaitAsync(TimeSpan.FromMinutes(2));
                if (ctx.Request.Url?.AbsolutePath != CallbackPath)
                    continue;

                var query = ParseQuery(ctx.Request.Url.Query);
                ServeHtml(ctx, query.TryGetValue("state", out var s) && s == state && query.ContainsKey("code"));
                return (query.TryGetValue("state", out var ss) && ss == state
                        && query.TryGetValue("code", out var c))
                    ? c
                    : null;
            }
            catch (TimeoutException)
            {
                return null;
            }
            catch (HttpListenerException)
            {
                return null;
            }
        }
        return null;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(query))
            return result;
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && !result.ContainsKey(kv[0]))
                result[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
        }
        return result;
    }

    private static void ServeHtml(HttpListenerContext ctx, bool success)
    {
        const string ok = "<html><body style='background:#121216;color:#e6e6ee;font-family:sans-serif;display:flex;align-items:center;justify-content:center;height:100vh'><div style='text-align:center'><h1 style='color:#00aaff'>Dusk Launcher</h1><p>Authorization complete. You can close this tab.</p></div></body></html>";
        const string fail = "<html><body style='background:#121216;color:#e6e6ee;font-family:sans-serif;display:flex;align-items:center;justify-content:center;height:100vh'><div style='text-align:center'><h1 style='color:#ff4455'>Dusk Launcher</h1><p>Authorization failed or was cancelled. Return to the app.</p></div></body></html>";
        var body = System.Text.Encoding.UTF8.GetBytes(success ? ok : fail);
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.OutputStream.Write(body);
        ctx.Response.OutputStream.Close();
    }

    private static async Task<AuthToken?> ExchangeCodeAsync(string code)
    {
        using var http = new HttpClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"]     = ClientId,
            ["client_secret"] = ClientSecret,
            ["grant_type"]    = "authorization_code",
            ["code"]          = code,
            ["redirect_uri"]  = RedirectUri,
        });

        var resp = await http.PostAsync(TokenUrl, form);
        if (!resp.IsSuccessStatusCode)
            return null;
        return await resp.Content.ReadFromJsonAsync<AuthToken>();
    }

    private static async Task FetchIdentityAsync(AuthToken token)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var resp = await http.GetAsync($"{ApiBase}/users/@me");
        if (!resp.IsSuccessStatusCode)
            return;
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        token.UserId     = root.GetProperty("id").GetString();
        token.Username   = root.GetProperty("username").GetString();
        token.GlobalName = root.TryGetProperty("global_name", out var gn) ? gn.GetString() : null;
        token.AvatarUrl  = root.TryGetProperty("avatar", out var av) && av.GetString() is string avt
            ? $"https://cdn.discordapp.com/avatars/{token.UserId}/{avt}.png?size=128"
            : null;
    }
}