import { lazy } from "react";
import {
  LifeBuoy,
  ShieldCheck,
  Package,
  ShoppingCart,
  Users,
  type LucideIcon,
} from "lucide-react";

export interface ModuleDef {
  id: string;
  name: string;
  description: string;
  path: string;
  // Keycloak role(s) that unlock this tile. User needs ANY one of these.
  roles: string[];
  // lucide-react icon component — shown on the dashboard tile and sidebar link
  Icon: LucideIcon;
  // Omitted for modules router.tsx wires up specially instead of rendering
  // flat — the federated remotes (admin, crm, product) need an auth prop
  // this generic slot can't carry, so each gets its own *RemoteMount
  // wrapper in router.tsx (see AdminRemoteMount there).
  Component?: React.LazyExoticComponent<React.ComponentType>;
}

export const modules: ModuleDef[] = [
  {
    id: "helpdesk",
    name: "Help Desk",
    description: "Support tickets and customer issue tracking",
    path: "/helpdesk",
    roles: ["helpdesk-user", "helpdesk-admin"],
    Icon: LifeBuoy,
    Component: lazy(() => import("../modules/helpdesk/HelpDeskApp")),
  },
  {
    id: "admin",
    name: "Administration",
    description: "Organizations, users, and roles",
    path: "/admin",
    // "platform-admin" (organization onboarding, platform-admin realm only) and
    // "tenant-admin" (self-service user management within one's own organization)
    // — AdminApp itself further gates each tab by which of the two the caller holds.
    roles: ["platform-admin", "tenant-admin"],
    Icon: ShieldCheck,
    // No Component here — this module is a Module Federation remote
    // (src/Administration/OpenHub.Administration.UI), loaded via a federated dynamic
    // import in router.tsx, not a local lazy() import like the others.
  },
  {
    id: "product",
    name: "Product Management",
    description: "Catalog, pricing, and warehouse inventory",
    path: "/product",
    roles: ["product-manager", "warehouse-staff"],
    Icon: Package,
    // No Component here — this module is a Module Federation remote
    // (src/ProductManager/OpenHub.ProductManager.UI), loaded via a federated
    // dynamic import in router.tsx, not a local lazy() import like the others.
  },
  {
    id: "ordering",
    name: "Ordering",
    description: "Order entry, fulfillment, and tracking",
    path: "/ordering",
    roles: ["ordering-user"],
    Icon: ShoppingCart,
    Component: lazy(() => import("../modules/ordering/OrderingApp")),
  },
  {
    id: "crm",
    name: "CRM",
    description: "Clients and the sales rep hierarchy responsible for them",
    path: "/crm",
    roles: ["crm-user"],
    Icon: Users,
    // No Component here — this module is a Module Federation remote
    // (src/CRM/OpenHub.CRM.UI), loaded via a federated dynamic import in
    // router.tsx, not a local lazy() import like the others.
  },
];
