// Ambient module declaration for the dynamically federated import — TypeScript
// has no way to know the shape of a module loaded at runtime from a separately
// built remote, so this is a hand-written type contract, not a generated one.
// Keep in sync with OpenHub.ProductManager.UI's exposed ProductApp props.
declare module "productRemote/ProductApp" {
  import type { ComponentType } from "react";
  import type { HostAuthContext } from "./HostAuthContext";

  const ProductApp: ComponentType<{ auth: HostAuthContext }>;
  export default ProductApp;
}
