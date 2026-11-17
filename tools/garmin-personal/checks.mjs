import assert from "node:assert/strict";
import { GarminPersonalClient, activityBundle, decodeFit, healthCatalog } from "./client.mjs";
export async function runChecks() {
  let checks = 0, tokens = 0;
  const responses = [[{ activityId: 1 }, { activityId: 2 }], [{ activityId: 2 }, { activityId: 3 }], []];
  const calls = [];
  const mock = async (url, init) => { calls.push({ url, method: init.method });
    if (url.includes("personal-api-key/token")) { tokens++; assert.equal(init.headers.Authorization, "ApiKey test-key"); return Response.json({ access_token: "test-access", expires_in: 86400 }); }
    assert.equal(init.headers.Authorization, "Bearer test-access");
    const u = new URL(url); return Response.json(responses[Number(u.searchParams.get("start")) / 2]); };
  const client = new GarminPersonalClient({ ownerId: "a", apiKey: "test-key", fetchImpl: mock });
  const rows = []; for await (const row of client.activities({ ownerId: "a", startDate: "2026-01-01", endDate: "2026-01-02", pageSize: 2 })) rows.push(row);
  assert.deepEqual(rows.map(x => x.activityId), [1, 2, 3]); checks++;
  assert.equal(tokens, 1); checks++;
  assert.equal(calls.every(x => x.url.startsWith("https://personal.connectapi.garmin.com/")), true); checks++;
  await assert.rejects(() => client.detail(1, "b"), /owner_mismatch/); checks++;
  assert.throws(() => client.detail("../bad", "a"), /invalid_activity_id/); checks++;
  const denied = new GarminPersonalClient({ ownerId: "a", apiKey: "test-key", fetchImpl: async () => new Response("private failure", { status: 403 }) });
  await assert.rejects(() => denied.detail(1, "a"), /reauthorization_required/); checks++;
  let tries = 0, slept = 0;
  const limited = new GarminPersonalClient({ ownerId: "a", apiKey: "test-key", sleep: async () => slept++,
    fetchImpl: async url => url.includes("personal-api-key/token") ? Response.json({ access_token: "access" }) : (++tries === 1 ? new Response("", { status: 429, headers: { "Retry-After": "0" } }) : Response.json({ activityId: 1 })) });
  assert.equal((await limited.detail(1, "a")).activityId, 1); assert.equal(slept, 1); checks++;
  const repeat = new GarminPersonalClient({ ownerId: "a", apiKey: "test-key", fetchImpl: async url => url.includes("personal-api-key/token") ? Response.json({ access_token: "access" }) : Response.json([{ activityId: 1 }]) });
  await assert.rejects(async () => { for await (const r of repeat.activities({ ownerId: "a", startDate: "2026-01-01", endDate: "2026-01-02", pageSize: 1 })) void r; }, /repeated_page/); checks++;
  const partial = new GarminPersonalClient({ ownerId: "a", apiKey: "test-key", fetchImpl: async url => url.includes("personal-api-key/token") ? Response.json({ access_token: "access" }) : Response.json([{ activityId: 1 }]) });
  await assert.rejects(async () => { for await (const r of partial.activities({ ownerId: "a", startDate: "2026-01-01", endDate: "2026-01-02", pageSize: 1, maxPages: 1 })) void r; }, /pagination_incomplete/); checks++;
  const bundle = activityBundle({ ownerId: "a", activityId: 1, summary: {}, receivedAt: "2026-11-17T10:00:00Z", originalBytes: Buffer.from("synthetic"),
    messages: { recordMesgs: [{ timestamp: new Date("2026-11-17T09:00:00Z"), heartRate: 140, speed: 2, enhancedSpeed: 3, cadence: 90, unknownMetric: 12 }], eventMesgs: [{ event: "timer" }] } });
  assert.equal(bundle.series.find(x => x.metric === "speed").samples[0].value, 3); checks++;
  assert.equal(bundle.series.find(x => x.metric === "fit:cadence").samples[0].value, 90); checks++;
  assert.equal(bundle.nativeMessages.recordMesgs[0].unknownMetric, 12); checks++;
  assert.equal(bundle.nativeMessages.eventMesgs.length, 1); checks++;
  assert.equal(bundle.originalSha256.length, 64); checks++;
  assert.throws(() => decodeFit(new Uint8Array([0x50, 0x4b]), {}), /zip_requires_validated_unpacking/); checks++;
  assert.equal(Object.keys(healthCatalog).length,19); checks++;
  assert.throws(()=>client.readHealth("https://attacker.test",{},"a"),/unsupported_health_endpoint/); checks++;
  assert.throws(()=>client.detail(Number.MAX_SAFE_INTEGER+1,"a"),/unsafe_activity_id/); checks++;
  const generic=new GarminPersonalClient({ownerId:"a",apiKey:"test-key",fetchImpl:async url=>url.includes("personal-api-key/token")?Response.json({access_token:"access"}):Response.json({ok:true})});
  assert.equal((await generic.profile("a")).ok,true); checks++;
  assert.equal((await generic.readHealth("/biometric-service/heartRateZones/v2",{},"a")).ok,true); checks++;
  return { checks, status: "passed" };
}
export async function runSdkChecks(sdk) {
  const minimal = new Uint8Array([0x0E,0x10,0xD9,0x07,0,0,0,0,0x2E,0x46,0x49,0x54,0x91,0x33,0,0]);
  const result = decodeFit(minimal, sdk); assert.equal(typeof result, "object");
  const bad = minimal.slice(); bad[15] = 1;
  assert.throws(() => decodeFit(bad, sdk), /invalid_fit_integrity/);
  return { checks: 2, status: "passed" };
}
