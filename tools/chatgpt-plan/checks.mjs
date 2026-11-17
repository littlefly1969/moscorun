import assert from "node:assert/strict";
import { generateKeyPairSync, sign, randomBytes } from "node:crypto";
import { beginSignIn, finishSignIn, newHostId, refreshConnection, models, respond, seal, unseal } from "./client.mjs";

export async function runChecks() {
  let checks = 0; const ok = (f) => { f(); checks++; };
  const now = Date.now(), hostId = newHostId(), ownerId = "athlete-a";
  const flow = () => beginSignIn({ ownerId, hostId, redirectUri: "http://127.0.0.1:1455/auth/callback", now });
  ok(() => assert.throws(() => beginSignIn({ ownerId, hostId, redirectUri: "https://attacker.test/auth/callback" })));
  const attempt = flow(), url = new URL(attempt.authorizationUrl);
  ok(() => assert.equal(url.searchParams.get("client_id"), "dynamic_agent_client"));
  ok(() => assert.equal(url.searchParams.get("agent_name_hint"), "MoscoRun"));
  ok(() => assert.notEqual(flow().pending.state, attempt.pending.state));
  const { privateKey, publicKey } = generateKeyPairSync("rsa", { modulusLength: 2048 });
  const jwk = { ...publicKey.export({ format: "jwk" }), kid: "test", alg: "RS256", use: "sig" };
  function jwt(pending, changes = {}) {
    const head = Buffer.from(JSON.stringify({ alg: "RS256", kid: "test" })).toString("base64url");
    const body = Buffer.from(JSON.stringify({ iss: "https://auth.openai.com", aud: "oaiapp_test", sub: "subject-a",
      exp: now / 1000 + 3600, nonce: pending.nonce, ...changes })).toString("base64url");
    const data = head + "." + body;
    return data + "." + sign("RSA-SHA256", Buffer.from(data), privateKey).toString("base64url");
  }
  const callback = (a) => a.pending.redirectUri + "?state=" + a.pending.state + "&code=test-code&client_id=oaiapp_test";
  function mock(pending, changes = {}, tokenChanges = {}) {
    return async (url, init) => {
      if (url.endsWith("openid-configuration")) return Response.json({ issuer: "https://auth.openai.com", jwks_uri: "https://auth.openai.com/jwks" });
      if (url.endsWith("/jwks")) return Response.json({ keys: [jwk] });
      assert.equal(new URLSearchParams(init.body).get("resource"), "https://api.openai.com/v1");
      return Response.json({ access_token: "access-test", refresh_token: "refresh-test", token_type: "Bearer",
        expires_in: 3600, scope: "resource.invoke chatgpt.tokens.use.direct", id_token: jwt(pending, changes), ...tokenChanges });
    };
  }
  const connection = await finishSignIn({ ownerId, callbackUrl: callback(attempt), pending: attempt.pending, now, fetchImpl: mock(attempt.pending) }); checks++;
  ok(() => assert.equal(connection.subject, "subject-a"));
  await assert.rejects(() => finishSignIn({ ownerId, callbackUrl: callback(attempt), pending: attempt.pending })); checks++;
  for (const changes of [{ nonce: "wrong" }, { aud: "wrong" }, { iss: "https://wrong.test" }, { exp: 1 }]) {
    const a = flow(); await assert.rejects(() => finishSignIn({ ownerId, callbackUrl: callback(a), pending: a.pending, now, fetchImpl: mock(a.pending, changes) })); checks++;
  }
  const wrong = flow(); await assert.rejects(() => finishSignIn({ ownerId: "athlete-b", callbackUrl: callback(wrong), pending: wrong.pending })); checks++;
  const noPlan = flow(); await assert.rejects(() => finishSignIn({ ownerId, callbackUrl: callback(noPlan), pending: noPlan.pending, now, fetchImpl: mock(noPlan.pending, {}, { scope: "openid" }) })); checks++;
  const denied = flow(); await assert.rejects(() => finishSignIn({ ownerId, callbackUrl: denied.pending.redirectUri + "?state=" + denied.pending.state + "&error=access_denied", pending: denied.pending })); checks++;
  const reauth = beginSignIn({ ownerId, hostId, redirectUri: connection.hostId ? "http://127.0.0.1:1455/auth/callback" : "", connection });
  ok(() => assert.equal(new URL(reauth.authorizationUrl).searchParams.has("agent_name_hint"), false));
  const key = randomBytes(32), encrypted = seal(connection, key);
  ok(() => assert.deepEqual(unseal(encrypted, key, ownerId), connection));
  ok(() => assert.throws(() => unseal(encrypted, key, "athlete-b")));
  ok(() => assert.equal(JSON.stringify(encrypted).includes("access-test"), false));
  let calls = 0, saves = 0;
  const refresh = async () => { calls++; await new Promise(r => setTimeout(r, 5)); return Response.json({
    access_token: "new", refresh_token: "rotated", token_type: "Bearer", expires_in: 3600 }); };
  const results = await Promise.all([1, 2].map(() => refreshConnection(connection, { ownerId, save: async c => { assert.equal(c.refreshToken, "rotated"); saves++; }, fetchImpl: refresh })));
  ok(() => assert.equal(calls, 1)); ok(() => assert.equal(saves, 1)); ok(() => assert.equal(results[0], results[1]));
  const catalog = await models(connection, { ownerId, fetchImpl: async () => Response.json({ models: [{ slug: "test-model", visibility: "list", display_name: "Test" }, { slug: "hidden", visibility: "hidden" }] }) });
  ok(() => assert.equal(catalog.length, 1));
  const events = async function*(text) { yield new TextEncoder().encode(text); };
  const success = async (url, init) => { const b = JSON.parse(init.body); assert.equal(b.store, false); assert.equal(b.stream, true);
    assert.equal("previous_response_id" in b, false); return { ok: true, body: events('data: {"type":"response.output_text.delta","delta":"ok"}\n\ndata: {"type":"response.completed"}\n\n') }; };
  const outputs = []; for await (const e of respond(connection, { ownerId, model: "test-model", input: [], fetchImpl: success })) outputs.push(e);
  ok(() => assert.equal(outputs.at(-1).type, "completed"));
  await assert.rejects(async () => { for await (const e of respond(connection, { ownerId, model: "test-model", input: [],
    fetchImpl: async () => ({ ok: true, body: events('data: {"type":"response.output_text.delta","delta":"partial"}\n\n') }) })) void e; }); checks++;
  await assert.rejects(async () => { for await (const e of respond(connection, { ownerId, model: "test-model", input: [],
    fetchImpl: async () => ({ ok: true, body: events('data: {"type":"response.failed","response":{"error":{"code":"subscription_sharing_usage_limit_exceeded"}}}\n\n') }) })) void e; }); checks++;
  ok(() => assert.throws(() => unseal({...encrypted,tag:Buffer.from(encrypted.tag,"base64url").subarray(0,4).toString("base64url")},key,ownerId)));
  ok(() => assert.throws(() => unseal({...encrypted,iv:Buffer.alloc(8).toString("base64url")},key,ownerId)));
  ok(() => assert.throws(() => unseal(encrypted,randomBytes(32),ownerId)));
  return { checks, status: "passed" };
}
