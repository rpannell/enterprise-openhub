import { describe, expect, it, vi } from "vitest";
import { render, renderHook } from "@testing-library/react";
import { CrmRuntimeProvider, useCrmRuntime } from "./CrmRuntimeContext";
import { buildHostAuth } from "../test/fixtures";
import * as crmApi from "../services/crmApi";

describe("CrmRuntimeProvider / useCrmRuntime", () => {
  it("derives hasRole() from auth.roles", () => {
    const auth = buildHostAuth({ roles: ["crm-user"] });
    const { result } = renderHook(() => useCrmRuntime(), {
      wrapper: ({ children }) => <CrmRuntimeProvider auth={auth}>{children}</CrmRuntimeProvider>,
    });

    expect(result.current.hasRole("crm-user")).toBe(true);
    expect(result.current.hasRole("platform-admin")).toBe(false);
  });

  it("passes realmSlug through unchanged", () => {
    const auth = buildHostAuth({ realmSlug: "client-z" });
    const { result } = renderHook(() => useCrmRuntime(), {
      wrapper: ({ children }) => <CrmRuntimeProvider auth={auth}>{children}</CrmRuntimeProvider>,
    });

    expect(result.current.realmSlug).toBe("client-z");
  });

  it("builds api via createCrmApi(auth.getToken)", () => {
    const spy = vi.spyOn(crmApi, "createCrmApi");
    const auth = buildHostAuth();

    renderHook(() => useCrmRuntime(), {
      wrapper: ({ children }) => <CrmRuntimeProvider auth={auth}>{children}</CrmRuntimeProvider>,
    });

    expect(spy).toHaveBeenCalledWith(auth.getToken);
    spy.mockRestore();
  });

  it("keeps the same api instance across re-renders with the same auth reference", () => {
    const auth = buildHostAuth();
    const { result, rerender } = renderHook(() => useCrmRuntime(), {
      wrapper: ({ children }) => <CrmRuntimeProvider auth={auth}>{children}</CrmRuntimeProvider>,
    });

    const firstApi = result.current.api;
    rerender();

    expect(result.current.api).toBe(firstApi);
  });

  it("throws when used outside a provider", () => {
    function Consumer() {
      useCrmRuntime();
      return null;
    }
    // Suppress React's expected console.error for the thrown-during-render case.
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    expect(() => render(<Consumer />)).toThrow(
      "useCrmRuntime must be used inside <CrmRuntimeProvider>"
    );
    spy.mockRestore();
  });
});
