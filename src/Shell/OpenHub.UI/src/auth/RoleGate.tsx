import type { ReactNode } from "react";
import { useAuth } from "./AuthProvider";

/**
 * Wrap any route or section with this to require one or more roles.
 * Renders `fallback` (or nothing) if the user lacks every required role.
 */
export function RoleGate({
  roles,
  children,
  fallback = null,
}: {
  roles: string[];
  children: ReactNode;
  fallback?: ReactNode;
}) {
  const { hasAnyRole } = useAuth();
  if (!hasAnyRole(roles)) return <>{fallback}</>;
  return <>{children}</>;
}
