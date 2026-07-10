import { useLayoutEffect } from "react";

/**
 * Prevents the page behind a modal from scrolling while it's open. Without this, a modal
 * that's taller than the viewport (routine on mobile) shows two independent scrollable
 * surfaces at once — the modal's own overlay and the page behind it — which is what "double
 * scrollbar" looks like. Restores the previous overflow value on unmount.
 */
export function useLockBodyScroll(): void {
  useLayoutEffect(() => {
    const original = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = original;
    };
  }, []);
}
