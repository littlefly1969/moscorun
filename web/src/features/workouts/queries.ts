import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "../../api/client";

export const workoutsKey = ["workouts"] as const;

export const useWorkouts = () =>
  useQuery({
    queryKey: workoutsKey,
    queryFn: () => unwrap(api.GET("/api/v1/workouts")),
    refetchOnMount: "always",
  });

export const useWorkout = (id: string) =>
  useQuery({
    queryKey: [...workoutsKey, id],
    queryFn: () => unwrap(api.GET("/api/v1/workouts/{id}", { params: { path: { id } } })),
  });

/** Il tracciato cambia solo con una nuova revisione: resta in cache per la sessione. */
export const useRoute = (id: string, enabled: boolean) =>
  useQuery({
    queryKey: [...workoutsKey, id, "route"],
    queryFn: () =>
      unwrap(
        api.GET("/api/v1/workouts/{id}/route", { params: { path: { id }, query: { maxPoints: 2000 } } }),
      ),
    enabled,
    staleTime: Infinity,
  });

export const useSeries = (id: string) =>
  useQuery({
    queryKey: [...workoutsKey, id, "series"],
    queryFn: () => unwrap(api.GET("/api/v1/workouts/{id}/series", { params: { path: { id } } })),
    staleTime: Infinity,
  });

export const useMapsConfig = () =>
  useQuery({
    queryKey: ["maps", "config"],
    queryFn: () => unwrap(api.GET("/api/v1/maps/config")),
    staleTime: Infinity,
  });
