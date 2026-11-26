import { NavLink, Outlet, useNavigate } from "react-router";
import { useCurrentUser, useLogout } from "../features/auth/useAuth";

export function Layout() {
  const { data: user } = useCurrentUser();
  const logout = useLogout();
  const navigate = useNavigate();

  return (
    <div className="shell">
      <header className="topbar">
        <span className="brand">MoscoRun</span>
        <nav aria-label="Principale">
          <NavLink to="/" end>
            Atlante scientifico
          </NavLink>
          <NavLink to="/forma">Forma</NavLink>
          <NavLink to="/allenamenti">Allenamenti</NavLink>
          <NavLink to="/dispositivi">Dispositivi</NavLink>
        </nav>
        <div className="user">
          <span className="muted">{user?.displayName ?? user?.email}</span>
          <button
            type="button"
            className="secondary"
            onClick={() => {
              logout.mutate(undefined, { onSuccess: () => void navigate("/accesso") });
            }}
          >
            Esci
          </button>
        </div>
      </header>
      <main className="content">
        <Outlet />
      </main>
    </div>
  );
}
