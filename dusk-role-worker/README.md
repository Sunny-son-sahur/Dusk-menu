# dusk-role-worker (Cloudflare Workers)

Free, always-warm, no-card-needed version of the role service. Same API
contract as the Railway edition:

```
GET /health                -> { ok: true }
GET /access?uid=123456789  -> { allowed, inServer, hasRole, pending, roleName, reason }
```

## deploy (5 minutes, all in the browser)

1. Go to <https://workers.cloudflare.com> and create a free account.
2. **Create Worker** -> name it `dusk-role-service`.
3. Delete the demo code, paste in all of `worker.js`, click **Deploy**.
4. Open **Settings -> Variables and Secrets** and add:

   | name         | value                    | type           |
   |--------------|--------------------------|----------------|
   | `BOT_TOKEN`  | your Discord bot token   | **Secret**     |
   | `GUILD_ID`   | `1543035598736986184`    | plain text     |
   | `ROLE_ID`    | `1543067837638254592`    | plain text     |

5. **Deploy** again (env changes need a redeploy).
6. Copy the `.workers.dev` URL and open `<url>/health` in a browser —
   you should see `{"ok":true}`.
7. Paste the URL into
   `DuskLauncher/src/Dusk.Launcher/Auth/AccessGate.cs` at
   `RoleServiceUrl`, commit, push.

## test a real check

Open `<url>/access?uid=<any discord user id>` in a browser:

- member with the role        -> `{ "allowed": true,  ... }`
- member without the role     -> `{ "allowed": false, "reason": "missing_role" }`
- not a member                -> `{ "allowed": false, "reason": "not_in_server" }`

(you can use your own user id for the test — right-click your name in a
channel -> Copy User ID, Developer Mode on.)

## why not Railway?

Railway's free tier is gone (~$5/mo hobby plan). This worker does the same
job: the launcher calls `RoleServiceUrl/access?uid=...`, the worker checks
the member's roles over the Discord bot API, and answers allow/deny.
Cloudflare's free plan is 100k requests/day — your server will never get
close.