import { describe, expect, it } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import SalesHierarchyView from "./SalesHierarchyView";
import { CrmRuntimeProvider } from "../context/CrmRuntimeContext";
import { buildHostAuth } from "../test/fixtures";

// Exercised against the real crmApi + MSW handlers (src/test/msw/handlers.ts), same approach
// as CrmRuntimeContext.test.tsx, rather than mocking createCrmApi like CrmApp.test.tsx does —
// this component's whole job is wiring together several SalesOrg endpoints, so the MSW-level
// integration test is the more meaningful one here.
function renderView() {
  return render(
    <CrmRuntimeProvider auth={buildHostAuth()}>
      <SalesHierarchyView />
    </CrmRuntimeProvider>
  );
}

describe("SalesHierarchyView", () => {
  it("renders the org unit tree from the flat list", async () => {
    renderView();

    expect(await screen.findByText("Carolinas District")).toBeInTheDocument();
  });

  it("populates the sales person dropdown", async () => {
    renderView();

    await waitFor(() => expect(screen.getByRole("combobox")).not.toBeDisabled());
    expect(screen.getByRole("option", { name: /Jane Rep/ })).toBeInTheDocument();
  });

  it("shows the manager chain and reports once a sales person is selected", async () => {
    const user = userEvent.setup();
    renderView();

    await waitFor(() => expect(screen.getByRole("combobox")).not.toBeDisabled());
    await user.selectOptions(screen.getByRole("combobox"), "person-1");

    expect(await screen.findByText(/Mona Manager/)).toBeInTheDocument();
    expect(await screen.findByText(/Ricky Report/)).toBeInTheDocument();
  });
});
