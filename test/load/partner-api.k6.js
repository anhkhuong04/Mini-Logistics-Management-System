import http from "k6/http";
import { check, fail, sleep } from "k6";
import { Rate, Trend } from "k6/metrics";

const trackingLatency = new Trend("partner_tracking_latency", true);
const unexpectedResponse = new Rate("partner_unexpected_response");
const writeLoadEnabled = (__ENV.ENABLE_WRITE_LOAD ?? "false").toLowerCase() === "true";

const scenarios = {
  tracking: {
    executor: "constant-arrival-rate",
    rate: Number(__ENV.TRACKING_RPS ?? 100),
    timeUnit: "1s",
    duration: __ENV.DURATION ?? "5m",
    preAllocatedVUs: Number(__ENV.PREALLOCATED_VUS ?? 50),
    maxVUs: Number(__ENV.MAX_VUS ?? 300),
    exec: "trackShipment"
  }
};

if (writeLoadEnabled) {
  scenarios.create = {
    executor: "constant-arrival-rate",
    rate: Number(__ENV.CREATE_RPS ?? 5),
    timeUnit: "1s",
    duration: __ENV.DURATION ?? "5m",
    preAllocatedVUs: 10,
    maxVUs: 100,
    exec: "createShipment"
  };
}

export const options = {
  scenarios,
  thresholds: {
    partner_tracking_latency: ["p(95)<300", "p(99)<750"],
    partner_unexpected_response: ["rate<0.001"],
    http_req_failed: ["rate<0.001"]
  }
};

const baseUrl = (__ENV.BASE_URL ?? "").replace(/\/$/, "");
const apiKeys = (__ENV.API_KEYS ?? __ENV.API_KEY ?? "")
  .split(",")
  .map(value => value.trim())
  .filter(Boolean);
const trackingCode = __ENV.TRACKING_CODE ?? "";

export function setup() {
  if (!baseUrl.startsWith("https://") || apiKeys.length === 0 || !trackingCode) {
    fail("BASE_URL=https://..., API_KEY or API_KEYS, and TRACKING_CODE are required.");
  }
}

function headers(extra = {}) {
  const apiKey = apiKeys[(__VU - 1) % apiKeys.length];
  return {
    authorization: `Bearer ${apiKey}`,
    accept: "application/json",
    ...extra
  };
}

export function trackShipment() {
  const response = http.get(
    `${baseUrl}/api/v1/partner/shipments/${encodeURIComponent(trackingCode)}`,
    { headers: headers(), tags: { endpoint: "tracking" }, timeout: "5s" }
  );
  trackingLatency.add(response.timings.duration);
  const valid = check(response, { "tracking is 200": value => value.status === 200 });
  unexpectedResponse.add(!valid);
  sleep(Math.random() * 0.05);
}

export function createShipment() {
  const unique = `${__VU}-${__ITER}-${Date.now()}`;
  const body = JSON.stringify({
    externalOrderId: `LOAD-${unique}`,
    receiver: { name: "Load Test Receiver", phone: "0911111111" },
    deliveryAddress: {
      street: "9 Le Loi",
      ward: "Ben Nghe",
      province: "Ho Chi Minh",
      country: "Vietnam"
    },
    parcel: { weightKg: 1, lengthCm: 10, widthCm: 10, heightCm: 10 },
    goodsValueAmount: 100000,
    codAmount: 0,
    currency: "VND"
  });
  const response = http.post(`${baseUrl}/api/v1/partner/shipments`, body, {
    headers: headers({ "content-type": "application/json", "idempotency-key": `load-${unique}` }),
    tags: { endpoint: "create" },
    timeout: "8s"
  });
  const valid = check(response, {
    "create is 200 or 201": value => value.status === 200 || value.status === 201
  });
  unexpectedResponse.add(!valid);
}
