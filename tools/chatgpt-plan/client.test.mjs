import assert from "node:assert/strict";
import { generateKeyPairSync, randomBytes, sign } from "node:crypto";
import { describe, test } from "node:test";
import {
  beginSignIn,
  finishSignIn,
  models,
  newHostId,
  refreshConnection,
  respond,
  seal,
  unseal,
} from "./client.mjs";

const now = Date.now();
const hostId = newHostId();
const ownerId = "athlete-a";
const redirectUri = "http://127.0.0.1:1455/auth/callback";
const flow = () => beginSignIn({ ownerId, hostId, redirectUri, now });

// Provider OIDC simulato con chiave RSA effimera: nessuna chiamata reale, nessun consumo del piano.
const { privateKey, publicKey } = generateKeyPairSync("rsa", { modulusLength: 2048 });
const jwk = { ...publicKey.export({ format: "jwk" }), kid: "test", alg: "RS256", use: "sig" };
const b64 = (value) => Buffer.from(JSON.stringify(value)).toString("base64url");
function jwt(pending, claims = {}, header = { alg: "RS256", kid: "test" }) {
  const data =
    b64(header) +
    "." +
    b64({
      iss: "https://auth.openai.com",
      aud: "oaiapp_test",
      sub: "subject-a",
      exp: now / 1000 + 3600,
      nonce: pending?.nonce,
      ...claims,
    });
  return data + "." + sign("RSA-SHA256", Buffer.from(data), privateKey).toString("base64url");
}
const callback = (a, extra = "&code=test-code&client_id=oaiapp_test") =>
  a.pending.redirectUri + "?state=" + a.pending.state + extra;
function provider(pending, { claims = {}, tokens = {}, discovery = {}, keys = [jwk] } = {}) {
  return async (url, init) => {
    if (url.endsWith("openid-configuration"))
      return Response.json({
        issuer: "https://auth.openai.com",
        jwks_uri: "https://auth.openai.com/jwks",
        ...discovery,
      });
    if (url.endsWith("/jwks")) return Response.json({ keys });
    assert.equal(new URLSearchParams(init.body).get("resource"), "https://api.openai.com/v1");
    return Response.json({
      access_token: "access-test",
      refresh_token: "refresh-test",
      token_type: "Bearer",
      expires_in: 3600,
      scope: "resource.invoke chatgpt.tokens.use.direct",
      id_token: jwt(pending, claims),
      ...tokens,
    });
  };
}
async function signIn(options) {
  const a = flow();
  return finishSignIn({
    ownerId,
    callbackUrl: callback(a),
    pending: a.pending,
    now,
    fetchImpl: provider(a.pending, options),
  });
}
const connection = await signIn();

const stream = (text) => ({
  ok: true,
  body: (async function* () {
    yield new TextEncoder().encode(text);
  })(),
});
const sse = (...events) => events.map((e) => "data: " + JSON.stringify(e) + "\n\n").join("");
async function collect(options) {
  const out = [];
  for await (const e of respond(connection, { ownerId, model: "test-model", input: [], ...options }))
    out.push(e);
  return out;
}

