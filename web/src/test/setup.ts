import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterAll, afterEach, beforeAll } from "vitest";
import { server } from "./server";

// ResizeObserver non esiste in jsdom ma serve ai grafici.
class ResizeObserverStub {
  observe(): void {
    // jsdom non misura il layout: nessuna notifica da emettere.
  }
  unobserve(): void {
    // Vedi observe().
  }
  disconnect(): void {
    // Vedi observe().
  }
}
if (!("ResizeObserver" in globalThis)) {
  Object.assign(globalThis, { ResizeObserver: ResizeObserverStub });
}

beforeAll(() => {
  server.listen({ onUnhandledFrame: "error" });
});
afterEach(() => {
  cleanup();
  server.resetHandlers();
});
afterAll(() => {
  server.close();
});
