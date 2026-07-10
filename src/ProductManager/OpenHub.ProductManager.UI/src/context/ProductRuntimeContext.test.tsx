import { describe, expect, it, vi } from "vitest";
import { render, renderHook, screen } from "@testing-library/react";
import { ProductRuntimeProvider, useProductRuntime } from "./ProductRuntimeContext";
import { buildHostAuth } from "../test/fixtures";
import * as productManagerApi from "../services/productManagerApi";

describe("ProductRuntimeProvider / useProductRuntime", () => {
  it("derives hasRole() from auth.roles", () => {
    const auth = buildHostAuth({ roles: ["product-manager"] });
    const { result } = renderHook(() => useProductRuntime(), {
      wrapper: ({ children }) => <ProductRuntimeProvider auth={auth}>{children}</ProductRuntimeProvider>,
    });

    expect(result.current.hasRole("product-manager")).toBe(true);
    expect(result.current.hasRole("warehouse-staff")).toBe(false);
  });

  it("passes realmSlug through unchanged", () => {
    const auth = buildHostAuth({ realmSlug: "client-z" });
    const { result } = renderHook(() => useProductRuntime(), {
      wrapper: ({ children }) => <ProductRuntimeProvider auth={auth}>{children}</ProductRuntimeProvider>,
    });

    expect(result.current.realmSlug).toBe("client-z");
  });

  it("builds api via createProductManagerApi(auth.getToken)", () => {
    const spy = vi.spyOn(productManagerApi, "createProductManagerApi");
    const auth = buildHostAuth();

    renderHook(() => useProductRuntime(), {
      wrapper: ({ children }) => <ProductRuntimeProvider auth={auth}>{children}</ProductRuntimeProvider>,
    });

    expect(spy).toHaveBeenCalledWith(auth.getToken);
    spy.mockRestore();
  });

  it("keeps the same api instance across re-renders with the same auth reference", () => {
    const auth = buildHostAuth();
    const { result, rerender } = renderHook(() => useProductRuntime(), {
      wrapper: ({ children }) => <ProductRuntimeProvider auth={auth}>{children}</ProductRuntimeProvider>,
    });

    const firstApi = result.current.api;
    rerender();

    expect(result.current.api).toBe(firstApi);
  });

  it("builds a new api instance when the auth reference changes", () => {
    let auth = buildHostAuth();
    const { result, rerender } = renderHook(() => useProductRuntime(), {
      wrapper: ({ children }) => <ProductRuntimeProvider auth={auth}>{children}</ProductRuntimeProvider>,
    });

    const firstApi = result.current.api;
    auth = buildHostAuth({ realmSlug: "client-b" });
    rerender();

    expect(result.current.api).not.toBe(firstApi);
  });

  it("throws when used outside a provider", () => {
    function Consumer() {
      useProductRuntime();
      return null;
    }
    // Suppress React's expected console.error for the thrown-during-render case.
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    expect(() => render(<Consumer />)).toThrow(
      "useProductRuntime must be used inside <ProductRuntimeProvider>"
    );
    spy.mockRestore();
  });
});
