namespace Microsoft.Extensions.Hosting;

internal static class OpenHubScalarTheme
{
    // Injected into Scalar via WithCustomCss.
    // Overrides Scalar's CSS custom properties to match the OpenHub brand:
    //   accent  #4A47C7  (indigo)
    //   ink     #14171F  (near-black text)
    //   highlight #F2A93B (amber)
    //   fonts   Space Grotesk (headings) / Inter (body)
    internal const string Css = """
        @import url('https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;600;700&family=Inter:wght@400;500;600&display=swap');

        .light-mode, .dark-mode {
            --scalar-font: 'Inter', system-ui, sans-serif;
            --scalar-font-code: 'Fira Code', 'Cascadia Code', 'Courier New', monospace;
            --scalar-color-accent: #4A47C7;
            --scalar-radius: 8px;
            --scalar-radius-lg: 12px;
        }

        /* ── Dark mode (default) ──────────────────────────────────── */
        .dark-mode {
            --scalar-background-1: #0f1117;
            --scalar-background-2: #161b27;
            --scalar-background-3: #1e2535;
            --scalar-color-1: #e2e8f0;
            --scalar-color-2: #94a3b8;
            --scalar-color-3: #64748b;
            --scalar-color-accent: #4A47C7;
            --scalar-border-color: rgba(255, 255, 255, 0.08);
            --scalar-scrollbar-color: rgba(255, 255, 255, 0.12);
            --scalar-scrollbar-color-active: rgba(255, 255, 255, 0.24);
            --scalar-color-green: #34d399;
            --scalar-color-blue: #60a5fa;
            --scalar-color-orange: #F2A93B;
            --scalar-color-red: #f87171;
            --scalar-color-purple: #a78bfa;
        }

        /* ── Light mode ───────────────────────────────────────────── */
        .light-mode {
            --scalar-background-1: #f8f9fc;
            --scalar-background-2: #f1f1f9;
            --scalar-background-3: #e8e8f5;
            --scalar-color-1: #14171F;
            --scalar-color-2: #475569;
            --scalar-color-3: #94a3b8;
            --scalar-color-accent: #4A47C7;
            --scalar-border-color: rgba(74, 71, 199, 0.12);
            --scalar-color-green: #059669;
            --scalar-color-blue: #2563eb;
            --scalar-color-orange: #d97706;
            --scalar-color-red: #dc2626;
        }

        /* ── Brand typography ──────────────────────────────────────── */
        .sidebar-heading,
        .section-heading,
        h1, h2, h3 {
            font-family: 'Space Grotesk', sans-serif !important;
            font-weight: 600 !important;
        }
        """;
}