describe("beginSignIn", () => {
  test("solo callback loopback 127.0.0.1 su /auth/callback", () => {
    for (const uri of [
      "https://attacker.test/auth/callback",
      "http://localhost:1455/auth/callback",
      "http://127.0.0.1/auth/callback",
      "http://127.0.0.1:1455/callback",
      "http://127.0.0.1:1455/auth/callback?x=1",
    ])
      assert.throws(() => beginSignIn({ ownerId, hostId, redirectUri: uri }), /invalid_loopback_callback/);
  });
  test("host e owner obbligatori", () => {
    assert.throws(() => beginSignIn({ ownerId, hostId: "host", redirectUri }), /invalid_host_or_owner/);
    assert.throws(() => beginSignIn({ ownerId: "", hostId, redirectUri }), /invalid_host_or_owner/);
  });
  test("prima registrazione: client dinamico, PKCE S256 e nome dell'agente", () => {
    const url = new URL(flow().authorizationUrl);
    assert.equal(url.origin + url.pathname, "https://auth.openai.com/api/accounts/authorize");
    assert.equal(url.searchParams.get("client_id"), "dynamic_agent_client");
    assert.equal(url.searchParams.get("agent_name_hint"), "MoscoRun");
    assert.equal(url.searchParams.get("code_challenge_method"), "S256");
    assert.match(url.searchParams.get("scope"), /chatgpt\.tokens\.use\.direct/);
  });
  test("state e nonce diversi a ogni tentativo", () => {
    const a = flow().pending,
      b = flow().pending;
    assert.notEqual(a.state, b.state);
    assert.notEqual(a.nonce, b.nonce);
  });
  test("riautorizzazione: client registrato e id_token_hint, nessun nome", () => {
    const url = new URL(beginSignIn({ ownerId, hostId, redirectUri, connection }).authorizationUrl);
    assert.equal(url.searchParams.get("client_id"), "oaiapp_test");
    assert.equal(url.searchParams.has("agent_name_hint"), false);
    assert.equal(url.searchParams.get("id_token_hint"), connection.idToken);
  });
  test("riautorizzazione di un altro owner rifiutata", () =>
    assert.throws(
      () => beginSignIn({ ownerId: "athlete-b", hostId, redirectUri, connection }),
      /account_mismatch/,
    ));
});

