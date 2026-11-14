import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap, type DeviceKind } from "../../api/client";
import { summaryKey } from "../dashboard/queries";

export const devicesKey = ["devices"] as const;

export const useDevices = () =>
  useQuery({ queryKey: devicesKey, queryFn: () => unwrap(api.GET("/api/v1/devices")) });

export function useRegisterDevice() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; kind: DeviceKind }) => unwrap(api.POST("/api/v1/devices", { body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: devicesKey }),
  });
}

export function useRevokeDevice() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => unwrap(api.DELETE("/api/v1/devices/{id}", { params: { path: { id } } })),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: devicesKey }),
        queryClient.invalidateQueries({ queryKey: summaryKey }),
      ]);
    },
  });
}
