import "./index.css";
import { BrowserRouter, Routes, Route, Navigate, NavLink } from "react-router-dom";
import type { HostAuthContext } from "./federation/HostAuthContext";
import { AdminRuntimeProvider, useAdminRuntime } from "./context/AdminRuntimeContext";
import OrganizationList from "./organizations/OrganizationList";
import UserList from "./users/UserList";

/**
 * The one thing this remote exposes (see rspack.config.ts `exposes`). Owns
 * its own nested routing AND its own <BrowserRouter> — react-router-dom is
 * deliberately NOT a federated singleton (see rspack.config.ts and
 * docs/module-federation.md "The router-sharing deadlock"), so this remote
 * cannot read Router context from whichever router the shell mounted; it
 * has to bring its own. basename="/admin" keeps it scoped to exactly the
 * path the shell mounts this component at (see OpenHub.UI/src/router.tsx's
 * "admin/*" route) — two independent router instances, both reading the
 * same actual browser URL, is unusual but fine for a single fixed mount
 * point with no need for cross-router navigation.
 */
export default function AdminApp({ auth }: { auth: HostAuthContext }) {
  return (
    <BrowserRouter basename="/admin">
      <AdminRuntimeProvider auth={auth}>
        <AdminShell />
      </AdminRuntimeProvider>
    </BrowserRouter>
  );
}

function AdminShell() {
  const { hasRole } = useAdminRuntime();
  const canManageOrganizations = hasRole("platform-admin");
  const canManageUsers = hasRole("tenant-admin") || hasRole("platform-admin");

  const tabClass = ({ isActive }: { isActive: boolean }) =>
    `px-4 py-2 text-sm ${
      isActive
        ? "border-b-2 border-accent font-medium text-accent"
        : "text-slate-500 hover:text-slate-700 dark:text-slate-400 dark:hover:text-slate-200"
    }`;

  return (
    <div>
      <h1 className="mb-1 text-xl font-semibold text-slate-800 dark:text-slate-100">
        Administration
      </h1>
      <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">
        Organizations, users, and roles.
      </p>

      <div className="mb-4 flex gap-1 border-b border-slate-200 dark:border-slate-800">
        {canManageOrganizations && (
          <NavLink to="organizations" className={tabClass}>
            Organizations
          </NavLink>
        )}
        {canManageUsers && (
          <NavLink to="users" className={tabClass}>
            Users
          </NavLink>
        )}
      </div>

      <Routes>
        <Route index element={<IndexRedirect />} />
        {canManageOrganizations && <Route path="organizations" element={<OrganizationList />} />}
        {canManageUsers && <Route path="users" element={<UserList />} />}
      </Routes>
    </div>
  );
}

function IndexRedirect() {
  const { hasRole } = useAdminRuntime();
  return <Navigate to={hasRole("platform-admin") ? "organizations" : "users"} replace />;
}
