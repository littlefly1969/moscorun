import { ApiError } from "../api/client";
import { createQueryClient } from "./queryClient";

describe("createQueryClient", () => {
  const retry = createQueryClient().getDefaultOptions().queries?.retry as (n: number, e: unknown) => boolean;

  it("non ritenta gli errori del client", () => {
    expect(retry(0, new ApiError(404))).toBe(false);
  });

  it("ritenta al più due volte gli errori di rete o del server", () => {
    expect(retry(0, new ApiError(503))).toBe(true);
    expect(retry(1, new TypeError("network"))).toBe(true);
    expect(retry(2, new ApiError(503))).toBe(false);
  });
});
