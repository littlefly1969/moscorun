import { createHash } from "node:crypto";
import { healthCatalog } from "./catalog.mjs";
export { healthCatalog };
const origin = "https://personal.connectapi.garmin.com";
const tokenPath = "/personalapitoken-service/personal-api/personal-api-key/token";
const id = (value) => {
  if (typeof value === "number" && !Number.isSafeInteger(value)) throw new GarminError("unsafe_activity_id");
  const s = String(value);
  if (!/^[1-9][0-9]{0,19}$/.test(s)) throw new GarminError("invalid_activity_id");
  return s;
};
export class GarminError extends Error {
  constructor(code, status = null, retryAfterSeconds = null) {
    super(code);
    this.name = "GarminError";
    this.status = status;
    this.retryAfterSeconds = retryAfterSeconds;
  }
}
async function bytes(response, maxBytes) {
  const chunks = [];
  let size = 0;
  if (!response.body) throw new GarminError("empty_response");
  for await (const chunk of response.body) {
    size += chunk.byteLength;
    if (size > maxBytes) throw new GarminError("response_too_large");
    chunks.push(Buffer.from(chunk));
  }
  return Buffer.concat(chunks);
}
export class GarminPersonalClient {
  #key;
  #token = null;
  #pending = null;
  #fetch;
  #now;
  #sleep;
  constructor({
    ownerId,
    apiKey,
    fetchImpl = fetch,
    now = Date.now,
    sleep = (ms) => new Promise((r) => setTimeout(r, ms)),
  }) {
    if (typeof ownerId !== "string" || !ownerId || typeof apiKey !== "string" || !apiKey.trim())
      throw new GarminError("connection_required");
    this.ownerId = ownerId;
    this.#key = apiKey;
    this.#fetch = fetchImpl;
    this.#now = now;
    this.#sleep = sleep;
  }
  async #send(url, init = {}) {
    try {
      return await this.#fetch(url, { ...init, redirect: "error", signal: AbortSignal.timeout(30000) });
    } catch {
      throw new GarminError("provider_unavailable");
    }
  }
  async #accessToken() {
    if (this.#token && this.#token.expiresAt > this.#now() + 60000) return this.#token.value;
    if (this.#pending) return this.#pending;
    this.#pending = (async () => {
      const r = await this.#send(origin + tokenPath, {
        method: "POST",
        headers: { Authorization: "ApiKey " + this.#key },
      });
      if (!r.ok)
        throw new GarminError(
          r.status === 401 || r.status === 403 ? "reauthorization_required" : "token_request_failed",
          r.status,
        );
      let data;
      try {
        data = JSON.parse(await bytes(r, 65536));
      } catch {
        throw new GarminError("invalid_token_response");
      }
      if (typeof data.access_token !== "string" || !data.access_token)
        throw new GarminError("invalid_token_response");
      const seconds = data.expires_in ?? 86400;
      if (!Number.isFinite(seconds) || seconds <= 0) throw new GarminError("invalid_token_expiry");
      this.#token = { value: data.access_token, expiresAt: this.#now() + Math.min(seconds, 86400) * 1000 };
      return data.access_token;
    })();
    try {
      return await this.#pending;
    } finally {
      this.#pending = null;
    }
  }
  async #get(path, { ownerId, query = {}, binary = false } = {}) {
    if (ownerId !== this.ownerId) throw new GarminError("owner_mismatch");
    if (
      !path.startsWith("/") ||
      path.startsWith("//") ||
      path.includes("..") ||
      path.includes("?") ||
      path.includes("#")
    )
      throw new GarminError("invalid_path");
    const url = new URL(path, origin);
    for (const [key, value] of Object.entries(query))
      if (value !== undefined) url.searchParams.set(key, String(value));
    let renewed = false;
    for (let attempt = 0; attempt < 3; attempt++) {
      const token = await this.#accessToken();
      const r = await this.#send(url.toString(), {
        headers: {
          Authorization: "Bearer " + token,
          Accept: binary ? "application/octet-stream" : "application/json",
        },
      });
      if (r.status === 401 && !renewed) {
        this.#token = null;
        renewed = true;
        continue;
      }
      if ((r.status === 429 || [502, 503, 504].includes(r.status)) && attempt < 2) {
        const header = r.headers.get("Retry-After");
        const parsed = header
          ? /^\d+$/.test(header)
            ? Number(header)
            : Number.isFinite(Date.parse(header))
              ? Math.max(0, Math.ceil((Date.parse(header) - this.#now()) / 1000))
              : null
          : null;
        const wait = parsed ?? 2 ** attempt;
        if (wait > 60) throw new GarminError("retry_later", r.status, wait);
        await this.#sleep(wait * 1000);
        continue;
      }
      if (!r.ok)
        throw new GarminError(
          r.status === 401 || r.status === 403 ? "reauthorization_required" : "provider_http_error",
          r.status,
        );
      const body = await bytes(r, binary ? 32 * 1024 * 1024 : 8 * 1024 * 1024);
      if (binary) return { bytes: body, contentType: r.headers.get("Content-Type") };
      try {
        return JSON.parse(body);
      } catch {
        throw new GarminError("invalid_json");
      }
    }
    throw new GarminError("retry_exhausted");
  }
  async *activities({ ownerId, startDate, endDate, pageSize = 99, maxPages = 1000 }) {
    if (
      !/^\d{4}-\d{2}-\d{2}$/.test(startDate) ||
      !/^\d{4}-\d{2}-\d{2}$/.test(endDate) ||
      startDate > endDate ||
      !Number.isInteger(pageSize) ||
      pageSize < 1 ||
      pageSize > 99 ||
      !Number.isInteger(maxPages) ||
      maxPages < 1
    )
      throw new GarminError("invalid_window");
    const seen = new Set(),
      pages = new Set();
    for (let page = 0; page < maxPages; page++) {
      const rows = await this.#get("/activitylist-service/activities/search/activities", {
        ownerId,
        query: {
          start: page * pageSize,
          limit: pageSize,
          startDate,
          endDate,
          sortOrder: "asc",
          excludeChildren: false,
        },
      });
      if (!Array.isArray(rows) || rows.length > pageSize) throw new GarminError("invalid_activity_page");
      if (!rows.length) return;
      const fingerprint = rows.map((row) => id(row.activityId)).join(",");
      if (pages.has(fingerprint)) throw new GarminError("repeated_page");
      pages.add(fingerprint);
      for (const row of rows) {
        const key = id(row.activityId);
        if (!seen.has(key)) {
          seen.add(key);
          yield row;
        }
      }
      if (rows.length < pageSize) return;
    }
    throw new GarminError("pagination_incomplete");
  }
  profile(ownerId) {
    return this.#get("/userprofile-service/userprofile/settings", { ownerId });
  }
  detail(activityId, ownerId) {
    return this.#get("/activity-service/activity/" + id(activityId), { ownerId });
  }
  original(activityId, ownerId) {
    return this.#get("/download-service/files/activity/" + id(activityId), { ownerId, binary: true });
  }
  readHealth(template, parameters, ownerId) {
    const spec = healthCatalog[template];
    if (!spec) throw new GarminError("unsupported_health_endpoint");
    const allowed = new Set([...spec.path, ...spec.query]);
    if (Object.keys(parameters).some((k) => !allowed.has(k)))
      throw new GarminError("unsupported_health_parameter");
    let endpoint = template;
    for (const key of spec.path) {
      const value = String(parameters[key] ?? "");
      if (!/^[A-Za-z0-9_-]{1,80}$/.test(value)) throw new GarminError("invalid_health_path_parameter");
      endpoint = endpoint.replace("{" + key + "}", encodeURIComponent(value));
    }
    return this.#get(endpoint, {
      ownerId,
      query: Object.fromEntries(
        spec.query.filter((k) => parameters[k] !== undefined).map((k) => [k, parameters[k]]),
      ),
    });
  }
  health(kind, date, ownerId) {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) throw new GarminError("invalid_date");
    const routes = {
      hrv: ["/hrv-service/hrv/" + date, {}],
      sleep: ["/sleep-service/sleep/dailySleep", { date }],
      nutrition: ["/nutrition-service/food/logs/" + date, {}],
      daily: ["/usersummary-service/usersummary/daily", { calendarDate: date }],
      heartRate: ["/wellness-service/wellness/dailyHeartRate", { date }],
    };
    if (!Object.hasOwn(routes, kind)) throw new GarminError("unsupported_health_category");
    const [path, query] = routes[kind];
    return this.#get(path, { ownerId, query });
  }
}
export function decodeFit(rawBytes, { Decoder, Stream }) {
  if (!(rawBytes instanceof Uint8Array) || rawBytes.byteLength > 32 * 1024 * 1024)
    throw new GarminError("invalid_fit_size");
  if (rawBytes[0] === 0x50 && rawBytes[1] === 0x4b) throw new GarminError("zip_requires_validated_unpacking");
  const decoder = new Decoder(Stream.fromByteArray(rawBytes));
  if (!decoder.isFIT() || !decoder.checkIntegrity()) throw new GarminError("invalid_fit_integrity");
  const result = decoder.read({ applyScaleAndOffset: true, convertDateTimesToDates: true });
  if (result.errors.length) throw new GarminError("fit_decode_failed");
  return result.messages;
}
const time = (v) => {
  const n = v instanceof Date ? v.getTime() : Date.parse(v);
  return Number.isFinite(n) ? new Date(n).toISOString() : null;
};
export function activityBundle({ ownerId, activityId, summary, messages, receivedAt, originalBytes }) {
  if (!ownerId || !time(receivedAt)) throw new GarminError("invalid_attribution");
  if (!messages || typeof messages !== "object" || !(originalBytes instanceof Uint8Array))
    throw new GarminError("invalid_activity_content");
  const records = (messages.recordMesgs ?? []).map((r) => ({ ...r, timestamp: time(r.timestamp) }));
  const metrics = [
    ["heartRate", "bpm", (r) => r.heartRate],
    ["speed", "m/s", (r) => r.enhancedSpeed ?? r.speed],
    ["power", "W", (r) => r.power],
    ["altitude", "m", (r) => r.enhancedAltitude ?? r.altitude],
    ["fit:cadence", "rpm", (r) => r.cadence],
    ["fit:verticalOscillation", "mm", (r) => r.verticalOscillation],
    ["fit:stanceTime", "ms", (r) => r.stanceTime],
    ["fit:verticalRatio", "%", (r) => r.verticalRatio],
    ["fit:stepLength", "mm", (r) => r.stepLength],
    ["fit:temperature", "Cel", (r) => r.temperature],
    ["fit:respirationRate", "1/min", (r) => r.respirationRate],
  ];
  const series = metrics
    .map(([metric, unit, value]) => ({
      metric,
      unit,
      method: "provider_reported",
      samples: records
        .filter((r) => r.timestamp && Number.isFinite(value(r)))
        .map((r) => ({ time: r.timestamp, value: value(r) })),
    }))
    .filter((s) => s.samples.length);
  return {
    schemaVersion: 1,
    ownerId,
    source: "garmin-connect",
    sourceId: id(activityId),
    availableAt: time(receivedAt),
    originalSha256: createHash("sha256").update(originalBytes).digest("hex"),
    summary,
    series,
    nativeMessages: messages,
    notes: [
      "Provider-native fields, developer fields, laps and events remain in nativeMessages.",
      "Native cadence and running-dynamics units are not silently converted to MoscoRun core metrics.",
      "A source revision must be assigned by the durable sync ledger after detecting content changes.",
    ],
  };
}
