import "./index.css";
import { BrowserRouter, Routes, Route, Navigate, NavLink } from "react-router-dom";
import type { HostAuthContext } from "./federation/HostAuthContext";
import { ProductRuntimeProvider, useProductRuntime } from "./context/ProductRuntimeContext";
import ProductList from "./catalog/ProductList";
import ProductCreatePage from "./catalog/ProductCreatePage";
import ProductEditPage from "./catalog/ProductEditPage";

/**
 * The one thing this remote exposes (see rspack.config.ts `exposes`). Owns
 * its own nested routing AND its own <BrowserRouter> — react-router-dom is
 * deliberately NOT a federated singleton (see rspack.config.ts and
 * OpenHub.UI/docs/module-federation.md "The router-sharing deadlock"), so
 * this remote cannot read Router context from whichever router the shell
 * mounted; it has to bring its own. basename="/product" keeps it scoped to
 * exactly the path the shell mounts this component at (see
 * OpenHub.UI/src/router.tsx's "product/*" route) — two independent router
 * instances, both reading the same actual browser URL, is unusual but fine
 * for a single fixed mount point with no need for cross-router navigation.
 */
export default function ProductApp({ auth }: { auth: HostAuthContext }) {
  return (
    <BrowserRouter basename="/product">
      <ProductRuntimeProvider auth={auth}>
        <ProductShell />
      </ProductRuntimeProvider>
    </BrowserRouter>
  );
}

function ProductShell() {
  const { hasRole } = useProductRuntime();
  const canCatalog = hasRole("product-manager");
  const canWarehouse = hasRole("warehouse-staff");

  const tabClass = ({ isActive }: { isActive: boolean }) =>
    `px-4 py-2 text-sm ${
      isActive
        ? "border-b-2 border-accent font-medium text-accent"
        : "text-slate-500 hover:text-slate-700 dark:text-slate-400 dark:hover:text-slate-200"
    }`;

  return (
    <div>
      <h1 className="mb-1 text-xl font-semibold text-slate-800 dark:text-slate-100">
        Product Management
      </h1>
      <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">
        Catalog, pricing, and warehouse inventory.
      </p>

      <div className="mb-4 flex gap-1 border-b border-slate-200 dark:border-slate-800">
        {canCatalog && (
          <NavLink to="catalog" className={tabClass}>
            Catalog
          </NavLink>
        )}
        {canWarehouse && (
          <NavLink to="warehouse" className={tabClass}>
            Warehouse
          </NavLink>
        )}
      </div>

      <Routes>
        <Route index element={<Navigate to="catalog" replace />} />
        <Route path="catalog" element={<ProductList />} />
        <Route path="catalog/new" element={<ProductCreatePage />} />
        <Route path="catalog/:id/edit" element={<ProductEditPage />} />
      </Routes>
    </div>
  );
}
