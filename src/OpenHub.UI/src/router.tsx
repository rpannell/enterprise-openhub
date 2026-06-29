import { Suspense } from "react";
import { createBrowserRouter, Navigate } from "react-router-dom";
import Shell from "./layout/Shell";
import Dashboard from "./pages/Dashboard";
import Account from "./pages/Account";
import { RoleGate } from "./auth/RoleGate";
import { modules } from "./config/modules";

function Loading() {
  return (
    <div className="p-6 text-sm text-slate-500 dark:text-slate-400">
      Loading…
    </div>
  );
}

function Forbidden() {
  return (
    <div className="rounded-lg border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-400">
      You don't have access to this application. Contact an administrator if
      you believe this is a mistake.
    </div>
  );
}

export const router = createBrowserRouter([
  {
    path: "/",
    element: <Shell />,
    children: [
      { index: true, element: <Dashboard /> },
      { path: "account", element: <Account /> },
      ...modules.map((m) => ({
        path: m.path.slice(1), // relative to Shell
        element: (
          <RoleGate roles={m.roles} fallback={<Forbidden />}>
            <Suspense fallback={<Loading />}>
              <m.Component />
            </Suspense>
          </RoleGate>
        ),
      })),
      { path: "*", element: <Navigate to="/" replace /> },
    ],
  },
]);
