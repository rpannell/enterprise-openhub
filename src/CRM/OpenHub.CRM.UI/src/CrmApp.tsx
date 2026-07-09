import "./index.css";
import { BrowserRouter, Routes, Route, Navigate, NavLink } from "react-router-dom";
import type { HostAuthContext } from "./federation/HostAuthContext";
import { CrmRuntimeProvider, useCrmRuntime } from "./context/CrmRuntimeContext";
import ClientList from "./clients/ClientList";
import SalesHierarchyView from "./salesorg/SalesHierarchyView";

/**
 * The one thing this remote exposes (see rspack.config.ts `exposes`). Owns
 * its own nested routing AND its own <BrowserRouter> — react-router-dom is
 * deliberately NOT a federated singleton (see rspack.config.ts and
 * docs/module-federation.md "The router-sharing deadlock"), so this remote
 * cannot read Router context from whichever router the shell mounted; it
 * has to bring its own. basename="/crm" keeps it scoped to exactly the
 * path the shell mounts this component at (see OpenHub.UI/src/router.tsx's
 * "crm/*" route) — two independent router instances, both reading the
 * same actual browser URL, is unusual but fine for a single fixed mount
 * point with no need for cross-router navigation.
 */
export default function CrmApp({ auth }: { auth: HostAuthContext }) {
  return (
    <BrowserRouter basename="/crm">
      <CrmRuntimeProvider auth={auth}>
        <CrmShell />
      </CrmRuntimeProvider>
    </BrowserRouter>
  );
}

function CrmShell() {
  const { hasRole } = useCrmRuntime();
  const canManage = hasRole("crm-user");

  const tabClass = ({ isActive }: { isActive: boolean }) =>
    `px-4 py-2 text-sm ${
      isActive
        ? "border-b-2 border-accent font-medium text-accent"
        : "text-slate-500 hover:text-slate-700 dark:text-slate-400 dark:hover:text-slate-200"
    }`;

  return (
    <div>
      <h1 className="mb-1 text-xl font-semibold text-slate-800 dark:text-slate-100">CRM</h1>
      <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">
        Clients and the sales person responsible for each.
      </p>

      <div className="mb-4 flex gap-1 border-b border-slate-200 dark:border-slate-800">
        {canManage && (
          <NavLink to="clients" className={tabClass}>
            Clients
          </NavLink>
        )}
        {canManage && (
          <NavLink to="sales-hierarchy" className={tabClass}>
            Sales Hierarchy
          </NavLink>
        )}
      </div>

      <Routes>
        <Route index element={<IndexRedirect />} />
        {canManage && <Route path="clients" element={<ClientList />} />}
        {canManage && <Route path="sales-hierarchy" element={<SalesHierarchyView />} />}
      </Routes>
    </div>
  );
}

function IndexRedirect() {
  return <Navigate to="clients" replace />;
}
