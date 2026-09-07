# dusk-role-service

Always-on helper for the Dusk Launcher access gate. The launcher checks
server *membership* using the user's own Discord token, but reading the
*role* over plain OAuth needs the `guilds.members.read` scope, which
Discord blocks for small apps. This service holds the **bot token** and
answers one question, 24/7:

```
GET /health                -> { ok: true }
GET /access?uid=123456789  -> { allowed, inServer, hasRole, pending, roleName, reason }
```

`allowed` is true only when the user is in the guild, is not pending
(membership screening), and holds the configured role.

## deploy on Railway

1. **Create the bot** in the [Discord Developer Portal](https://discord.com/developers/applications)
   (the same application works for both launcher auth and this bot):
   - OAuth2 -> General: your redirect `http://127.0.0.1:48153/callback` stays as-is.
   - Bot -> **Add Bot** -> **Reset Token** -> copy the token.
   - Use the OAuth2 URL generator with scope `bot` to invite the bot to your server
     (it needs no special permissions to read a single member's roles).
2. **Get the ids:**
   - Server id: Discord -> server settings -> **Widget** (or enable Developer Mode and
     right-click the server name -> Copy Server ID).
   - Role id: enable Developer Mode, right-click the role -> Copy Role ID.
3. **Railway:** New Project -> Deploy from GitHub -> pick this repo. Railway
   detects the `Dockerfile` automatically.
4. Set the env vars in Railway -> Variables:
   ```
   DISCORD_BOT_TOKEN = <your bot token>
   GUILD_ID          = <your server id>
   ROLE_ID           = <your role id>
   ```
5. Copy the generated URL (e.g. `https://dusk-role-service-production-xxxx.up.railway.app`)
   and paste it into `DuskLauncher/src/Dusk.Launcher/Auth/AccessGate.cs`
   at `RoleServiceUrl`.

## run locally (optional)

```
DISCORD_BOT_TOKEN=xxx GUILD_ID=xxx ROLE_ID=xxx dotnet run --project dusk-role-service
curl 'http://localhost:8080/access?uid=123456789'
```