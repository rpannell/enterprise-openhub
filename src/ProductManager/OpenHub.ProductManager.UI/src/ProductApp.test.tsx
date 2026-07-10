import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import ProductApp from "./ProductApp";
import { buildHostAuth } from "./test/fixtures";

const fakeApi = {
  getLookups: vi.fn(async () => ({
    conditionTypes: [],
    availabilityStatuses: [],
    unitsOfMeasure: [],
    currencies: [],
    priceTypes: [],
    manufacturers: [],
    brands: [],
  })),
  getProducts: vi.fn(async () => ({ items: [], page: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
  getProduct: vi.fn(),
  createProduct: vi.fn(),
  updateProduct: vi.fn(),
  createManufacturer: vi.fn(),
  createBrand: vi.fn(),
};

vi.mock("./services/productManagerApi", () => ({
  createProductManagerApi: vi.fn(() => fakeApi),
}));

function goTo(path: string) {
  window.history.pushState({}, "", path);
}

describe("ProductApp role gating", () => {
  beforeEach(() => {
    goTo("/product");
  });

  afterEach(() => {
    goTo("/");
  });

  it("a product-manager sees the Catalog tab and is redirected to catalog", async () => {
    render(<ProductApp auth={buildHostAuth({ roles: ["product-manager"] })} />);

    expect(screen.getByRole("link", { name: "Catalog" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Warehouse" })).not.toBeInTheDocument();
    await waitFor(() => expect(window.location.pathname).toBe("/product/catalog"));
  });

  it("a warehouse-staff-only user sees the Warehouse tab but not Catalog, and is still redirected to catalog", async () => {
    render(<ProductApp auth={buildHostAuth({ roles: ["warehouse-staff"] })} />);

    expect(screen.queryByRole("link", { name: "Catalog" })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Warehouse" })).toBeInTheDocument();
    // The index route redirects to "catalog" unconditionally — there's no Warehouse
    // screen yet, so this is the module's current (pre-existing) behavior, not new.
    await waitFor(() => expect(window.location.pathname).toBe("/product/catalog"));
  });

  it("a user with both roles sees both tabs", async () => {
    render(<ProductApp auth={buildHostAuth({ roles: ["product-manager", "warehouse-staff"] })} />);

    expect(screen.getByRole("link", { name: "Catalog" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Warehouse" })).toBeInTheDocument();
  });

  it("a user with neither role sees no tabs but the catalog route still renders (no per-route gate)", async () => {
    render(<ProductApp auth={buildHostAuth({ roles: [] })} />);

    expect(screen.queryByRole("link", { name: "Catalog" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Warehouse" })).not.toBeInTheDocument();
    await waitFor(() => expect(window.location.pathname).toBe("/product/catalog"));
    // No "Add Product" button without product-manager, but the list itself has no
    // role gate — same pre-existing behavior as before this module was federated.
    expect(screen.queryByRole("button", { name: "Add Product" })).not.toBeInTheDocument();
  });

  it("a product-manager sees the Add Product button on the catalog list", async () => {
    render(<ProductApp auth={buildHostAuth({ roles: ["product-manager"] })} />);
    await waitFor(() => expect(screen.getByRole("button", { name: "Add Product" })).toBeInTheDocument());
  });

  it("mounts under the /product basename", async () => {
    goTo("/product/catalog");
    render(<ProductApp auth={buildHostAuth({ roles: ["product-manager"] })} />);

    await waitFor(() => expect(window.location.pathname).toBe("/product/catalog"));
    expect(screen.getByRole("link", { name: "Catalog" })).toBeInTheDocument();
  });
});
