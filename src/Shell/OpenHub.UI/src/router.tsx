import { Suspense, lazy } from "react";
import { createBrowserRouter, Navigate } from "react-router-dom";
import Shell from "./layout/Shell";
import Dashboard from "./pages/Dashboard";
import Account from "./pages/Account";
import { RoleGate } from "./auth/RoleGate";
import { useAuth } from "./auth/AuthProvider";
import { KEYCLOAK_REALM } from "./keycloak";
import { modules } from "./config/modules";
import ProductApp from "./modules/product/ProductApp";
import ProductList from "./modules/product/catalog/ProductList";
import ProductCreatePage from "./modules/product/catalog/ProductCreatePage";
import ProductEditPage from "./modules/product/catalog/ProductEditPage";

// A genuine Module Federation remote — a separately built (Rspack),
// separately deployable artifact (src/Administration/OpenHub.Administration.UI), not a
// local folder. React.lazy works unchanged with a federated dynamic
// import(): the runtime resolves "adminRemote/AdminApp" to whatever
// remoteEntry.js the federation plugin configured (see vite.config.ts).
const FederatedAdminApp = lazy(() => import("adminRemote/AdminApp"));

// Same pattern, second federated remote (src/CRM/OpenHub.CRM.UI) — see FederatedAdminApp above.
const FederatedCrmApp = lazy(() => import("crmRemote/CrmApp"));

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

/**
 * The entire integration surface for the federated Admin remote: resolve
 * the HostAuthContext once, hand it to the remote as a prop, done. The
 * remote owns everything under /admin/* from here — its own tabs, its own
 * nested routes (organizations/users) — the shell doesn't know they exist.
 */
function AdminRemoteMount() {
  const { roles, getToken } = useAuth();
  return <FederatedAdminApp auth={{ roles, realmSlug: KEYCLOAK_REALM, getToken }} />;
}

/**
 * Same integration surface as AdminRemoteMount above, for the CRM remote.
 */
function CrmRemoteMount() {
  const { roles, getToken } = useAuth();
  return <FederatedCrmApp auth={{ roles, realmSlug: KEYCLOAK_REALM, getToken }} />;
}

const productModule = modules.find((m) => m.id === "product")!;
const adminModule = modules.find((m) => m.id === "admin")!;
const crmModule = modules.find((m) => m.id === "crm")!;
const otherModules = modules.filter(
  (m) => m.id !== "product" && m.id !== "admin" && m.id !== "crm"
);

export const router = createBrowserRouter([
  {
    path: "/",
    element: <Shell />,
    children: [
      { index: true, element: <Dashboard /> },
      { path: "account", element: <Account /> },

      // Product module — nested routes for catalog list, create, edit
      {
        path: "product",
        element: (
          <RoleGate roles={productModule.roles} fallback={<Forbidden />}>
            <ProductApp />
          </RoleGate>
        ),
        children: [
          { index: true, element: <Navigate to="catalog" replace /> },
          { path: "catalog", element: <ProductList /> },
          { path: "catalog/new", element: <ProductCreatePage /> },
          { path: "catalog/:id/edit", element: <ProductEditPage /> },
        ],
      },

      // Administration module — federated remote, mounted whole. The
      // trailing "/*" hands every sub-path (organizations, users, ...) to
      // the remote's own <Routes> instead of declaring them here.
      {
        path: "admin/*",
        element: (
          <RoleGate roles={adminModule.roles} fallback={<Forbidden />}>
            <Suspense fallback={<Loading />}>
              <AdminRemoteMount />
            </Suspense>
          </RoleGate>
        ),
      },

      // CRM module — federated remote, mounted whole, same pattern as Administration
      // above. The trailing "/*" hands every sub-path (clients, sales-reps, ...) to
      // the remote's own <Routes> instead of declaring them here.
      {
        path: "crm/*",
        element: (
          <RoleGate roles={crmModule.roles} fallback={<Forbidden />}>
            <Suspense fallback={<Loading />}>
              <CrmRemoteMount />
            </Suspense>
          </RoleGate>
        ),
      },

      // All other modules remain flat — Component is guaranteed here since
      // otherModules excludes the three ids (product, admin, crm) that omit it.
      ...otherModules.map((m) => {
        const Component = m.Component!;
        return {
          path: m.path.slice(1),
          element: (
            <RoleGate roles={m.roles} fallback={<Forbidden />}>
              <Suspense fallback={<Loading />}>
                <Component />
              </Suspense>
            </RoleGate>
          ),
        };
      }),

      { path: "*", element: <Navigate to="/" replace /> },
    ],
  },
]);
