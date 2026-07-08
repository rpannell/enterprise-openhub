import { createContext, useContext, useMemo, type ReactNode } from "react";
import type { HostAuthContext } from "../federation/HostAuthContext";
import { createAdministrationApi, type AdministrationApi } from "../services/administrationApi";

/**
 * This context never crosses the federation boundary — it's an internal
 * implementation detail of this remote's own component tree, built on top
 * of the (federated, singleton) React instance. Safe to use freely, unlike
 * a context shared *across* host and remote would be.
 */
interface AdminRuntime {
  roles: string[];
  realmSlug: string;
  hasRole: (role: string) => boolean;
  api: AdministrationApi;
}

const AdminRuntimeContext = createContext<AdminRuntime | undefined>(undefined);

export function AdminRuntimeProvider({
  auth,
  children,
}: {
  auth: HostAuthContext;
  children: ReactNode;
}) {
  const value = useMemo<AdminRuntime>(
    () => ({
      roles: auth.roles,
      realmSlug: auth.realmSlug,
      hasRole: (role: string) => auth.roles.includes(role),
      api: createAdministrationApi(auth.getToken),
    }),
    [auth]
  );

  return <AdminRuntimeContext.Provider value={value}>{children}</AdminRuntimeContext.Provider>;
}

export function useAdminRuntime(): AdminRuntime {
  const ctx = useContext(AdminRuntimeContext);
  if (!ctx) throw new Error("useAdminRuntime must be used inside <AdminRuntimeProvider>");
  return ctx;
}
