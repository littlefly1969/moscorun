import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap, type AttributionStatus } from "../../api/client";

export const summaryKey = ["dashboard", "summary"] as const;
export const weighingsKey = ["weighings"] as const;

/** Il cruscotto mostra dati che arrivano dagli agenti in qualsiasi momento: sempre aggiornati all'apertura e ogni minuto. */
const live = { refetchOnMount: "always", refetchInterval: 60_000 } as const;

export const useSummary = () =>
  useQuery({ queryKey: summaryKey, queryFn: () => unwrap(api.GET("/api/v1/dashboard/summary")), ...live });

export const useWeighings = () =>
  useQuery({ queryKey: weighingsKey, queryFn: () => unwrap(api.GET("/api/v1/weighings")), ...live });

export function useSetAttribution() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, attribution }: { id: string; attribution: AttributionStatus }) =>
      unwrap(
        api.PUT("/api/v1/weighings/{id}/attribution", { params: { path: { id } }, body: { attribution } }),
      ),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: weighingsKey }),
        queryClient.invalidateQueries({ queryKey: summaryKey }),
      ]);
    },
  });
}
