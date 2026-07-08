import { Outlet, NavLink } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";

export default function ProductApp() {
  const { hasRole } = useAuth();
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

      <Outlet />
    </div>
  );
}
