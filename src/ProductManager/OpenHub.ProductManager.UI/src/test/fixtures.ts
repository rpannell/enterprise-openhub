import type { HostAuthContext } from "../federation/HostAuthContext";
import type {
  LookupsResponse,
  LookupItem,
  Product,
  CreateProductRequest,
  UpdateProductRequest,
} from "../types";

export function buildHostAuth(overrides: Partial<HostAuthContext> = {}): HostAuthContext {
  return {
    roles: ["product-manager", "warehouse-staff"],
    realmSlug: "client-a",
    getToken: async () => "test-token",
    ...overrides,
  };
}

export function buildLookupItem(overrides: Partial<LookupItem> = {}): LookupItem {
  return {
    id: "lookup-1",
    code: "LOOKUP",
    name: "Lookup Item",
    ...overrides,
  };
}

export function buildLookups(overrides: Partial<LookupsResponse> = {}): LookupsResponse {
  return {
    conditionTypes: [buildLookupItem({ id: "cond-1", code: "NEW", name: "New" })],
    availabilityStatuses: [buildLookupItem({ id: "avail-1", code: "IN_STOCK", name: "In Stock" })],
    unitsOfMeasure: [
      { id: "uom-1", code: "KG", name: "Kilogram", abbreviation: "kg", uomType: "WEIGHT" },
    ],
    currencies: [{ id: "cur-1", code: "USD", name: "US Dollar", symbol: "$" }],
    priceTypes: [buildLookupItem({ id: "price-1", code: "LIST", name: "List Price" })],
    manufacturers: [buildLookupItem({ id: "mfr-1", code: null, name: "Acme Corp" })],
    brands: [buildLookupItem({ id: "brand-1", code: null, name: "Acme Brand" })],
    ...overrides,
  };
}

export function buildProduct(overrides: Partial<Product> = {}): Product {
  return {
    id: "product-1",
    sku: "WIDGET-001",
    name: "Widget",
    description: "A widget.",
    shortDescription: "Widget",
    manufacturerName: "Acme Corp",
    brandName: "Acme Brand",
    conditionTypeName: "New",
    availabilityStatusName: "In Stock",
    manufacturerPartNumber: "MPN-1",
    isActive: true,
    isPublished: false,
    weight: 1.5,
    weightUomCode: "KG",
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    prices: [],
    ...overrides,
  };
}

export function buildCreateProductRequest(overrides: Partial<CreateProductRequest> = {}): CreateProductRequest {
  return {
    sku: "WIDGET-002",
    name: "Widget Two",
    manufacturer: "Acme Corp",
    brand: "Acme Brand",
    ...overrides,
  };
}

export function buildUpdateProductRequest(overrides: Partial<UpdateProductRequest> = {}): UpdateProductRequest {
  return {
    name: "Updated Widget",
    isActive: true,
    isPublished: true,
    ...overrides,
  };
}
