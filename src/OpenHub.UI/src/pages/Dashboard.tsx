import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { modules } from "../config/modules";

export default function Dashboard() {
  const { hasAnyRole, username } = useAuth();
  const visibleModules = modules.filter((m) => hasAnyRole(m.roles));

  return (
    <div>
      <h1 className="mb-1 text-xl font-semibold text-slate-800 dark:text-slate-100">
        Welcome, {username}
      </h1>
      <p className="mb-6 text-sm text-slate-500 dark:text-slate-400">
        You have access to {visibleModules.length} of {modules.length}{" "}
        applications.
      </p>

      {visibleModules.length === 0 ? (
        <div className="rounded-lg border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-400">
          No applications are assigned to your account yet. Contact an
          administrator if this looks wrong.
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {visibleModules.map((m) => (
            <Link
              key={m.id}
              to={m.path}
              className="group rounded-lg border border-slate-200 bg-white p-5 transition hover:border-accent hover:shadow-sm dark:border-slate-800 dark:bg-slate-900"
            >
              <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-md bg-accent/10 text-accent">
                <m.Icon size={20} />
              </div>
              <div className="font-medium text-slate-800 group-hover:text-accent dark:text-slate-100">
                {m.name}
              </div>
              <div className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                {m.description}
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
