import { useEffect, useState, type ReactNode } from "react";
import { X, Copy, Check } from "lucide-react";
import { useAdminRuntime } from "../context/AdminRuntimeContext";
import type { CreatedUser, Role } from "../types";

interface CreateUserModalProps {
  realmSlug: string;
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

export function CreateUserModal({ realmSlug, onCreated, onCancel }: CreateUserModalProps) {
  const { api } = useAdminRuntime();
  const [email, setEmail] = useState("");
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [selectedRoles, setSelectedRoles] = useState<string[]>([]);
  const [roles, setRoles] = useState<Role[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  // Set once creation succeeds — switches the modal into the one-time password
  // screen below instead of closing immediately. There's no SMTP integration
  // yet to email this to the new user, so this is the only way an admin can
  // hand it over; it's never shown or retrievable again after this modal closes.
  const [createdUser, setCreatedUser] = useState<CreatedUser | null>(null);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    // The role list already excludes "tenant-admin"/"platform-admin" for a
    // tenant-admin caller — enforced server-side, not filtered here.
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
      const created = await api.createOrganizationUser(realmSlug, {
        email: email.trim(),
        firstName: firstName.trim() || undefined,
        lastName: lastName.trim() || undefined,
        initialRoles: selectedRoles.length > 0 ? selectedRoles : undefined,
      });
      setCreatedUser(created);
    } catch (err: any) {
      setError(err?.body?.detail ?? "Could not create user.");
      setSaving(false);
    }
  }

  async function handleCopyPassword() {
    if (!createdUser?.temporaryPassword) return;
    await navigator.clipboard.writeText(createdUser.temporaryPassword);
    setCopied(true);
  }

  if (createdUser) {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
        <div className="w-full max-w-md rounded-lg border border-slate-200 bg-white p-6 shadow-lg dark:border-slate-700 dark:bg-slate-900">
          <h2 className="mb-1 text-sm font-semibold text-slate-800 dark:text-slate-100">
            User created
          </h2>
          <p className="mb-4 text-xs text-slate-500 dark:text-slate-400">
            {createdUser.email} won&rsquo;t get an email — there&rsquo;s no email delivery set up
            yet. Copy this temporary password and share it with them yourself.{" "}
            <strong>It will not be shown again.</strong>
          </p>

          <div className="mb-4 flex items-center justify-between gap-2 rounded-md border border-slate-300 bg-slate-50 px-3 py-2 dark:border-slate-600 dark:bg-slate-800">
            <code className="select-all font-mono text-sm text-slate-800 dark:text-slate-100">
              {createdUser.temporaryPassword}
            </code>
            <button
              type="button"
              onClick={handleCopyPassword}
              aria-label="Copy temporary password"
              className="shrink-0 rounded-md p-1.5 text-slate-500 hover:bg-slate-200 dark:text-slate-400 dark:hover:bg-slate-700"
            >
              {copied ? <Check size={16} /> : <Copy size={16} />}
            </button>
          </div>

          <div className="flex justify-end">
            <button
              type="button"
              onClick={onCreated}
              className="rounded-md bg-accent px-4 py-2 text-sm font-medium text-white hover:bg-accent/90"
            >
              Done
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="w-full max-w-md rounded-lg border border-slate-200 bg-white p-6 shadow-lg dark:border-slate-700 dark:bg-slate-900">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-sm font-semibold text-slate-800 dark:text-slate-100">New User</h2>
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
          <Field label="Roles" hint="Optional, any number">
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
              {saving ? "Creating…" : "Create"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
