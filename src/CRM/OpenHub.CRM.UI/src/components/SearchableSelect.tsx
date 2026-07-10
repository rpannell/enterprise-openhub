import { useEffect, useMemo, useRef, useState } from "react";
import { ChevronDown } from "lucide-react";

export interface SearchableSelectOption {
  value: string;
  label: string;
}

interface SearchableSelectProps {
  value: string;
  onChange: (value: string) => void;
  options: SearchableSelectOption[];
  placeholder?: string;
  className: string;
}

/**
 * A filter-as-you-type dropdown for option lists too long to scan as a plain <select> (e.g.
 * ~250 countries) — native <select> only jumps to options as you type the first letter, not a
 * contains-match search.
 */
export function SearchableSelect({ value, onChange, options, placeholder = "Select…", className }: SearchableSelectProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const containerRef = useRef<HTMLDivElement>(null);

  const selected = options.find((o) => o.value === value);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return options;
    return options.filter((o) => o.label.toLowerCase().includes(q));
  }, [options, query]);

  useEffect(() => {
    if (!open) return;
    function handleClickOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
        setQuery("");
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [open]);

  function select(option: SearchableSelectOption) {
    onChange(option.value);
    setOpen(false);
    setQuery("");
  }

  return (
    <div ref={containerRef} className="relative">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className={`${className} flex items-center justify-between text-left`}
      >
        <span className={selected ? "" : "text-slate-400"}>{selected ? selected.label : placeholder}</span>
        <ChevronDown size={14} className="ml-1 shrink-0 text-slate-400" />
      </button>
      {open && (
        <div className="absolute z-10 mt-1 w-full rounded-md border border-slate-200 bg-white shadow-lg dark:border-slate-700 dark:bg-slate-800">
          <input
            autoFocus
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Escape") {
                setOpen(false);
                setQuery("");
              } else if (e.key === "Enter" && filtered.length > 0) {
                select(filtered[0]);
              }
            }}
            placeholder="Search…"
            className="w-full rounded-t-md border-b border-slate-200 px-3 py-2 text-sm text-slate-800 placeholder-slate-400 outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
          />
          <ul className="max-h-56 overflow-y-auto py-1">
            {filtered.length === 0 ? (
              <li className="px-3 py-2 text-sm text-slate-400">No matches.</li>
            ) : (
              filtered.map((o) => (
                <li key={o.value}>
                  <button
                    type="button"
                    onClick={() => select(o)}
                    className={`block w-full px-3 py-1.5 text-left text-sm hover:bg-slate-100 dark:hover:bg-slate-700 ${
                      o.value === value ? "bg-accent/10 text-accent" : "text-slate-700 dark:text-slate-200"
                    }`}
                  >
                    {o.label}
                  </button>
                </li>
              ))
            )}
          </ul>
        </div>
      )}
    </div>
  );
}
