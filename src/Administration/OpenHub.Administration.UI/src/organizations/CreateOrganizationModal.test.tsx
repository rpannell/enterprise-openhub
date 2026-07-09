import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { CreateOrganizationModal } from "./CreateOrganizationModal";
import { useAdminRuntime } from "../context/AdminRuntimeContext";

vi.mock("../context/AdminRuntimeContext", () => ({
  useAdminRuntime: vi.fn(),
}));

const mockedUseAdminRuntime = vi.mocked(useAdminRuntime);

function mockRuntime(createOrganization: any) {
  mockedUseAdminRuntime.mockReturnValue({
    roles: ["platform-admin"],
    realmSlug: "client-a",
    hasRole: () => true,
    api: { createOrganization } as any,
  });
}

describe("CreateOrganizationModal", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows a validation error and does not call the API when required fields are empty", async () => {
    const createOrganization = vi.fn();
    mockRuntime(createOrganization);
    const user = userEvent.setup();

    render(<CreateOrganizationModal onCreated={vi.fn()} onCancel={vi.fn()} />);
    await user.click(screen.getByRole("button", { name: "Create" }));

    expect(await screen.findByText("Realm, name, and alias are required.")).toBeInTheDocument();
    expect(createOrganization).not.toHaveBeenCalled();
  });

  it("splits the domains field on commas before submitting", async () => {
    const createOrganization = vi.fn(async () => ({}));
    mockRuntime(createOrganization);
    const onCreated = vi.fn();
    const user = userEvent.setup();

    render(<CreateOrganizationModal onCreated={onCreated} onCancel={vi.fn()} />);

    const [realmSlugInput, aliasInput] = screen.getAllByPlaceholderText("client-c");
    await user.type(realmSlugInput, "client-c");
    await user.type(screen.getByPlaceholderText("Client C Inc."), "Client C Inc.");
    await user.type(aliasInput, "client-c");
    await user.type(screen.getByPlaceholderText("client-c.com"), " client-c.com , www.client-c.com ,, ");

    await user.click(screen.getByRole("button", { name: "Create" }));

    await waitFor(() =>
      expect(createOrganization).toHaveBeenCalledWith({
        realmSlug: "client-c",
        name: "Client C Inc.",
        alias: "client-c",
        domains: ["client-c.com", "www.client-c.com"],
      })
    );
    await waitFor(() => expect(onCreated).toHaveBeenCalled());
  });

  it("disables the submit button while saving and re-enables it after a failure", async () => {
    let resolveCreate!: (value: unknown) => void;
    const createOrganization = vi.fn(
      () =>
        new Promise((_resolve, reject) => {
          resolveCreate = reject;
        })
    );
    mockRuntime(createOrganization);
    const user = userEvent.setup();

    render(<CreateOrganizationModal onCreated={vi.fn()} onCancel={vi.fn()} />);
    const [realmSlugInput, aliasInput] = screen.getAllByPlaceholderText("client-c");
    await user.type(realmSlugInput, "client-c");
    await user.type(screen.getByPlaceholderText("Client C Inc."), "Client C Inc.");
    await user.type(aliasInput, "client-c");

    const submitButton = screen.getByRole("button", { name: /create/i });
    await user.click(submitButton);

    await waitFor(() => expect(submitButton).toBeDisabled());
    expect(screen.getByRole("button", { name: "Creating…" })).toBeDisabled();

    resolveCreate(Object.assign(new Error("conflict"), { status: 409, body: { detail: "Realm slug already exists." } }));

    expect(await screen.findByText("Realm slug already exists.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Create" })).not.toBeDisabled();
  });

  it("shows a fallback message when the error has no body detail", async () => {
    const createOrganization = vi.fn(async () => {
      throw new Error("network down");
    });
    mockRuntime(createOrganization);
    const user = userEvent.setup();

    render(<CreateOrganizationModal onCreated={vi.fn()} onCancel={vi.fn()} />);
    const [realmSlugInput, aliasInput] = screen.getAllByPlaceholderText("client-c");
    await user.type(realmSlugInput, "client-c");
    await user.type(screen.getByPlaceholderText("Client C Inc."), "Client C Inc.");
    await user.type(aliasInput, "client-c");

    await user.click(screen.getByRole("button", { name: "Create" }));

    expect(
      await screen.findByText("Could not create organization. Confirm the realm has already been provisioned.")
    ).toBeInTheDocument();
  });
});
