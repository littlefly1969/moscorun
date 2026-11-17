import { randomBytes, randomUUID, createHash, createPublicKey, verify, createCipheriv, createDecipheriv } from "node:crypto";

const issuer = "https://auth.openai.com";
const resource = "https://api.openai.com/v1";
const planScope = "chatgpt.tokens.use.direct";
const scopes = "openid profile email offline_access resource.invoke " + planScope;
const refreshes = new Map();
const fail = (code) => { throw new Error(code); };
const text = (value) => typeof value === "string" && value.length > 0;
const random = () => randomBytes(32).toString("base64url");
export const newHostId = () => "urn:uuid:" + randomUUID();

function callbackUri(value) {
  const url = new URL(value);
  if (url.protocol !== "http:" || url.hostname !== "127.0.0.1" || !url.port ||
      url.pathname !== "/auth/callback" || url.search || url.hash || url.username || url.password) fail("invalid_loopback_callback");
  return url;
}

export function beginSignIn({ ownerId, hostId, redirectUri, connection, now = Date.now() }) {
  if (!text(ownerId) || !/^urn:uuid:[0-9a-f-]{36}$/i.test(hostId)) fail("invalid_host_or_owner");
  callbackUri(redirectUri);
  if (connection && (connection.ownerId !== ownerId || !text(connection.clientId) || !text(connection.subject))) fail("account_mismatch");
  const pending = { ownerId, hostId, redirectUri, state: random(), nonce: random(), verifier: random(),
    createdAt: now, selected: connection ? { clientId: connection.clientId, subject: connection.subject } : null, used: false };
  const url = new URL(issuer + "/api/accounts/authorize");
  const values = { client_id: connection?.clientId ?? "dynamic_agent_client", ext_agent_host_id: hostId,
    response_type: "code", redirect_uri: redirectUri, scope: scopes, resource,
    state: pending.state, nonce: pending.nonce, code_challenge_method: "S256",
    code_challenge: createHash("sha256").update(pending.verifier).digest("base64url") };
  if (!connection) values.agent_name_hint = "MoscoRun";
  if (connection?.idToken) values.id_token_hint = connection.idToken;
  for (const [key, value] of Object.entries(values)) url.searchParams.set(key, value);
  return { authorizationUrl: url.toString(), pending };
}

async function requestJson(url, init, fetchImpl) {
  let response;
  try { response = await fetchImpl(url, { ...init, redirect: "error", signal: AbortSignal.timeout(30000) }); }
  catch { fail("provider_unavailable"); }
  if (!response.ok) fail("provider_http_" + response.status);
  try { return await response.json(); } catch { fail("invalid_provider_response"); }
}

async function validateIdentity(token, { clientId, nonce, subject, fetchImpl, now }) {
  if (!text(token) || token.length > 32768) fail("invalid_id_token");
  let header, payload, parts;
  try { parts = token.split("."); if (parts.length !== 3) fail("invalid_id_token");
    header = JSON.parse(Buffer.from(parts[0], "base64url")); payload = JSON.parse(Buffer.from(parts[1], "base64url")); }
  catch { fail("invalid_id_token"); }
  if (header.alg !== "RS256" || !text(header.kid)) fail("unsupported_id_token");
  const metadata = await requestJson(issuer + "/.well-known/openid-configuration", {}, fetchImpl);
  let jwksUrl;
  try { jwksUrl = new URL(metadata.jwks_uri); } catch { fail("invalid_discovery"); }
  if (metadata.issuer !== issuer || jwksUrl.origin !== issuer || jwksUrl.username || jwksUrl.password) fail("invalid_discovery");
  const jwks = await requestJson(jwksUrl.toString(), {}, fetchImpl);
  const keys = (jwks.keys ?? []).filter(k => k.kid === header.kid && k.kty === "RSA" && (!k.use || k.use === "sig") && (!k.alg || k.alg === "RS256"));
  if (keys.length !== 1) fail("invalid_signing_key");
  let valid = false;
  try { valid = verify("RSA-SHA256", Buffer.from(parts[0] + "." + parts[1]), createPublicKey({ key: keys[0], format: "jwk" }), Buffer.from(parts[2], "base64url")); }
  catch { fail("invalid_id_signature"); }
  const audiences = Array.isArray(payload.aud) ? payload.aud : [payload.aud];
  if (!valid || payload.iss !== issuer || !audiences.includes(clientId) || !Number.isFinite(payload.exp) ||
      payload.exp * 1000 <= now || (payload.nbf && payload.nbf * 1000 > now + 30000) || !text(payload.sub) ||
      (audiences.length > 1 && payload.azp !== clientId) || (nonce && payload.nonce !== nonce) ||
      (subject && payload.sub !== subject)) fail("invalid_id_claims");
  return payload;
}

