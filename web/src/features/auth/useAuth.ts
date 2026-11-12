import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ApiError, unwrap } from "../../api/client";

export const meQueryKey = ["auth", "me"] as const;

export function useCurrentUser() {
  return useQuery({
    queryKey: meQueryKey,
    queryFn: async () => {
      try {
        return await unwrap(api.GET("/api/auth/me"));
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) return null;
        throw error;
      }
    },
  });
}

export function useLogin() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (credentials: { email: string; password: string }) =>
      unwrap(api.POST("/api/auth/login", { body: credentials })),
    // "all": la query /me non ha osservatori attivi sulla pagina di login, ma va aggiornata prima di navigare.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: meQueryKey, refetchType: "all" }),
  });
}

export function useLogout() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => unwrap(api.POST("/api/auth/logout")),
    onSuccess: () => {
      queryClient.clear();
    },
  });
}
