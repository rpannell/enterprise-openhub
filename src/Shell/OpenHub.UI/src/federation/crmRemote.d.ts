// Ambient module declaration for the dynamically federated import — TypeScript
// has no way to know the shape of a module loaded at runtime from a separately
// built remote, so this is a hand-written type contract, not a generated one.
// Keep in sync with OpenHub.CRM.UI's exposed CrmApp props.
declare module "crmRemote/CrmApp" {
  import type { ComponentType } from "react";
  import type { HostAuthContext } from "./HostAuthContext";

  const CrmApp: ComponentType<{ auth: HostAuthContext }>;
  export default CrmApp;
}
