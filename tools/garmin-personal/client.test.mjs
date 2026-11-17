import assert from "node:assert/strict";
import { describe, test } from "node:test";
import * as fitSdk from "@garmin/fitsdk";
import { GarminError, GarminPersonalClient, activityBundle, decodeFit, healthCatalog } from "./client.mjs";

const tokenPath = "personal-api-key/token";
const ok = (body) => Response.json(body);

/** Client con provider simulato: il token è sempre valido, `api` risponde alle altre richieste. */
function clientWith(api, options = {}) {
  const calls = [];
  const fetchImpl = async (url, init) => {
    calls.push({ url, init });
    if (url.includes(tokenPath)) return ok({ access_token: "access", expires_in: 86400 });
    return api(url, init);
  };
  const client = new GarminPersonalClient({ ownerId: "a", apiKey: "test-key", fetchImpl, ...options });
  return { client, calls };
}

async function collect(iterable) {
  const rows = [];
  for await (const row of iterable) rows.push(row);
  return rows;
}

const window = { ownerId: "a", startDate: "2026-01-01", endDate: "2026-01-02" };

describe("costruzione e token", () => {
  test("owner e chiave obbligatori", () => {
    assert.throws(() => new GarminPersonalClient({ ownerId: "", apiKey: "k" }), /connection_required/);
    assert.throws(() => new GarminPersonalClient({ ownerId: "a", apiKey: " " }), /connection_required/);
  });

  test("chiave inviata solo allo scambio del token, poi Bearer su host ufficiale", async () => {
    let tokens = 0;
    const responses = [[{ activityId: 1 }, { activityId: 2 }], [{ activityId: 2 }, { activityId: 3 }], []];
    const calls = [];
    const fetchImpl = async (url, init) => {
      calls.push(url);
      if (url.includes(tokenPath)) {
        tokens++;
        assert.equal(init.method, "POST");
        assert.equal(init.headers.Authorization, "ApiKey test-key");
        return ok({ access_token: "test-access", expires_in: 86400 });
      }
      assert.equal(init.headers.Authorization, "Bearer test-access");
      assert.equal(init.redirect, "error");
      return ok(responses[Number(new URL(url).searchParams.get("start")) / 2]);
    };
    const client = new GarminPersonalClient({ ownerId: "a", apiKey: "test-key", fetchImpl });
    const rows = await collect(client.activities({ ...window, pageSize: 2 }));
    assert.deepEqual(
      rows.map((x) => x.activityId),
      [1, 2, 3],
    );
    assert.equal(tokens, 1);
    assert.ok(calls.every((u) => u.startsWith("https://personal.connectapi.garmin.com/")));
  });

  test("token rinnovato in prossimità della scadenza e richieste concorrenti condividono lo scambio", async () => {
    let now = 0,
      tokens = 0;
    const fetchImpl = async (url) => {
      if (url.includes(tokenPath)) {
        tokens++;
        await new Promise((r) => setTimeout(r, 2));
        return ok({ access_token: "t" + tokens, expires_in: 3600 });
      }
      return ok({});
    };
    const client = new GarminPersonalClient({ ownerId: "a", apiKey: "k", fetchImpl, now: () => now });
    await Promise.all([client.profile("a"), client.profile("a")]);
    assert.equal(tokens, 1);
    now = 3600 * 1000 - 30000;
    await client.profile("a");
    assert.equal(tokens, 2);
  });

  test("scadenza assente: 24 ore; scadenza non valida rifiutata", async () => {
    const make = (body) =>
      new GarminPersonalClient({
        ownerId: "a",
        apiKey: "k",
        fetchImpl: async (url) => (url.includes(tokenPath) ? ok(body) : ok({})),
      });
    assert.deepEqual(await make({ access_token: "t" }).profile("a"), {});
    await assert.rejects(
      () => make({ access_token: "t", expires_in: -1 }).profile("a"),
      /invalid_token_expiry/,
    );
    await assert.rejects(() => make({}).profile("a"), /invalid_token_response/);
  });

  test("errori dello scambio del token", async () => {
    const make = (response) =>
      new GarminPersonalClient({ ownerId: "a", apiKey: "k", fetchImpl: async () => response() });
    await assert.rejects(
      () => make(() => new Response("", { status: 401 })).profile("a"),
      /reauthorization_required/,
    );
    await assert.rejects(
      () => make(() => new Response("", { status: 500 })).profile("a"),
      /token_request_failed/,
    );
    await assert.rejects(() => make(() => new Response("not json")).profile("a"), /invalid_token_response/);
    await assert.rejects(
      () =>
        new GarminPersonalClient({
          ownerId: "a",
          apiKey: "k",
          fetchImpl: async () => {
            throw new TypeError("network");
          },
        }).profile("a"),
      /provider_unavailable/,
    );
  });
});

