import { useState, type SubmitEvent } from "react";
import { useLocation, useNavigate } from "react-router";
import { ApiError } from "../../api/client";
import { useLogin } from "./useAuth";

function messageFor(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401) return "Email o password non corrette.";
    if (error.status === 423) return "Troppi tentativi: account bloccato per 15 minuti.";
    if (error.status === 429) return "Troppi tentativi ravvicinati: attendi un minuto.";
  }
  return "Accesso non riuscito. Riprova.";
}

export function LoginPage() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const login = useLogin();
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from ?? "/";

  const onSubmit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    login.mutate({ email, password }, { onSuccess: () => void navigate(from, { replace: true }) });
  };

  return (
    <main className="login">
      <form className="card login-card" onSubmit={onSubmit} aria-labelledby="login-title">
        <h1 id="login-title">MoscoRun</h1>
        <p className="muted">Accedi al tuo cruscotto di allenamento.</p>
        <label>
          Email
          <input
            type="email"
            autoComplete="username"
            required
            value={email}
            onChange={(e) => {
              setEmail(e.target.value);
            }}
          />
        </label>
        <label>
          Password
          <input
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => {
              setPassword(e.target.value);
            }}
          />
        </label>
        {login.isError && (
          <p role="alert" className="error">
            {messageFor(login.error)}
          </p>
        )}
        <button type="submit" disabled={login.isPending}>
          {login.isPending ? "Accesso…" : "Accedi"}
        </button>
      </form>
    </main>
  );
}
