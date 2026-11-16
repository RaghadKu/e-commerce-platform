import { check, sleep } from "k6";
import http from "k6/http";

export const options = {
  stages: [
    { duration: "30s", target: 30 }, // Ramp up to 20 concurrent users over 30s
    { duration: "30s", target: 100 }, // Stay at 20 concurrent users for 1 min
    { duration: "10s", target: 0 }, // Ramp down to 0 users
  ],
  thresholds: {
    http_req_duration: ["p(95)<500", "p(50)<200", "p(99)<800"],
    http_req_failed: ["rate<0.01"],
  },
};

const BASE_URL = __ENV.BASE_URL || "http://localhost:8080";

// Define a pool of different product UUIDs to pick from
// (Make sure these products exist in your test database)
const PRODUCTS = [
  "885111ac-67d3-4cfe-3db5-08df3e253a66",
  "7731c555-b466-458f-fd31-08df3e2709f7",
  "319afbc3-cf94-43e1-108a-08df3e2ab0a5",
  "25aceea5-06bf-4351-0998-08df3e6c9891",
  "bf667cf4-31c6-479d-6863-08df3e6f8646",
  "0ae61535-afd1-4cdb-2d61-08df3e7183bf",
  "a2b99ad7-865c-4f9f-9ba4-08df3e74eacb",
];

// Helper function to get a random product from the array
function getRandomProduct() {
  const randomIndex = Math.floor(Math.random() * PRODUCTS.length);
  return PRODUCTS[randomIndex];
}

export default function () {
  const uniqueId = `${__VU}_${__ITER}_${Date.now()}`;
  const email = `user_${uniqueId}@test.com`;
  const password = "Password123!";
  const fullName = `Test User ${uniqueId}`;

  const baseHeaders = {
    "Content-Type": "application/json",
  };

  // ==========================================
  // 1. REGISTER USER (/api/auth/register)
  // ==========================================
  const registerPayload = JSON.stringify({
    fullName: fullName,
    email: email,
    password: password,
  });

  let res = http.post(`${BASE_URL}/api/auth/register`, registerPayload, {
    headers: baseHeaders,
  });

  if (!check(res, { "register status is 201": (r) => r.status === 201 })) {
    return;
  }

  // ==========================================
  // 2. LOGIN & GET TOKEN (/api/auth/login)
  // ==========================================
  const loginPayload = JSON.stringify({
    email: email,
    password: password,
  });

  res = http.post(`${BASE_URL}/api/auth/login`, loginPayload, {
    headers: baseHeaders,
  });

  const loginCheck = check(res, {
    "login status is 200": (r) => r.status === 200,
    "has access token": (r) => r.json("accessToken") !== undefined,
  });

  if (!loginCheck) {
    return;
  }

  const token = res.json("accessToken");
  const authHeaders = {
    "Content-Type": "application/json",
    Authorization: `Bearer ${token}`,
  };

  // ==========================================
  // 3. ADD A RANDOM ITEM TO ORDER (/api/orders/items)
  // ==========================================
  const selectedProductId = getRandomProduct();
  const randomQty = Math.floor(Math.random() * 3) + 1; // Random quantity between 1 and 3

  const addItemPayload = JSON.stringify({
    productId: selectedProductId,
    qty: randomQty,
  });

  res = http.post(`${BASE_URL}/api/orders/items`, addItemPayload, {
    headers: authHeaders,
  });

  if (!check(res, { "add item status is 201": (r) => r.status === 201 })) {
    return;
  }

  // ==========================================
  // 4. CHECKOUT ORDER (/api/orders/checkout)
  // ==========================================
  res = http.post(`${BASE_URL}/api/orders/checkout`, null, {
    headers: authHeaders,
  });

  check(res, {
    "checkout status is 200": (r) => r.status === 200,
  });

  sleep(1);
}