describe("richieste", () => {
  test("owner diverso e id non validi rifiutati", async () => {
    const { client } = clientWith(async () => ok({}));
    await assert.rejects(() => client.detail(1, "b"), /owner_mismatch/);
    assert.throws(
      () => client.detail("../bad", "a"),
      (e) => e instanceof GarminError && e.message === "invalid_activity_id",
    );
    assert.throws(() => client.detail(Number.MAX_SAFE_INTEGER + 1, "a"), /unsafe_activity_id/);
  });

  test("403 richiede nuova autorizzazione, altri errori HTTP generici", async () => {
    await assert.rejects(
      () => clientWith(async () => new Response("private failure", { status: 403 })).client.detail(1, "a"),
      (e) => e.message === "reauthorization_required" && e.status === 403,
    );
    await assert.rejects(
      () => clientWith(async () => new Response("", { status: 404 })).client.detail(1, "a"),
      /provider_http_error/,
    );
  });

  test("401 rinnova il token una sola volta", async () => {
    let tokens = 0,
      hits = 0;
    const fetchImpl = async (url) => {
      if (url.includes(tokenPath)) return ok({ access_token: "t" + ++tokens });
      return ++hits === 1 ? new Response("", { status: 401 }) : ok({ activityId: 1 });
    };
    const client = new GarminPersonalClient({ ownerId: "a", apiKey: "k", fetchImpl });
    assert.equal((await client.detail(1, "a")).activityId, 1);
    assert.equal(tokens, 2);
    const always = new GarminPersonalClient({
      ownerId: "a",
      apiKey: "k",
      fetchImpl: async (url) =>
        url.includes(tokenPath) ? ok({ access_token: "t" }) : new Response("", { status: 401 }),
    });
    await assert.rejects(() => always.detail(1, "a"), /reauthorization_required/);
  });

  test("429 e 5xx rispettano Retry-After in secondi o come data", async () => {
    const waits = [];
    let tries = 0;
    const now = Date.parse("2026-11-17T10:00:00Z");
    const { client } = clientWith(
      async () => {
        tries++;
        if (tries === 1) return new Response("", { status: 429, headers: { "Retry-After": "2" } });
        if (tries === 2)
          return new Response("", {
            status: 503,
            headers: { "Retry-After": "Tue, 17 Nov 2026 10:00:05 GMT" },
          });
        return ok({ activityId: 1 });
      },
      { sleep: async (ms) => waits.push(ms), now: () => now },
    );
    assert.equal((await client.detail(1, "a")).activityId, 1);
    assert.deepEqual(waits, [2000, 5000]);
  });

  test("attesa oltre 60 secondi o tentativi esauriti", async () => {
    const slow = clientWith(
      async () => new Response("", { status: 429, headers: { "Retry-After": "120" } }),
      {
        sleep: async () => {},
      },
    );
    await assert.rejects(
      () => slow.client.detail(1, "a"),
      (e) => e.message === "retry_later" && e.retryAfterSeconds === 120,
    );
    const busy = clientWith(async () => new Response("", { status: 502 }), { sleep: async () => {} });
    await assert.rejects(() => busy.client.detail(1, "a"), /provider_http_error/);
  });

  test("tentativi esauriti dopo rinnovo e attese", async () => {
    let hits = 0;
    const { client } = clientWith(async () => new Response("", { status: ++hits <= 2 ? 429 : 401 }), {
      sleep: async () => {},
    });
    await assert.rejects(() => client.detail(1, "a"), /retry_exhausted/);
  });

  test("risposte non JSON, vuote o troppo grandi rifiutate", async () => {
    await assert.rejects(
      () => clientWith(async () => new Response("<html>")).client.detail(1, "a"),
      /invalid_json/,
    );
    await assert.rejects(
      () => clientWith(async () => new Response(null)).client.detail(1, "a"),
      /empty_response/,
    );
    const big = new Uint8Array(32 * 1024 * 1024 + 1);
    await assert.rejects(
      () => clientWith(async () => new Response(big)).client.original(1, "a"),
      /response_too_large/,
    );
  });

  test("download originale binario con tipo di contenuto", async () => {
    const { client, calls } = clientWith(
      async () =>
        new Response(new Uint8Array([1, 2]), { headers: { "Content-Type": "application/octet-stream" } }),
    );
    const file = await client.original(7, "a");
    assert.deepEqual([...file.bytes], [1, 2]);
    assert.equal(file.contentType, "application/octet-stream");
    assert.equal(calls.at(-1).init.headers.Accept, "application/octet-stream");
  });
});

