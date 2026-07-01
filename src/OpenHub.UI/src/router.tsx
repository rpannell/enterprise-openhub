import { Suspense } from "react";
import { createBrowserRouter, Navigate } from "react-router-dom";
import Shell from "./layout/Shell";
import Dashboard from "./pages/Dashboard";
import Account from "./pages/Account";
import { RoleGate } from "./auth/RoleGate";
import { modules } from "./config/modules";
import ProductApp from "./modules/product/ProductApp";
import ProductList from "./modules/product/catalog/ProductList";
import ProductCreatePage from "./modules/product/catalog/ProductCreatePage";
import ProductEditPage from "./modules/product/catalog/ProductEditPage";

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

const productModule = modules.find((m) => m.id === "product")!;
const otherModules = modules.filter((m) => m.id !== "product");

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

      // All other modules remain flat
      ...otherModules.map((m) => ({
        path: m.path.slice(1),
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
