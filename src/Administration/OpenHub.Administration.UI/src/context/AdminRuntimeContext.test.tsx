import { describe, expect, it, vi } from "vitest";
import { render, renderHook, screen } from "@testing-library/react";
import { AdminRuntimeProvider, useAdminRuntime } from "./AdminRuntimeContext";
import { buildHostAuth } from "../test/fixtures";
import * as administrationApi from "../services/administrationApi";

describe("AdminRuntimeProvider / useAdminRuntime", () => {
  it("derives hasRole() from auth.roles", () => {
    const auth = buildHostAuth({ roles: ["tenant-admin"] });
    const { result } = renderHook(() => useAdminRuntime(), {
      wrapper: ({ children }) => <AdminRuntimeProvider auth={auth}>{children}</AdminRuntimeProvider>,
    });

    expect(result.current.hasRole("tenant-admin")).toBe(true);
    expect(result.current.hasRole("platform-admin")).toBe(false);
  });

  it("passes realmSlug through unchanged", () => {
    const auth = buildHostAuth({ realmSlug: "client-z" });
    const { result } = renderHook(() => useAdminRuntime(), {
      wrapper: ({ children }) => <AdminRuntimeProvider auth={auth}>{children}</AdminRuntimeProvider>,
    });

    expect(result.current.realmSlug).toBe("client-z");
  });

  it("builds api via createAdministrationApi(auth.getToken)", () => {
    const spy = vi.spyOn(administrationApi, "createAdministrationApi");
    const auth = buildHostAuth();

    renderHook(() => useAdminRuntime(), {
      wrapper: ({ children }) => <AdminRuntimeProvider auth={auth}>{children}</AdminRuntimeProvider>,
    });

    expect(spy).toHaveBeenCalledWith(auth.getToken);
    spy.mockRestore();
  });

  it("keeps the same api instance across re-renders with the same auth reference", () => {
    const auth = buildHostAuth();
    const { result, rerender } = renderHook(() => useAdminRuntime(), {
      wrapper: ({ children }) => <AdminRuntimeProvider auth={auth}>{children}</AdminRuntimeProvider>,
    });

    const firstApi = result.current.api;
    rerender();

    expect(result.current.api).toBe(firstApi);
  });

  it("builds a new api instance when the auth reference changes", () => {
    let auth = buildHostAuth();
    const { result, rerender } = renderHook(() => useAdminRuntime(), {
      wrapper: ({ children }) => <AdminRuntimeProvider auth={auth}>{children}</AdminRuntimeProvider>,
    });

    const firstApi = result.current.api;
    auth = buildHostAuth({ realmSlug: "client-b" });
    rerender();

    expect(result.current.api).not.toBe(firstApi);
  });

  it("throws when used outside a provider", () => {
    function Consumer() {
      useAdminRuntime();
      return null;
    }
    // Suppress React's expected console.error for the thrown-during-render case.
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    expect(() => render(<Consumer />)).toThrow(
      "useAdminRuntime must be used inside <AdminRuntimeProvider>"
    );
    spy.mockRestore();
  });
});