describe("attività", () => {
  const pages = (rows) => clientWith(async () => ok(rows)).client;

  test("pagina ripetuta e lettura incompleta segnalate", async () => {
    await assert.rejects(
      () => collect(pages([{ activityId: 1 }]).activities({ ...window, pageSize: 1 })),
      /repeated_page/,
    );
    let n = 0;
    const growing = clientWith(async () => ok([{ activityId: ++n }])).client;
    await assert.rejects(
      () => collect(growing.activities({ ...window, pageSize: 1, maxPages: 2 })),
      /pagination_incomplete/,
    );
  });

  test("pagina non valida o più lunga del limite", async () => {
    await assert.rejects(() => collect(pages({}).activities(window)), /invalid_activity_page/);
    await assert.rejects(
      () => collect(pages([{ activityId: 1 }, { activityId: 2 }]).activities({ ...window, pageSize: 1 })),
      /invalid_activity_page/,
    );
  });

  test("finestra e parametri controllati", async () => {
    const client = pages([]);
    for (const bad of [
      { ...window, startDate: "2026-1-1" },
      { ...window, startDate: "2026-02-01" },
      { ...window, pageSize: 100 },
      { ...window, maxPages: 0 },
    ])
      await assert.rejects(() => collect(client.activities(bad)), /invalid_window/);
  });

  test("ultima pagina corta termina senza ulteriori richieste", async () => {
    const { client, calls } = clientWith(async () => ok([{ activityId: 1 }]));
    assert.equal((await collect(client.activities({ ...window, pageSize: 2 }))).length, 1);
    assert.equal(calls.filter((c) => !c.url.includes(tokenPath)).length, 1);
  });
});

describe("salute", () => {
  test("catalogo dei 19 endpoint ufficiali", () => assert.equal(Object.keys(healthCatalog).length, 19));

  test("solo endpoint e parametri del catalogo", () => {
    const { client } = clientWith(async () => ok({}));
    assert.throws(() => client.readHealth("https://attacker.test", {}, "a"), /unsupported_health_endpoint/);
    assert.throws(
      () => client.readHealth("/hrv-service/hrv/{calendarDate}", { calendarDate: "2026-01-01", x: 1 }, "a"),
      /unsupported_health_parameter/,
    );
    assert.throws(
      () => client.readHealth("/hrv-service/hrv/{calendarDate}", { calendarDate: "../x" }, "a"),
      /invalid_health_path_parameter/,
    );
  });

  test("percorso e query costruiti dal catalogo", async () => {
    const { client, calls } = clientWith(async () => ok({ ok: true }));
    await client.readHealth(
      "/metrics-service/metrics/maxmet/daily/{startDate}/{endDate}",
      { startDate: "2026-01-01", endDate: "2026-01-31", sport: "running" },
      "a",
    );
    const url = new URL(calls.at(-1).url);
    assert.equal(url.pathname, "/metrics-service/metrics/maxmet/daily/2026-01-01/2026-01-31");
    assert.equal(url.searchParams.get("sport"), "running");
    assert.equal((await client.readHealth("/biometric-service/heartRateZones/v2", {}, "a")).ok, true);
  });

  test("scorciatoie giornaliere", async () => {
    const { client, calls } = clientWith(async () => ok({}));
    for (const kind of ["hrv", "sleep", "nutrition", "daily", "heartRate"])
      await client.health(kind, "2026-01-01", "a");
    assert.deepEqual(
      calls.filter((c) => !c.url.includes(tokenPath)).map((c) => new URL(c.url).pathname),
      [
        "/hrv-service/hrv/2026-01-01",
        "/sleep-service/sleep/dailySleep",
        "/nutrition-service/food/logs/2026-01-01",
        "/usersummary-service/usersummary/daily",
        "/wellness-service/wellness/dailyHeartRate",
      ],
    );
    assert.throws(() => client.health("hrv", "ieri", "a"), /invalid_date/);
    assert.throws(() => client.health("toString", "2026-01-01", "a"), /unsupported_health_category/);
  });
});