describe("finishSignIn", () => {
  test("connessione attribuita all'owner con identità verificata", () => {
    assert.equal(connection.subject, "subject-a");
    assert.equal(connection.clientId, "oaiapp_test");
    assert.equal(connection.ownerId, ownerId);
    assert.equal(connection.expiresAt, now + 3600 * 1000);
  });
  test("tentativo usabile una sola volta", async () => {
    const a = flow();
    await finishSignIn({
      ownerId,
      callbackUrl: callback(a),
      pending: a.pending,
      now,
      fetchImpl: provider(a.pending),
    });
    await assert.rejects(
      () => finishSignIn({ ownerId, callbackUrl: callback(a), pending: a.pending, now }),
      /invalid_signin_attempt/,
    );
  });
  test("tentativo scaduto o di un altro owner rifiutato", async () => {
    const a = flow();
    await assert.rejects(
      () => finishSignIn({ ownerId, callbackUrl: callback(a), pending: a.pending, now: now + 600001 }),
      /invalid_signin_attempt/,
    );
    await assert.rejects(
      () => finishSignIn({ ownerId: "athlete-b", callbackUrl: callback(a), pending: a.pending, now }),
      /invalid_signin_attempt/,
    );
  });
  test("state errato, duplicato o callback su altra origine rifiutati", async () => {
    const a = flow();
    const run = (url) => finishSignIn({ ownerId, callbackUrl: url, pending: a.pending, now });
    await assert.rejects(() => run(redirectUri + "?state=wrong&code=c"), /invalid_oauth_state/);
    await assert.rejects(() => run(callback(a, "&state=" + a.pending.state)), /invalid_oauth_state/);
    await assert.rejects(
      () => run("http://127.0.0.1:9999/auth/callback?state=" + a.pending.state),
      /invalid_oauth_state/,
    );
  });
  test("autorizzazione negata", async () => {
    const a = flow();
    await assert.rejects(
      () =>
        finishSignIn({ ownerId, callbackUrl: callback(a, "&error=access_denied"), pending: a.pending, now }),
      /authorization_declined/,
    );
  });
  test("registrazione mancante, dinamica o diversa da quella selezionata", async () => {
    for (const extra of ["&code=c", "&code=c&client_id=dynamic_agent_client"]) {
      const a = flow();
      await assert.rejects(
        () => finishSignIn({ ownerId, callbackUrl: callback(a, extra), pending: a.pending, now }),
        /invalid_client_registration/,
      );
    }
    const re = beginSignIn({ ownerId, hostId, redirectUri, connection, now });
    await assert.rejects(
      () =>
        finishSignIn({
          ownerId,
          callbackUrl: callback(re, "&code=c&client_id=oaiapp_other"),
          pending: re.pending,
          now,
        }),
      /invalid_client_registration/,
    );
  });
  test("codice mancante o duplicato", async () => {
    for (const extra of ["&client_id=oaiapp_test", "&client_id=oaiapp_test&code=a&code=b"]) {
      const a = flow();
      await assert.rejects(
        () => finishSignIn({ ownerId, callbackUrl: callback(a, extra), pending: a.pending, now }),
        /authorization_code_missing/,
      );
    }
  });
  test("claim dell'id token verificati", async () => {
    for (const claims of [
      { nonce: "wrong" },
      { aud: "wrong" },
      { iss: "https://wrong.test" },
      { exp: 1 },
      { nbf: now / 1000 + 3600 },
      { aud: ["oaiapp_test", "other"] },
      { sub: "" },
    ])
      await assert.rejects(() => signIn({ claims }), /invalid_id_claims/, JSON.stringify(claims));
    assert.equal(
      (await signIn({ claims: { aud: ["oaiapp_test", "x"], azp: "oaiapp_test" } })).subject,
      "subject-a",
    );
  });
  test("firma, chiave e discovery verificate", async () => {
    await assert.rejects(() => signIn({ keys: [] }), /invalid_signing_key/);
    await assert.rejects(() => signIn({ keys: [{ ...jwk, alg: "RS512" }] }), /invalid_signing_key/);
    const other = generateKeyPairSync("rsa", { modulusLength: 2048 }).publicKey.export({ format: "jwk" });
    await assert.rejects(() => signIn({ keys: [{ ...other, kid: "test" }] }), /invalid_id_claims/);
    await assert.rejects(
      () => signIn({ keys: [{ kid: "test", kty: "RSA", n: "x" }] }),
      /invalid_id_signature/,
    );
    await assert.rejects(
      () => signIn({ discovery: { jwks_uri: "https://evil.test/jwks" } }),
      /invalid_discovery/,
    );
    await assert.rejects(() => signIn({ discovery: { jwks_uri: "not a url" } }), /invalid_discovery/);
    await assert.rejects(() => signIn({ discovery: { issuer: "https://evil.test" } }), /invalid_discovery/);
  });
  test("id token malformato o con algoritmo non supportato", async () => {
    await assert.rejects(() => signIn({ tokens: { id_token: "a.b" } }), /invalid_id_token/);
    await assert.rejects(() => signIn({ tokens: { id_token: "x.y.z" } }), /invalid_id_token/);
    await assert.rejects(() => signIn({ tokens: { id_token: undefined } }), /invalid_id_token/);
    await assert.rejects(
      () => signIn({ tokens: { id_token: b64({ alg: "none", kid: "test" }) + "." + b64({}) + ".x" } }),
      /unsupported_id_token/,
    );
  });
  test("permesso del piano e token obbligatori", async () => {
    await assert.rejects(() => signIn({ tokens: { scope: "openid" } }), /plan_permission_missing/);
    await assert.rejects(() => signIn({ tokens: { token_type: "mac" } }), /invalid_tokens/);
    await assert.rejects(() => signIn({ tokens: { expires_in: 0 } }), /invalid_tokens/);
  });
  test("provider irraggiungibile o con risposta non valida", async () => {
    const a = flow();
    await assert.rejects(
      () =>
        finishSignIn({
          ownerId,
          callbackUrl: callback(a),
          pending: a.pending,
          now,
          fetchImpl: async () => {
            throw new TypeError("network");
          },
        }),
      /provider_unavailable/,
    );
    for (const response of [new Response("", { status: 500 }), new Response("not json")]) {
      const b = flow();
      await assert.rejects(
        () =>
          finishSignIn({
            ownerId,
            callbackUrl: callback(b),
            pending: b.pending,
            now,
            fetchImpl: async () => response,
          }),
        /provider_http_500|invalid_provider_response/,
      );
    }
  });
});

