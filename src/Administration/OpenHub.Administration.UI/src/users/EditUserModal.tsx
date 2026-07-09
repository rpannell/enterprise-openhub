import { useEffect, useState, type ReactNode } from "react";
import { X } from "lucide-react";
import { useAdminRuntime } from "../context/AdminRuntimeContext";
import type { OrgUser, Role } from "../types";

interface EditUserModalProps {
  realmSlug: string;
  user: OrgUser;
  onSaved: () => void;
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

export function EditUserModal({ realmSlug, user, onSaved, onCancel }: EditUserModalProps) {
  const { api } = useAdminRuntime();
  const [email, setEmail] = useState(user.email ?? "");
  const [firstName, setFirstName] = useState(user.firstName ?? "");
  const [lastName, setLastName] = useState(user.lastName ?? "");
  const [enabled, setEnabled] = useState(user.enabled);
  const [selectedRoles, setSelectedRoles] = useState<string[]>(user.roles);
  const [roles, setRoles] = useState<Role[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    // Same "excludes tenant-admin/platform-admin for a tenant-admin caller"
    // guarantee as CreateUserModal — enforced server-side.
    api
      .getAssignableRoles(realmSlug)
      .then(setRoles)
      .catch(() => setRoles([]));
  }, [api, realmSlug]);

  function toggleRole(roleName: string) {
    setSelectedRoles((current) =>
      current.includes(roleName) ? current.filter((r) => r !== roleName) : [...current, roleName]
    );
  }

  async function handleSubmit() {
    if (!email.trim()) {
      setError("Email is required.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await api.updateOrganizationUser(realmSlug, user.id, {
        email: email.trim(),
        firstName: firstName.trim() || undefined,
        lastName: lastName.trim() || undefined,
        enabled,
      });

      const added = selectedRoles.filter((r) => !user.roles.includes(r));
      const removed = user.roles.filter((r) => !selectedRoles.includes(r));
      await Promise.all([
        ...added.map((roleName) => api.assignRole(realmSlug, user.id, roleName)),
        ...removed.map((roleName) => api.removeRole(realmSlug, user.id, roleName)),
      ]);

      onSaved();
    } catch (err: any) {
      setError(err?.body?.detail ?? "Could not update user.");
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="w-full max-w-md rounded-lg border border-slate-200 bg-white p-6 shadow-lg dark:border-slate-700 dark:bg-slate-900">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-sm font-semibold text-slate-800 dark:text-slate-100">Edit User</h2>
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
          <Field label="Email" hint="Also used as the username — there's no separate username.">
            <input
              autoFocus
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className={inputClass}
            />
          </Field>
          <Field label="First name">
            <input value={firstName} onChange={(e) => setFirstName(e.target.value)} className={inputClass} />
          </Field>
          <Field label="Last name">
            <input value={lastName} onChange={(e) => setLastName(e.target.value)} className={inputClass} />
          </Field>
          <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-200">
            <input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} />
            Enabled
          </label>
          <Field label="Roles" hint="Any number">
            <div className="space-y-1.5 rounded-md border border-slate-300 p-2 dark:border-slate-600">
              {roles.length === 0 && (
                <p className="text-xs text-slate-400">No assignable roles.</p>
              )}
              {roles.map((r) => (
                <label
                  key={r.name}
                  className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-200"
                >
                  <input
                    type="checkbox"
                    checked={selectedRoles.includes(r.name)}
                    onChange={() => toggleRole(r.name)}
                  />
                  {r.name}
                </label>
              ))}
            </div>
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
              {saving ? "Saving…" : "Save"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