describe("FIT", () => {
  const minimal = new Uint8Array([
    0x0e, 0x10, 0xd9, 0x07, 0, 0, 0, 0, 0x2e, 0x46, 0x49, 0x54, 0x91, 0x33, 0, 0,
  ]);

  test("file minimo ufficiale decodificato con l'SDK Garmin", () => {
    assert.equal(typeof decodeFit(minimal, fitSdk), "object");
  });

  test("integrità verificata prima della decodifica", () => {
    const bad = minimal.slice();
    bad[15] = 1;
    assert.throws(() => decodeFit(bad, fitSdk), /invalid_fit_integrity/);
  });

  test("ZIP, dimensione e tipo rifiutati", () => {
    assert.throws(() => decodeFit(new Uint8Array([0x50, 0x4b]), fitSdk), /zip_requires_validated_unpacking/);
    assert.throws(() => decodeFit([1, 2], fitSdk), /invalid_fit_size/);
  });

  test("errori di decodifica dell'SDK propagati", () => {
    class Decoder {
      isFIT = () => true;
      checkIntegrity = () => true;
      read = () => ({ errors: [new Error("x")], messages: {} });
    }
    assert.throws(
      () => decodeFit(minimal, { Decoder, Stream: { fromByteArray: (b) => b } }),
      /fit_decode_failed/,
    );
  });
});

describe("activityBundle", () => {
  const base = {
    ownerId: "a",
    activityId: 1,
    summary: {},
    receivedAt: "2026-11-17T10:00:00Z",
    originalBytes: Buffer.from("synthetic"),
    messages: {
      recordMesgs: [
        {
          timestamp: new Date("2026-11-17T09:00:00Z"),
          heartRate: 140,
          speed: 2,
          enhancedSpeed: 3,
          cadence: 90,
          unknownMetric: 12,
        },
        { timestamp: "non una data", heartRate: 150 },
      ],
      eventMesgs: [{ event: "timer" }],
    },
  };

  test("serie normalizzate e campi nativi conservati", () => {
    const bundle = activityBundle(base);
    assert.equal(bundle.series.find((x) => x.metric === "speed").samples[0].value, 3);
    assert.equal(bundle.series.find((x) => x.metric === "fit:cadence").samples[0].value, 90);
    assert.equal(bundle.series.find((x) => x.metric === "heartRate").samples.length, 1);
    assert.equal(bundle.nativeMessages.recordMesgs[0].unknownMetric, 12);
    assert.equal(bundle.nativeMessages.eventMesgs.length, 1);
    assert.match(bundle.originalSha256, /^[0-9a-f]{64}$/);
    assert.equal(bundle.sourceId, "1");
  });

  test("attribuzione e contenuto obbligatori", () => {
    assert.throws(() => activityBundle({ ...base, ownerId: "" }), /invalid_attribution/);
    assert.throws(() => activityBundle({ ...base, receivedAt: "x" }), /invalid_attribution/);
    assert.throws(() => activityBundle({ ...base, messages: undefined }), /invalid_activity_content/);
    assert.throws(() => activityBundle({ ...base, originalBytes: "x" }), /invalid_activity_content/);
  });

  test("messaggi senza record producono serie vuote", () => {
    assert.deepEqual(activityBundle({ ...base, messages: {} }).series, []);
  });
});