describe("refreshConnection", () => {
  test("rinnovi concorrenti nello stesso processo: una chiamata e un salvataggio", async () => {
    let calls = 0,
      saves = 0;
    const fetchImpl = async () => {
      calls++;
      await new Promise((r) => setTimeout(r, 5));
      return Response.json({
        access_token: "new",
        refresh_token: "rotated",
        token_type: "Bearer",
        expires_in: 3600,
      });
    };
    const save = async (c) => {
      assert.equal(c.refreshToken, "rotated");
      saves++;
    };
    const results = await Promise.all(
      [1, 2].map(() => refreshConnection(connection, { ownerId, save, fetchImpl })),
    );
    assert.equal(calls, 1);
    assert.equal(saves, 1);
    assert.equal(results[0], results[1]);
  });
  test("nuovo id token verificato sul medesimo soggetto", async () => {
    const fetchImpl = provider(null, { claims: { sub: "subject-b" } });
    await assert.rejects(
      () => refreshConnection(connection, { ownerId, save: async () => {}, fetchImpl, now }),
      /invalid_id_claims/,
    );
    const same = await refreshConnection(connection, {
      ownerId,
      save: async () => {},
      fetchImpl: provider(null),
      now,
    });
    assert.equal(same.accessToken, "access-test");
  });
  test("senza refresh token, salvataggio o owner corretto rifiutato", async () => {
    await assert.rejects(
      () => refreshConnection(connection, { ownerId: "x", save: async () => {} }),
      /invalid_refresh/,
    );
    await assert.rejects(() => refreshConnection(connection, { ownerId }), /invalid_refresh/);
    await assert.rejects(
      () => refreshConnection({ ...connection, refreshToken: null }, { ownerId, save: async () => {} }),
      /invalid_refresh/,
    );
  });
});

describe("models", () => {
  test("solo i modelli visibili dell'account", async () => {
    const catalog = await models(connection, {
      ownerId,
      fetchImpl: async () =>
        Response.json({
          models: [
            { slug: "test-model", visibility: "list", display_name: "Test" },
            { slug: "hidden", visibility: "hidden" },
          ],
        }),
    });
    assert.deepEqual(catalog, [{ slug: "test-model", displayName: "Test" }]);
  });
  test("catalogo non valido, connessione scaduta o di un altro owner", async () => {
    await assert.rejects(
      () => models(connection, { ownerId, fetchImpl: async () => Response.json({}) }),
      /invalid_model_catalog/,
    );
    await assert.rejects(
      () => models({ ...connection, expiresAt: 0 }, { ownerId }),
      /connection_requires_signin/,
    );
    await assert.rejects(() => models(connection, { ownerId: "x" }), /connection_requires_signin/);
  });
});

