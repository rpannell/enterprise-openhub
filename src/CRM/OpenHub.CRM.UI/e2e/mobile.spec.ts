import { test, expect, type Page } from "@playwright/test";

/**
 * Regression coverage for the "double scrollbar" bug: every modal in this remote
 * (ClientForm, SalesPersonForm, SalesRoleForm, SalesOrgUnitForm, SalesAssignmentForm,
 * ConfirmDialog) is taller than a mobile viewport, so without body-scroll locking the modal's
 * own overlay AND the page behind it both become independently scrollable at once. All six
 * share the useLockBodyScroll() hook (src/hooks/useLockBodyScroll.ts) — this exercises one
 * real modal (ClientForm) against the "mobile" Playwright project (Chromium/Pixel 5 viewport —
 * see playwright.config.ts for why not the WebKit-based "iPhone 12" preset) as representative
 * coverage for that shared mechanism, rather than repeating the same assertion once per modal.
 *
 * Standalone dev harness (bootstrap.tsx + fakeHostAuth — role "crm-user", realmSlug
 * "client-a"). No CRM.Api/SalesOrg.Api is started for this suite, so /crmapi and
 * /salesorgapi calls are stubbed at the browser network layer.
 */
async function stubCrmApi(page: Page) {
  await page.route("**/crmapi/api/v1/clients", async (route) => {
    await route.fulfill({ json: [] });
  });
  await page.route("**/salesorgapi/api/v1/sales-people", async (route) => {
    await route.fulfill({ json: [{ id: "sp-1", firstName: "Jane", lastName: "Rep" }] });
  });
}

test.describe("mobile viewport — modal body-scroll lock", () => {
  test.beforeEach(async ({ page }) => {
    await stubCrmApi(page);
    await page.goto("/crm/clients");
  });

  test("opening a modal locks page scroll; closing it restores the previous value", async ({ page }) => {
    const bodyOverflow = () => page.evaluate(() => getComputedStyle(document.body).overflow);

    // Generous timeout: this remote's dev server compiles each route lazily on first
    // request (see playwright.config.ts), so the very first assertion after navigating
    // can take longer than Playwright's 5s default while that chunk compiles.
    await expect(page.getByRole("button", { name: "New Client" })).toBeEnabled({ timeout: 15_000 });
    const overflowBefore = await bodyOverflow();
    expect(overflowBefore).not.toBe("hidden");

    await page.getByRole("button", { name: "New Client" }).click();
    await expect(page.getByRole("heading", { name: "New Client" })).toBeVisible();
    expect(await bodyOverflow()).toBe("hidden");

    await page.getByRole("button", { name: "Cancel" }).click();
    await expect(page.getByRole("heading", { name: "New Client" })).toHaveCount(0);
    expect(await bodyOverflow()).toBe(overflowBefore);
  });
});
