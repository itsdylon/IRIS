import assert from "node:assert/strict"
import process from "node:process"
import {io as ioClient} from "socket.io-client"

const SERVER_URL = process.env.SERVER_URL || "http://localhost:3000";
const TIMEOUT_MS = Number(process.env.TEST_TIMEOUT_MS || 8000);

function delay(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

function withTimeout(promise, label) {
  let t;
  const timeout = new Promise((_, rej) => {
    t = setTimeout(() => rej(new Error(`Timeout: ${label}`)), TIMEOUT_MS);
  });
  return Promise.race([promise, timeout]).finally(() => clearTimeout(t));
}

function once(socket, eventName) {
  return new Promise((resolve) => socket.once(eventName, resolve));
}

function connectClient(name) {
  const socket = ioClient(SERVER_URL, {
    transports: ["websocket"], // faster + less flaky in CI
    timeout: TIMEOUT_MS,
    reconnection: false,
  });

  socket.on("connect_error", (err) => {
    // helpful logging; will still fail via timeout if not handled
    console.error(`[${name}] connect_error:`, err?.message || err);
  });

  return socket;
}