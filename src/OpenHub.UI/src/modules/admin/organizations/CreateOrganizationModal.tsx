import { useState, type ReactNode } from "react";
import { X } from "lucide-react";
import { administrationApi } from "../../../services/administrationApi";

interface CreateOrganizationModalProps {
  onCreated: () => void;
  onCancel: () => void;
}

const inputClass =
  "w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-800 placeholder-slate-400 focus:border-accent focus:outline-none dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100";

function Field({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
  return (
    <div>
      <label className="mb-1 block text-xs font-medium text-slate-600 dark:text-slate-400">
        {label}
      </label>
      {children}
      {hint && <p className="mt-1 text-xs text-slate-400">{hint}</p>}
    </div>
  );
}

export function CreateOrganizationModal({ onCreated, onCancel }: CreateOrganizationModalProps) {
  const [realmSlug, setRealmSlug] = useState("");
  const [name, setName] = useState("");
  const [alias, setAlias] = useState("");
  const [domains, setDomains] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function handleSubmit() {
    if (!realmSlug.trim() || !name.trim() || !alias.trim()) {
      setError("Realm, name, and alias are required.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await administrationApi.createOrganization({
        realmSlug: realmSlug.trim(),
        name: name.trim(),
        alias: alias.trim(),
        domains: domains
          .split(",")
          .map((d) => d.trim())
          .filter(Boolean),
      });
      onCreated();
    } catch (err: any) {
      setError(
        err?.body?.detail ?? "Could not create organization. Confirm the realm has already been provisioned."
      );
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="w-full max-w-md rounded-lg border border-slate-200 bg-white p-6 shadow-lg dark:border-slate-700 dark:bg-slate-900">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-sm font-semibold text-slate-800 dark:text-slate-100">
            New Organization
          </h2>
          <button
            type="button"
            onClick={onCancel}
            className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-300"
            aria-label="Close"
          >
            <X size={18} />
          </button>
        </div>

        <div className="space-y-3">
          <Field label="Realm slug" hint="Must already be provisioned in Keycloak — realm creation is a manual ops step.">
            <input
              autoFocus
              value={realmSlug}
              onChange={(e) => setRealmSlug(e.target.value)}
              placeholder="client-c"
              className={inputClass}
            />
          </Field>
          <Field label="Name">
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Client C Inc."
              className={inputClass}
            />
          </Field>
          <Field label="Alias">
            <input
              value={alias}
              onChange={(e) => setAlias(e.target.value)}
              placeholder="client-c"
              className={inputClass}
            />
          </Field>
          <Field label="Domains" hint="Comma-separated, optional">
            <input
              value={domains}
              onChange={(e) => setDomains(e.target.value)}
              placeholder="client-c.com"
              className={inputClass}
            />
          </Field>

          {error && <p className="text-xs text-red-500">{error}</p>}

          <div className="flex justify-end gap-2 pt-2">
            <button
              type="button"
              onClick={onCancel}
              className="rounded-md border border-slate-200 px-4 py-2 text-sm text-slate-600 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={handleSubmit}
              disabled={saving}
              className="rounded-md bg-accent px-4 py-2 text-sm font-medium text-white hover:bg-accent/90 disabled:opacity-50"
            >
              {saving ? "Creating…" : "Create"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
