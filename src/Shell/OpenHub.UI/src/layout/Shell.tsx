import { useState } from "react";
import { Link, Outlet, useLocation } from "react-router-dom";
import { Menu, X, Sun, Moon } from "lucide-react";
import { useAuth } from "../auth/AuthProvider";
import { useTheme } from "../theme/ThemeProvider";
import { modules } from "../config/modules";
import logoLight from "../assets/logo-full-light.svg";
import logoDark from "../assets/logo-full-dark.svg";

export default function Shell() {
  const { username, roles, logout, hasAnyRole } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const location = useLocation();
  const [navOpen, setNavOpen] = useState(false);
  const visibleModules = modules.filter((m) => hasAnyRole(m.roles));

  const navLinkClass = (active: boolean) =>
    `flex items-center gap-2.5 rounded-md px-3 py-2 text-sm ${
      active
        ? "bg-accent/10 text-accent font-medium"
        : "text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800"
    }`;

  const SidebarContent = (
    <>
      <div className="flex items-center justify-between px-5 py-5">
        <img
          src={theme === "dark" ? logoDark : logoLight}
          alt="OpenHub"
          className="h-7"
        />
        <button
          onClick={() => setNavOpen(false)}
          className="text-slate-400 hover:text-slate-700 lg:hidden dark:hover:text-slate-100"
          aria-label="Close navigation"
        >
          <X size={20} />
        </button>
      </div>
      <nav className="flex flex-col gap-1 px-3">
        <Link
          to="/"
          onClick={() => setNavOpen(false)}
          className={navLinkClass(location.pathname === "/")}
        >
          {/* Dashboard has no module icon of its own — keep it text-only, it's the "home" link */}
          Dashboard
        </Link>
        {visibleModules.map((m) => (
          <Link
            key={m.id}
            to={m.path}
            onClick={() => setNavOpen(false)}
            className={navLinkClass(location.pathname.startsWith(m.path))}
          >
            <m.Icon size={17} />
            {m.name}
          </Link>
        ))}
      </nav>
    </>
  );

  return (
    <div className="flex h-screen bg-slate-50 dark:bg-slate-950">
      {/* Desktop sidebar */}
      <aside className="hidden w-60 shrink-0 border-r border-slate-200 bg-white lg:flex lg:flex-col dark:border-slate-800 dark:bg-slate-900">
        {SidebarContent}
      </aside>

      {/* Mobile drawer */}
      {navOpen && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <div
            className="absolute inset-0 bg-black/40"
            onClick={() => setNavOpen(false)}
          />
          <aside className="absolute inset-y-0 left-0 w-60 bg-white dark:bg-slate-900">
            {SidebarContent}
          </aside>
        </div>
      )}

      <div className="flex flex-1 flex-col">
        <header className="flex h-14 items-center justify-between border-b border-slate-200 bg-white px-4 sm:px-6 dark:border-slate-800 dark:bg-slate-900">
          <div className="flex items-center gap-3">
            <button
              onClick={() => setNavOpen(true)}
              className="text-slate-500 hover:text-slate-800 lg:hidden dark:text-slate-400 dark:hover:text-slate-100"
              aria-label="Open navigation"
            >
              <Menu size={20} />
            </button>
            <span className="hidden text-sm text-slate-500 sm:inline dark:text-slate-400">
              {roles.length} role{roles.length === 1 ? "" : "s"} assigned
            </span>
          </div>

          <div className="flex items-center gap-2 sm:gap-4">
            <button
              onClick={toggleTheme}
              className="rounded-md p-2 text-slate-500 hover:bg-slate-100 hover:text-slate-800 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-slate-100"
              aria-label="Toggle dark mode"
              title={theme === "dark" ? "Switch to light mode" : "Switch to dark mode"}
            >
              {theme === "dark" ? <Sun size={18} /> : <Moon size={18} />}
            </button>
            <Link
              to="/account"
              className="hidden text-sm text-slate-500 hover:text-slate-800 sm:inline dark:text-slate-400 dark:hover:text-slate-100"
            >
              {username}
            </Link>
            <button
              onClick={logout}
              className="text-sm text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-slate-100"
            >
              Sign out
            </button>
          </div>
        </header>
        <main className="flex-1 overflow-auto bg-slate-50 p-4 sm:p-6 dark:bg-slate-950">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
