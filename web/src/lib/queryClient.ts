import { QueryClient } from "@tanstack/react-query";
import { ApiError } from "../api/client";

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        // Gli errori 4xx sono definitivi: ritentare non serve.
        retry: (failureCount, error) =>
          !(error instanceof ApiError && error.status < 500) && failureCount < 2,
      },
    },
  });
}
