import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import keycloak from "../keycloak";

interface AuthState {
  initialized: boolean;
  authenticated: boolean;
  username: string | null;
  roles: string[];
  hasRole: (role: string) => boolean;
  hasAnyRole: (roles: string[]) => boolean;
  logout: () => void;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [initialized, setInitialized] = useState(false);
  const [authenticated, setAuthenticated] = useState(false);
  const [roles, setRoles] = useState<string[]>([]);
  const initStarted = useRef(false);

  useEffect(() => {
    if (initStarted.current) return;
    initStarted.current = true;

    keycloak
      .init({
        onLoad: "login-required", // every route behind this provider requires login
        pkceMethod: "S256",
        checkLoginIframe: false,
      })
      .then((authed) => {
        setAuthenticated(authed);
        // Realm roles + this client's roles, merged and de-duplicated.
        const realmRoles = keycloak.realmAccess?.roles ?? [];
        const clientId = import.meta.env.VITE_KEYCLOAK_CLIENT_ID as string;
        const clientRoles = keycloak.resourceAccess?.[clientId]?.roles ?? [];
        setRoles(Array.from(new Set([...realmRoles, ...clientRoles])));
        setInitialized(true);

        // Keep the token fresh; Keycloak handles silent refresh under the hood.
        setInterval(() => {
          keycloak.updateToken(60).catch(() => keycloak.login());
        }, 30000);
      })
      .catch((err) => {
        console.error("Keycloak init failed", err);
        setInitialized(true);
      });
  }, []);

  const value = useMemo<AuthState>(
    () => ({
      initialized,
      authenticated,
      username: keycloak.tokenParsed?.preferred_username ?? null,
      roles,
      hasRole: (role: string) => roles.includes(role),
      hasAnyRole: (required: string[]) =>
        required.length === 0 || required.some((r) => roles.includes(r)),
      logout: () => keycloak.logout({ redirectUri: window.location.origin }),
    }),
    [initialized, authenticated, roles]
  );

  if (!initialized) {
    return (
      <div className="flex h-screen items-center justify-center text-slate-500 dark:bg-slate-950 dark:text-slate-400">
        Checking your session…
      </div>
    );
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside <AuthProvider>");
  return ctx;
}
