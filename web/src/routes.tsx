import { Navigate, type RouteObject } from "react-router";
import { Layout } from "./components/Layout";
import { LoginPage } from "./features/auth/LoginPage";
import { RequireAuth } from "./features/auth/RequireAuth";
import { DashboardPage } from "./features/dashboard/DashboardPage";
import { DevicesPage } from "./features/devices/DevicesPage";

export const routes: RouteObject[] = [
  { path: "/accesso", element: <LoginPage /> },
  {
    element: (
      <RequireAuth>
        <Layout />
      </RequireAuth>
    ),
    children: [
      { index: true, element: <DashboardPage /> },
      { path: "dispositivi", element: <DevicesPage /> },
    ],
  },
  { path: "*", element: <Navigate to="/" replace /> },
];
