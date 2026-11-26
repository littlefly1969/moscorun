import { NavLink, Outlet } from "react-router";
import "./workspace.css";

const sections = [
  { path: "/", number: "01", title: "Atlante", hint: "Catalogo e misure" },
  { path: "/cronoscopio", number: "02", title: "Cronoscopio", hint: "Tempo e segnali" },
  { path: "/laboratorio", number: "03", title: "Laboratorio", hint: "Metodi e protocolli" },
  { path: "/simulatore", number: "04", title: "Simulatore", hint: "Scenari condizionali" },
  { path: "/evidenze", number: "05", title: "Evidenze", hint: "Relazioni documentate" },
];

export function WorkspaceLayout() {
  return (
    <div className="science-workspace">
      <nav className="sw-nav" aria-label="Ambiente scientifico">
        <p className="sw-kicker">MoscoRun / Scientific workspace</p>
        {sections.map((section) => (
          <NavLink key={section.path} to={section.path} end>
            <span>{section.number}</span>
            <div>
              <strong>{section.title}</strong>
              <small>{section.hint}</small>
            </div>
          </NavLink>
        ))}
      </nav>
      <div className="sw-main">
        <Outlet />
      </div>
    </div>
  );
}
