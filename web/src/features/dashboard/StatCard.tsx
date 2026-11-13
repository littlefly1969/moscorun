import type { ReactNode } from "react";

export function StatCard({ label, value, hint }: { label: string; value: ReactNode; hint?: ReactNode }) {
  return (
    <section className="card stat" aria-label={label}>
      <h2 className="stat-label">{label}</h2>
      <p className="stat-value">{value}</p>
      {hint != null && <p className="muted stat-hint">{hint}</p>}
    </section>
  );
}
