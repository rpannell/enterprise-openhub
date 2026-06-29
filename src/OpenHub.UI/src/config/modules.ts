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
  Component: React.LazyExoticComponent<React.ComponentType>;
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
    description: "Users, roles, and system settings",
    path: "/admin",
    roles: ["system-admin"],
    Icon: ShieldCheck,
    Component: lazy(() => import("../modules/admin/AdminApp")),
  },
  {
    id: "product",
    name: "Product Management",
    description: "Catalog, pricing, and warehouse inventory",
    path: "/product",
    roles: ["product-manager", "warehouse-staff"],
    Icon: Package,
    Component: lazy(() => import("../modules/product/ProductApp")),
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
    description: "Accounts, contacts, and customer history",
    path: "/crm",
    roles: ["crm-user"],
    Icon: Users,
    Component: lazy(() => import("../modules/crm/CrmApp")),
  },
];
