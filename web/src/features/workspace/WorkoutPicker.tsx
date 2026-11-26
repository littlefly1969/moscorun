import { useInfiniteQuery } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { api, unwrap, type WorkoutSummary } from "../../api/client";
import { activityLabel, formatDateTime, formatDistance } from "../../lib/format";

export function WorkoutPicker({ children }: { children: (workout: WorkoutSummary) => ReactNode }) {
  const [id, setId] = useState("");
  const workouts = useInfiniteQuery({
    queryKey: ["workspace", "workouts"],
    initialPageParam: null as string | null,
    queryFn: ({ pageParam }) =>
      unwrap(
        api.GET("/api/v1/workouts", {
          params: { query: { pageSize: 50, ...(pageParam ? { cursor: pageParam } : {}) } },
        }),
      ),
    getNextPageParam: (last) => last.nextCursor ?? undefined,
  });
  const items = workouts.data?.pages.flatMap((page) => page.items) ?? [];
  const selected = items.find((workout) => workout.id === id);
  return (
    <>
      <div className="sw-controls">
        <label>
          Seduta di riferimento
          <select
            value={id}
            onChange={(event) => {
              setId(event.target.value);
            }}
          >
            <option value="">Seleziona una seduta</option>
            {items.map((workout) => (
              <option key={workout.id} value={workout.id}>
                {activityLabel(workout.activityType)} · {formatDateTime(workout.startedAt)} ·{" "}
                {formatDistance(workout.distanceM)}
              </option>
            ))}
          </select>
        </label>
        {workouts.hasNextPage && (
          <button
            type="button"
            className="secondary"
            disabled={workouts.isFetchingNextPage}
            onClick={() => void workouts.fetchNextPage()}
          >
            Altre sedute
          </button>
        )}
      </div>
      {workouts.isPending && <p role="status">Caricamento delle sedute…</p>}
      {workouts.isError && <p role="alert">Impossibile leggere le sedute.</p>}
      {workouts.isSuccess && items.length === 0 && <p>Nessuna seduta disponibile.</p>}
      {selected && children(selected)}
    </>
  );
}
