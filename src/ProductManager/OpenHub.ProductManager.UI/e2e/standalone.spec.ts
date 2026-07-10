import { test, expect, type Page } from "@playwright/test";

/**
 * Exercises the standalone dev harness (bootstrap.tsx + fakeHostAuth —
 * roles: ["product-manager", "warehouse-staff"], realmSlug "client-a"). No
 * ProductManager.Api is started for this suite, so every /pm/api/v1/* call
 * is stubbed at the browser network layer via page.route() — this remote's
 * own README notes that real calls 401 in this mode anyway (no live backend
 * behind the dev server's proxy), so stubbing is what actually unlocks
 * testing the list/form flows instead of just watching every view fall
 * straight into its error-banner state.
 */
const LOOKUPS = {
  conditionTypes: [{ id: "cond-1", code: "NEW", name: "New" }],
  availabilityStatuses: [{ id: "avail-1", code: "IN_STOCK", name: "In Stock" }],
  unitsOfMeasure: [{ id: "uom-1", code: "KG", name: "Kilogram", abbreviation: "kg", uomType: "WEIGHT" }],
  currencies: [{ id: "cur-1", code: "USD", name: "US Dollar", symbol: "$" }],
  priceTypes: [{ id: "price-1", code: "LIST", name: "List Price" }],
  manufacturers: [{ id: "mfr-1", code: null, name: "Acme Corp" }],
  brands: [{ id: "brand-1", code: null, name: "Acme Brand" }],
};

const PRODUCT = {
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
};

async function stubProductManagerApi(page: Page) {
  await page.route("**/pm/api/v1/lookups", async (route) => {
    await route.fulfill({ json: LOOKUPS });
  });
  // "*" suffix (not an exact-path match) — getProducts() always appends a query
  // string (?page=1&pageSize=25&...), and a bare "**/pm/api/v1/products" glob
  // only matches that exact suffix, not a URL with query params tacked on.
  await page.route("**/pm/api/v1/products*", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({ json: { items: [PRODUCT], page: 1, pageSize: 25, totalCount: 1, totalPages: 1 } });
    } else {
      await route.fulfill({ status: 201, json: PRODUCT });
    }
  });
  // Registered after the list route above — Playwright matches the
  // most-recently-registered route first, so this single-product GET (used by
  // ProductEditPage) takes priority over the list stub for "/products/:id" URLs.
  await page.route("**/pm/api/v1/products/*", async (route) => {
    await route.fulfill({ json: PRODUCT });
  });
}

test.describe("standalone harness", () => {
  test.beforeEach(async ({ page }) => {
    await stubProductManagerApi(page);
    // ProductApp mounts its own <BrowserRouter basename="/product">, which renders
    // nothing at all for a URL outside that basename (see rspack.config.ts's
    // historyApiFallback comment) — "/" alone would show a blank page.
    await page.goto("/product");
  });

  test("shows the Catalog and Warehouse tabs for the fakeHostAuth dual-role user", async ({ page }) => {
    await expect(page.getByRole("link", { name: "Catalog" })).toBeVisible();
    await expect(page.getByRole("link", { name: "Warehouse" })).toBeVisible();
  });

  test("the index route redirects to the catalog list, which loads", async ({ page }) => {
    await expect(page).toHaveURL(/\/product\/catalog$/);
    await expect(page.getByText("WIDGET-001")).toBeVisible();
    await expect(page.getByRole("button", { name: "Add Product" })).toBeVisible();
  });

  test("Add Product navigates to the create form, which blocks submit on empty required fields", async ({ page }) => {
    await page.getByRole("button", { name: "Add Product" }).click();
    await expect(page).toHaveURL(/\/product\/catalog\/new$/);
    await expect(page.getByRole("heading", { name: "New Product" })).toBeVisible();

    await page.getByRole("button", { name: "Create Product" }).click();
    await expect(page.getByText("SKU is required.")).toBeVisible();
    // Validation blocked submission — still on the create form.
    await expect(page.getByRole("heading", { name: "New Product" })).toBeVisible();
  });

  test("clicking the edit icon on a row navigates to the edit form pre-filled with that product", async ({ page }) => {
    await page.getByTitle("Edit product").click();
    await expect(page).toHaveURL(/\/product\/catalog\/product-1\/edit$/);
    await expect(page.getByRole("heading", { name: "Edit Product" })).toBeVisible();
    await expect(page.getByPlaceholder("Product display name")).toHaveValue("Widget");
  });
});
