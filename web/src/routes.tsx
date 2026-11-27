import { Navigate, type RouteObject } from "react-router";
import { Layout } from "./components/Layout";
import { LoginPage } from "./features/auth/LoginPage";
import { RequireAuth } from "./features/auth/RequireAuth";
import { DashboardPage } from "./features/dashboard/DashboardPage";
import { DevicesPage } from "./features/devices/DevicesPage";
import { WorkoutDetailPage } from "./features/workouts/WorkoutDetailPage";
import { WorkoutsPage } from "./features/workouts/WorkoutsPage";
import { WorkspaceLayout } from "./features/workspace/WorkspaceLayout";
import { AtlasPage } from "./features/workspace/AtlasPage";
import { ChronoscopePage } from "./features/workspace/ChronoscopePage";
import { EvidencePage, LaboratoryPage } from "./features/workspace/LaboratoryPage";
import { AcquisitionPage } from "./features/workspace/AcquisitionPage";
import { SimulatorPage } from "./features/workspace/SimulatorPage";

export const routes: RouteObject[] = [
  { path: "/accesso", element: <LoginPage /> },
  {
    element: (
      <RequireAuth>
        <Layout />
      </RequireAuth>
    ),
    children: [
      {
        element: <WorkspaceLayout />,
        children: [
          { index: true, element: <AtlasPage /> },
          { path: "cronoscopio", element: <ChronoscopePage /> },
          { path: "laboratorio", element: <LaboratoryPage /> },
          { path: "simulatore", element: <SimulatorPage /> },
          { path: "evidenze", element: <EvidencePage /> },
          { path: "acquisizione", element: <AcquisitionPage /> },
        ],
      },
      { path: "forma", element: <DashboardPage /> },
      { path: "allenamenti", element: <WorkoutsPage /> },
      { path: "allenamenti/:id", element: <WorkoutDetailPage /> },
      { path: "dispositivi", element: <DevicesPage /> },
    ],
  },
  { path: "*", element: <Navigate to="/" replace /> },
];