function grantedTokens(tokens) {
  const granted = typeof tokens.scope === "string" ? tokens.scope.split(/\s+/).filter(Boolean) : [];
  if (!granted.includes(planScope) || !granted.includes("resource.invoke")) fail("plan_permission_missing");
  if (!text(tokens.access_token) || !Number.isFinite(tokens.expires_in) || tokens.expires_in <= 0 ||
      String(tokens.token_type).toLowerCase() !== "bearer") fail("invalid_tokens");
  return granted;
}

export async function finishSignIn({ ownerId, callbackUrl, pending, fetchImpl = fetch, now = Date.now() }) {
  if (!pending || pending.used || pending.ownerId !== ownerId || now < pending.createdAt ||
      now - pending.createdAt > 600000) fail("invalid_signin_attempt");
  const callback = new URL(callbackUrl), expected = callbackUri(pending.redirectUri);
  if (callback.origin !== expected.origin || callback.pathname !== expected.pathname || callback.hash ||
      callback.searchParams.getAll("state").length !== 1 || callback.searchParams.get("state") !== pending.state) fail("invalid_oauth_state");
  pending.used = true;
  if (callback.searchParams.has("error")) fail("authorization_declined");
  const issued = callback.searchParams.get("client_id") ?? pending.selected?.clientId;
  if (!text(issued) || issued === "dynamic_agent_client" ||
      (pending.selected && issued !== pending.selected.clientId)) fail("invalid_client_registration");
  const code = callback.searchParams.get("code");
  if (!text(code) || callback.searchParams.getAll("code").length !== 1) fail("authorization_code_missing");
  const tokens = await requestJson(issuer + "/api/accounts/oauth/token", {
    method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({ grant_type: "authorization_code", client_id: issued, code,
      code_verifier: pending.verifier, redirect_uri: pending.redirectUri, resource }).toString()
  }, fetchImpl);
  const granted = grantedTokens(tokens);
  const identity = await validateIdentity(tokens.id_token, { clientId: issued, nonce: pending.nonce,
    subject: pending.selected?.subject, fetchImpl, now });
  return { ownerId, clientId: issued, subject: identity.sub, email: identity.email ?? null, hostId: pending.hostId,
    accessToken: tokens.access_token, refreshToken: tokens.refresh_token ?? null, idToken: tokens.id_token,
    scopes: granted, expiresAt: now + tokens.expires_in * 1000 };
}

export async function refreshConnection(connection, { ownerId, save, fetchImpl = fetch, now = Date.now() }) {
  if (connection.ownerId !== ownerId || typeof save !== "function" || !text(connection.refreshToken)) fail("invalid_refresh");
  const key = JSON.stringify([ownerId, connection.clientId, connection.subject]);
  if (refreshes.has(key)) return refreshes.get(key);
  const promise = (async () => {
    const tokens = await requestJson(issuer + "/api/accounts/oauth/token", {
      method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: new URLSearchParams({ grant_type: "refresh_token", client_id: connection.clientId,
        refresh_token: connection.refreshToken, resource }).toString()
    }, fetchImpl);
    if (!text(tokens.scope)) tokens.scope = connection.scopes.join(" ");
    const granted = grantedTokens(tokens);
    if (tokens.id_token) await validateIdentity(tokens.id_token, { clientId: connection.clientId,
      subject: connection.subject, fetchImpl, now });
    const updated = { ...connection, accessToken: tokens.access_token,
      refreshToken: tokens.refresh_token ?? connection.refreshToken, idToken: tokens.id_token ?? connection.idToken,
      scopes: granted, expiresAt: now + tokens.expires_in * 1000 };
    await save(updated);
    return updated;
  })();
  refreshes.set(key, promise);
  try { return await promise; } finally { if (refreshes.get(key) === promise) refreshes.delete(key); }
}

