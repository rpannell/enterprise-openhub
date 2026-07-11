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
import {
  createNotificationConnection,
  type NotificationConnection,
  type RealtimeMessageListener,
} from "../services/realtimeMessaging";

interface AuthState {
  initialized: boolean;
  authenticated: boolean;
  username: string | null;
  roles: string[];
  hasRole: (role: string) => boolean;
  hasAnyRole: (roles: string[]) => boolean;
  logout: () => void;
  /** Returns a fresh bearer token — the one thing federated remotes get instead of their own Keycloak session. */
  getToken: () => Promise<string>;
  /** Subscribes to real-time messages pushed from the backend. Returns an unsubscribe function. */
  onRealtimeMessage: (listener: RealtimeMessageListener) => () => void;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [initialized, setInitialized] = useState(false);
  const [authenticated, setAuthenticated] = useState(false);
  const [roles, setRoles] = useState<string[]>([]);
  const initStarted = useRef(false);
  const notificationConnectionRef = useRef<NotificationConnection | null>(null);

  const getToken = async () => {
    await keycloak.updateToken(30).catch(() => keycloak.login());
    return keycloak.token!;
  };

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

  // Connects once the user is authenticated, disconnects on logout/unmount. Connection-only —
  // no visible UI here; remotes and Shell pages subscribe via onRealtimeMessage below.
  useEffect(() => {
    if (!authenticated) return;

    const hubBaseUrl = import.meta.env.VITE_REALTIME_MESSAGING_URL as string | undefined;
    if (!hubBaseUrl) {
      console.warn("VITE_REALTIME_MESSAGING_URL not set — real-time messaging disabled.");
      return;
    }

    const connection = createNotificationConnection(`${hubBaseUrl}/hubs/notifications`, getToken);
    notificationConnectionRef.current = connection;
    connection.start().catch((err) => console.error("Notification hub connection failed", err));

    return () => {
      notificationConnectionRef.current = null;
      connection.stop();
    };
  }, [authenticated]);

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
      getToken,
      onRealtimeMessage: (listener: RealtimeMessageListener) => {
        if (!notificationConnectionRef.current) return () => {};
        return notificationConnectionRef.current.subscribe(listener);
      },
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
