import { useState } from "react";
import { useAuth } from "../../auth/AuthProvider";

/**
 * Product Management has two sub-areas with different roles:
 * - Catalog: product-manager
 * - Warehouse: warehouse-staff
 * A user with either role sees this module; the tabs below are
 * individually gated so a warehouse-only user only sees Warehouse.
 */
export default function ProductApp() {
  const { hasRole } = useAuth();
  const canCatalog = hasRole("product-manager");
  const canWarehouse = hasRole("warehouse-staff");
  const [tab, setTab] = useState<"catalog" | "warehouse">(
    canCatalog ? "catalog" : "warehouse"
  );

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
          <button
            onClick={() => setTab("catalog")}
            className={`px-4 py-2 text-sm ${
              tab === "catalog"
                ? "border-b-2 border-accent font-medium text-accent"
                : "text-slate-500 dark:text-slate-400"
            }`}
          >
            Catalog
          </button>
        )}
        {canWarehouse && (
          <button
            onClick={() => setTab("warehouse")}
            className={`px-4 py-2 text-sm ${
              tab === "warehouse"
                ? "border-b-2 border-accent font-medium text-accent"
                : "text-slate-500 dark:text-slate-400"
            }`}
          >
            Warehouse
          </button>
        )}
      </div>

      <div className="rounded-lg border border-slate-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900 text-sm text-slate-500 dark:text-slate-400">
        {tab === "catalog"
          ? "Catalog and pricing management mounts here."
          : "Warehouse inventory and stock management mounts here."}
      </div>
    </div>
  );
}
