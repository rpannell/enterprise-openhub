import { useAuth } from "../auth/AuthProvider";
import { accountConsoleUrl } from "../keycloak";

/**
 * Everything here comes from the token Keycloak already gave the browser
 * at login — no additional API calls. The "manage account" button just
 * opens Keycloak's own built-in Account Console in a new tab, where the
 * user can change their name, email, password, etc. directly with Keycloak.
 */
export default function Account() {
  const { username, roles } = useAuth();

  return (
    <div className="max-w-xl">
      <h1 className="mb-1 text-xl font-semibold text-slate-800 dark:text-slate-100">
        Your account
      </h1>
      <p className="mb-6 text-sm text-slate-500 dark:text-slate-400">
        Profile and password changes are handled by Keycloak, not this app.
      </p>

      <div className="rounded-lg border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
        <dl className="mb-5 space-y-2 text-sm">
          <div className="flex justify-between">
            <dt className="text-slate-500 dark:text-slate-400">Username</dt>
            <dd className="font-medium text-slate-800 dark:text-slate-100">
              {username}
            </dd>
          </div>
          <div className="flex justify-between">
            <dt className="text-slate-500 dark:text-slate-400">Roles</dt>
            <dd className="font-medium text-slate-800 dark:text-slate-100">
              {roles.length > 0 ? roles.join(", ") : "—"}
            </dd>
          </div>
        </dl>

        <a
          href={accountConsoleUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center rounded-md bg-accent px-4 py-2 text-sm font-medium text-white hover:bg-accent/90"
        >
          Manage account in Keycloak
        </a>
      </div>
    </div>
  );
}
