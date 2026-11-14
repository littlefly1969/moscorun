import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router";
import { useCurrentUser } from "./useAuth";

export function RequireAuth({ children }: { children: ReactNode }) {
  const { data: user, isPending, isError } = useCurrentUser();
  const location = useLocation();

  if (isPending)
    return (
      <p className="muted" role="status">
        Caricamento…
      </p>
    );
  if (isError) return <p role="alert">Server non raggiungibile. Riprova tra poco.</p>;
  if (!user) return <Navigate to="/accesso" replace state={{ from: location.pathname }} />;
  return children;
}
