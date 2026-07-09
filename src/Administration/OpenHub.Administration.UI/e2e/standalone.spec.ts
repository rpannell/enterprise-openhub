import { test, expect, type Page } from "@playwright/test";

/**
 * Exercises the standalone dev harness (bootstrap.tsx + fakeHostAuth —
 * roles: ["platform-admin", "tenant-admin"], realmSlug "client-a"). No
 * Administration.Api or Keycloak is started for this suite, so every
 * /adm/api/v1/* call is stubbed at the browser network layer via
 * page.route() — this remote's own README notes that real calls 401 in this
 * mode anyway (no live backend behind the dev server's proxy), so stubbing
 * is what actually unlocks testing the modal/reload flows instead of just
 * watching every view fall straight into its error-banner state.
 */
const ORG = { id: "org-1", realmSlug: "client-a", name: "Client A Inc.", alias: "client-a", enabled: true, domains: ["client-a.com"] };
const USER = { id: "user-1", username: "jdoe", email: "jdoe@client-a.com", firstName: "Jane", lastName: "Doe", enabled: true, roles: ["product-manager"] };
const ROLE = { name: "product-manager", description: "Product catalog access" };

async function stubAdministrationApi(page: Page) {
  await page.route("**/adm/api/v1/organizations", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({ json: [ORG] });
    } else {
      await route.fulfill({ status: 201, json: ORG });
    }
  });
  await page.route("**/adm/api/v1/organizations/*/users", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({ json: [USER] });
    } else {
      await route.fulfill({ status: 201, json: USER });
    }
  });
  await page.route("**/adm/api/v1/organizations/*/roles", async (route) => {
    await route.fulfill({ json: [ROLE] });
  });
}

test.describe("standalone harness", () => {
  test.beforeEach(async ({ page }) => {
    await stubAdministrationApi(page);
    // AdminApp mounts its own <BrowserRouter basename="/admin">, which renders
    // nothing at all for a URL outside that basename (see rspack.config.ts's
    // historyApiFallback comment) — "/" alone would show a blank page.
    await page.goto("/admin");
  });

  test("shows both tabs for the fakeHostAuth dual-role user", async ({ page }) => {
    await expect(page.getByRole("link", { name: "Organizations" })).toBeVisible();
    await expect(page.getByRole("link", { name: "Users" })).toBeVisible();
  });

  test("switching to the Organizations tab loads the list", async ({ page }) => {
    await page.getByRole("link", { name: "Organizations" }).click();
    await expect(page).toHaveURL(/\/admin\/organizations$/);
    await expect(page.getByText("Client A Inc.")).toBeVisible();
    await expect(page.getByRole("button", { name: "New Organization" })).toBeVisible();
  });

  test("New Organization modal opens, blocks submit on empty required fields, and closes on cancel", async ({ page }) => {
    await page.getByRole("link", { name: "Organizations" }).click();
    await page.getByRole("button", { name: "New Organization" }).click();

    await expect(page.getByRole("heading", { name: "New Organization" })).toBeVisible();
    await page.getByRole("button", { name: "Create" }).click();
    await expect(page.getByText("Realm, name, and alias are required.")).toBeVisible();
    // Validation blocked submission — the modal is still open.
    await expect(page.getByRole("heading", { name: "New Organization" })).toBeVisible();

    await page.getByRole("button", { name: "Cancel" }).click();
    await expect(page.getByRole("heading", { name: "New Organization" })).toHaveCount(0);
  });

  test("switching to the Users tab loads the list and New User modal validates required fields", async ({ page }) => {
    await page.getByRole("link", { name: "Users" }).click();
    await expect(page).toHaveURL(/\/admin\/users$/);
    await expect(page.getByText("jdoe", { exact: true })).toBeVisible();

    await page.getByRole("button", { name: "New User" }).click();
    await expect(page.getByRole("heading", { name: "New User" })).toBeVisible();

    await page.getByRole("button", { name: "Create" }).click();
    await expect(page.getByText("Email is required.")).toBeVisible();
  });
});