describe("respond", () => {
  test("richiesta conforme alla preview e testo fino al completamento", async () => {
    const out = await collect({
      instructions: "Sei un allenatore.",
      fetchImpl: async (_url, init) => {
        const body = JSON.parse(init.body);
        assert.equal(body.store, false);
        assert.equal(body.stream, true);
        assert.equal(body.instructions, "Sei un allenatore.");
        assert.equal("previous_response_id" in body, false);
        assert.equal("tools" in body, false);
        return stream(
          "data: [DONE]\r\n\r\n: commento\n\n" +
            sse({ type: "response.output_text.delta", delta: "ok" }, { type: "response.completed" }),
        );
      },
    });
    assert.deepEqual(out, [{ type: "text", text: "ok" }, { type: "completed" }]);
  });
  test("chiamate a strumenti function restituite al chiamante", async () => {
    const tools = [{ type: "function", name: "lastWorkout", parameters: { type: "object", properties: {} } }];
    const out = await collect({
      tools,
      toolChoice: "auto",
      fetchImpl: async (_url, init) => {
        const body = JSON.parse(init.body);
        assert.deepEqual(body.tools, tools);
        assert.equal(body.tool_choice, "auto");
        return stream(
          sse(
            { type: "response.output_item.done", item: { type: "message" } },
            {
              type: "response.output_item.done",
              item: { type: "function_call", call_id: "c1", name: "lastWorkout", arguments: "{}" },
            },
            { type: "response.completed" },
          ),
        );
      },
    });
    assert.deepEqual(out[0], { type: "tool_call", callId: "c1", name: "lastWorkout", arguments: "{}" });
  });
  test("strumenti ospitati non supportati dalla preview rifiutati prima della chiamata", async () => {
    for (const tools of [[{ type: "image_generation" }], [{ type: "file_search" }], [null], "function"])
      await assert.rejects(
        () => collect({ tools, fetchImpl: async () => assert.fail("chiamata inattesa") }),
        /unsupported_tool/,
      );
  });
  test("flusso interrotto, errore del provider o evento non valido", async () => {
    await assert.rejects(
      () =>
        collect({
          fetchImpl: async () => stream(sse({ type: "response.output_text.delta", delta: "partial" })),
        }),
      /inference_interrupted/,
    );
    await assert.rejects(
      () =>
        collect({
          fetchImpl: async () =>
            stream(
              sse({
                type: "response.failed",
                response: { error: { code: "subscription_sharing_usage_limit_exceeded" } },
              }),
            ),
        }),
      /inference_subscription_sharing_usage_limit_exceeded/,
    );
    await assert.rejects(
      () => collect({ fetchImpl: async () => stream(sse({ type: "error" })) }),
      /inference_error/,
    );
    await assert.rejects(
      () => collect({ fetchImpl: async () => stream("data: {x\n\n") }),
      /invalid_stream_event/,
    );
    await assert.rejects(
      () => collect({ fetchImpl: async () => stream("data: " + "x".repeat(2 * 1024 * 1024)) }),
      /event_too_large/,
    );
  });
  test("connessione caduta a metà risposta", async () => {
    const body = (async function* () {
      yield new TextEncoder().encode(sse({ type: "response.output_text.delta", delta: "a" }));
      throw new TypeError("terminated");
    })();
    await assert.rejects(
      () => collect({ fetchImpl: async () => ({ ok: true, body }) }),
      /inference_interrupted/,
    );
  });
  test("annullamento del chiamante", async () => {
    const controller = new AbortController();
    const body = (async function* () {
      yield new TextEncoder().encode(sse({ type: "response.output_text.delta", delta: "a" }));
      controller.abort();
      throw new DOMException("aborted", "AbortError");
    })();
    await assert.rejects(
      () => collect({ signal: controller.signal, fetchImpl: async () => ({ ok: true, body }) }),
      /inference_cancelled/,
    );
    const aborted = AbortSignal.abort();
    await assert.rejects(
      () =>
        collect({
          signal: aborted,
          fetchImpl: async (_url, init) => {
            assert.equal(init.signal.aborted, true);
            throw new DOMException("aborted", "AbortError");
          },
        }),
      /inference_cancelled/,
    );
  });
  test("errori HTTP e parametri non validi", async () => {
    await assert.rejects(
      () => collect({ fetchImpl: async () => ({ ok: false, status: 429 }) }),
      /inference_http_429/,
    );
    await assert.rejects(
      () =>
        collect({
          fetchImpl: async () => {
            throw new TypeError("network");
          },
        }),
      /provider_unavailable/,
    );
    await assert.rejects(() => collect({ model: "" }), /invalid_inference/);
    await assert.rejects(() => collect({ input: "ciao" }), /invalid_inference/);
  });
  test("errori non legati al flusso propagati", async () => {
    const body = (async function* () {
      yield new TextEncoder().encode("");
      throw new RangeError("bug");
    })();
    await assert.rejects(() => collect({ fetchImpl: async () => ({ ok: true, body }) }), RangeError);
  });
});

describe("seal e unseal", () => {
  const key = randomBytes(32);
  const envelope = seal(connection, key);
  test("cifratura autenticata e legata all'owner", () => {
    assert.deepEqual(unseal(envelope, key, ownerId), connection);
    assert.equal(JSON.stringify(envelope).includes("access-test"), false);
    assert.throws(() => unseal(envelope, key, "athlete-b"), /credential_unavailable/);
  });
  test("tag troncato, IV non valido, chiave errata o versione sconosciuta rifiutati", () => {
    const tag = Buffer.from(envelope.tag, "base64url").subarray(0, 4).toString("base64url");
    for (const [env, k] of [
      [{ ...envelope, tag }, key],
      [{ ...envelope, iv: Buffer.alloc(8).toString("base64url") }, key],
      [envelope, randomBytes(32)],
      [{ ...envelope, version: 2 }, key],
      [envelope, randomBytes(16)],
    ])
      assert.throws(() => unseal(env, k, ownerId), /credential_unavailable/);
  });
  test("chiave non valida in cifratura", () => {
    assert.throws(() => seal(connection, randomBytes(16)), /invalid_encryption_key/);
    assert.throws(() => seal({ ...connection, ownerId: "" }, key), /invalid_encryption_key/);
  });
});
