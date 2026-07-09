/** @type {import('tailwindcss').Config} */
// Mirrors src/Shell/OpenHub.UI/tailwind.config.js — same brand tokens, so the
// federated Admin module doesn't look like a different app once it's mounted
// inside the shell. This is duplication, not a shared theme package; see
// docs/module-federation.md for why that's a simplification, not the
// end-state for a real multi-remote setup.
export default {
  darkMode: "class",
  content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
  theme: {
    extend: {
      colors: {
        ink: "#14171F",
        accent: "#4A47C7",
        "accent-dark": "#3A37A0",
        highlight: "#F2A93B",
      },
      fontFamily: {
        display: ["Space Grotesk", "Inter", "system-ui", "sans-serif"],
        sans: ["Inter", "system-ui", "sans-serif"],
      },
    },
  },
  plugins: [],
};
