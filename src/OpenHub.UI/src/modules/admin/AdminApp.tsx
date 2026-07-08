import { Outlet, NavLink } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";

export default function AdminApp() {
  const { hasRole } = useAuth();
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

      <Outlet />
    </div>
  );
}
