// Dusk Role Service — Cloudflare Worker edition.
//
// Same contract as the Railway version so the launcher doesn't care which
// one is behind RoleServiceUrl:
//   GET /health                -> { ok: true }
//   GET /access?uid=123456789  -> { allowed, inServer, hasRole, pending, roleName, reason }
//
// Env vars / secrets (Workers Dashboard -> Settings -> Variables):
//   BOT_TOKEN  - Discord bot token       (add as a SECRET)
//   GUILD_ID   - the Dusk server id
//   ROLE_ID    - the role that unlocks the launcher

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    if (url.pathname === "/health") {
      return json({ ok: true });
    }

    if (url.pathname === "/access") {
      const uid = url.searchParams.get("uid");
      if (!uid) return json({ allowed: false, reason: "missing_uid" }, 400);

      if (!env.BOT_TOKEN || !env.GUILD_ID || !env.ROLE_ID) {
        return json({ allowed: false, reason: "not_configured" }, 500);
      }

      const auth = { Authorization: `Bot ${env.BOT_TOKEN}` };

      // 404 = user is not a member of the guild at all.
      const res = await fetch(
        `https://discord.com/api/v10/guilds/${env.GUILD_ID}/members/${uid}`,
        { headers: auth }
      );

      if (res.status === 404) {
        return json({
          allowed: false, inServer: false, hasRole: false,
          pending: false, roleName: null, reason: "not_in_server",
        });
      }
      if (!res.ok) {
        return json({ allowed: false, reason: `discord_error_${res.status}` }, 502);
      }

      const member = await res.json();
      const pending = !!member.pending;
      const hasRole = (member.roles || []).includes(env.ROLE_ID);
      const roleName = await getRoleName(env);
      const allowed = hasRole && !pending;

      return json({
        allowed, inServer: true, hasRole, pending, roleName,
        reason: allowed ? "allowed" : pending ? "pending" : "missing_role",
      });
    }

    return json({ error: "not_found" }, 404);
  },
};

async function getRoleName(env) {
  try {
    const res = await fetch(
      `https://discord.com/api/v10/guilds/${env.GUILD_ID}/roles`,
      { headers: { Authorization: `Bot ${env.BOT_TOKEN}` } }
    );
    if (!res.ok) return null;
    const roles = await res.json();
    const role = roles.find((r) => r.id === env.ROLE_ID);
    return role ? role.name : null;
  } catch {
    return null;
  }
}

function json(body, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json", "Access-Control-Allow-Origin": "*" },
  });
}