function authorize(connection, ownerId) {
  if (connection.ownerId !== ownerId || !connection.scopes.includes(planScope) ||
      connection.expiresAt <= Date.now()) fail("connection_requires_signin");
}

export async function models(connection, { ownerId, fetchImpl = fetch }) {
  authorize(connection, ownerId);
  const data = await requestJson(resource + "/models", { headers: { Authorization: "Bearer " + connection.accessToken } }, fetchImpl);
  if (!Array.isArray(data.models)) fail("invalid_model_catalog");
  return data.models.filter(m => m.visibility === "list").map(m => ({ slug: m.slug, displayName: m.display_name }));
}

export async function* respond(connection, { ownerId, model, input, instructions, fetchImpl = fetch }) {
  authorize(connection, ownerId);
  if (!text(model) || !Array.isArray(input)) fail("invalid_inference");
  const body = { model, input, store: false, stream: true };
  if (instructions) body.instructions = instructions;
  let response;
  try { response = await fetchImpl(resource + "/responses", { method: "POST", redirect: "error",
    signal: AbortSignal.timeout(180000), headers: { Authorization: "Bearer " + connection.accessToken,
    "Content-Type": "application/json" }, body: JSON.stringify(body) }); }
  catch { fail("provider_unavailable"); }
  if (!response.ok || !response.body) fail("inference_http_" + response.status);
  const decoder = new TextDecoder(); let buffer = "", completed = false;
  for await (const chunk of response.body) {
    buffer = (buffer + decoder.decode(chunk, { stream: true })).replace(/\r\n/g, "\n");
    if (buffer.length > 2 * 1024 * 1024) fail("event_too_large");
    let boundary;
    while ((boundary = buffer.indexOf("\n\n")) >= 0) {
      const block = buffer.slice(0, boundary); buffer = buffer.slice(boundary + 2);
      const data = block.split("\n").filter(l => l.startsWith("data:")).map(l => l.slice(5).trimStart()).join("\n");
      if (!data || data === "[DONE]") continue;
      let event; try { event = JSON.parse(data); } catch { fail("invalid_stream_event"); }
      if (event.type === "response.failed" || event.type === "response.incomplete" || event.type === "error") fail("inference_" + (event.response?.error?.code ?? event.error?.code ?? event.type));
      if (event.type === "response.completed") completed = true;
      if (event.type === "response.output_text.delta") yield { type: "text", text: event.delta };
    }
  }
  if (!completed) fail("inference_interrupted");
  yield { type: "completed" };
}

export function seal(connection, key) {
  if (!Buffer.isBuffer(key) || key.length !== 32 || !text(connection.ownerId)) fail("invalid_encryption_key");
  const iv = randomBytes(12), cipher = createCipheriv("aes-256-gcm", key, iv);
  cipher.setAAD(Buffer.from(connection.ownerId));
  const data = Buffer.concat([cipher.update(JSON.stringify(connection)), cipher.final()]);
  return { version: 1, iv: iv.toString("base64url"), tag: cipher.getAuthTag().toString("base64url"), data: data.toString("base64url") };
}

export function unseal(envelope, key, ownerId) {
  try {
    if (envelope.version !== 1 || !text(ownerId)) fail("invalid_envelope");
    const decipher = createDecipheriv("aes-256-gcm", key, Buffer.from(envelope.iv, "base64url"));
    decipher.setAAD(Buffer.from(ownerId)); decipher.setAuthTag(Buffer.from(envelope.tag, "base64url"));
    const record = JSON.parse(Buffer.concat([decipher.update(Buffer.from(envelope.data, "base64url")), decipher.final()]));
    if (record.ownerId !== ownerId) fail("account_mismatch");
    return record;
  } catch { fail("credential_unavailable"); }
}
