import { useEffect, useState, type ReactNode } from "react";
import { X } from "lucide-react";
import { useCrmRuntime } from "../context/CrmRuntimeContext";
import { useLockBodyScroll } from "../hooks/useLockBodyScroll";
import type { AvailableKeycloakUser, SalesPerson } from "../types";

interface SalesPersonFormProps {
  editing?: SalesPerson | null;
  onSaved: () => void;
  onCancel: () => void;
}

const inputClass =
  "w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-800 placeholder-slate-400 focus:border-accent focus:outline-none dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100";

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <label className="mb-1 block text-xs font-medium text-slate-600 dark:text-slate-400">
        {label}
      </label>
      {children}
    </div>
  );
}

function keycloakUserName(u: AvailableKeycloakUser): string {
  const name = `${u.firstName ?? ""} ${u.lastName ?? ""}`.trim();
  return name || u.username;
}

export function SalesPersonForm({ editing, onSaved, onCancel }: SalesPersonFormProps) {
  useLockBodyScroll();
  const { api } = useCrmRuntime();

  // Create mode only: sales people are always created by linking an existing Keycloak
  // identity (they need to log in), not by typing name/email freehand.
  const [availableUsers, setAvailableUsers] = useState<AvailableKeycloakUser[]>([]);
  const [usersLoading, setUsersLoading] = useState(!editing);
  const [uniqueId, setUniqueId] = useState("");

  const [firstName, setFirstName] = useState(editing?.firstName ?? "");
  const [lastName, setLastName] = useState(editing?.lastName ?? "");
  const [email, setEmail] = useState(editing?.email ?? "");
  const [phone, setPhone] = useState(editing?.phone ?? "");
  const [jobTitle, setJobTitle] = useState(editing?.jobTitle ?? "");
  const [employeeNumber, setEmployeeNumber] = useState(editing?.employeeNumber ?? "");
  const [startDate, setStartDate] = useState(editing?.startDate ?? "");
  const [endDate, setEndDate] = useState(editing?.endDate ?? "");
  const [activeStatus, setActiveStatus] = useState(editing?.activeStatus ?? true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (editing) return;
    setUsersLoading(true);
    api
      .getAvailableKeycloakUsers()
      .then(setAvailableUsers)
      .catch(() => setError("Failed to load Keycloak users."))
      .finally(() => setUsersLoading(false));
  }, [api, editing]);

  function selectUser(id: string) {
    setUniqueId(id);
    const user = availableUsers.find((u) => u.uniqueId === id);
    setFirstName(user?.firstName ?? "");
    setLastName(user?.lastName ?? "");
    setEmail(user?.email ?? "");
  }

  async function handleSubmit() {
    if (!editing && !uniqueId) {
      setError("Select a Keycloak user.");
      return;
    }
    if (!firstName.trim() || !lastName.trim() || !email.trim()) {
      setError("First name, last name, and email are required.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      if (editing) {
        await api.updateSalesPerson(editing.id, {
          firstName: firstName.trim(),
          lastName: lastName.trim(),
          phone: phone.trim() || null,
          jobTitle: jobTitle.trim() || null,
          employeeNumber: employeeNumber.trim() || null,
          activeStatus,
          startDate: startDate || null,
          endDate: endDate || null,
        });
      } else {
        await api.createSalesPerson({
          uniqueId,
          firstName: firstName.trim(),
          lastName: lastName.trim(),
          email: email.trim(),
          phone: phone.trim() || null,
          jobTitle: jobTitle.trim() || null,
          employeeNumber: employeeNumber.trim() || null,
          startDate: startDate || null,
        });
      }
      onSaved();
    } catch (err: any) {
      setError(err?.body?.detail ?? "Could not save sales person.");
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-black/40 py-8">
      <div className="w-full max-w-lg rounded-lg border border-slate-200 bg-white p-6 shadow-lg dark:border-slate-700 dark:bg-slate-900">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-sm font-semibold text-slate-800 dark:text-slate-100">
            {editing ? "Edit Sales Person" : "New Sales Person"}
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

        <div className="space-y-4">
          {!editing && (
            <Field label="Keycloak user">
              <select
                autoFocus
                value={uniqueId}
                onChange={(e) => selectUser(e.target.value)}
                disabled={usersLoading || availableUsers.length === 0}
                className={inputClass}
              >
                <option value="">
                  {usersLoading
                    ? "Loading…"
                    : availableUsers.length === 0
                      ? "No unlinked Keycloak users available"
                      : "Select a Keycloak user…"}
                </option>
                {availableUsers.map((u) => (
                  <option key={u.uniqueId} value={u.uniqueId}>
                    {keycloakUserName(u)}
                    {u.email ? ` — ${u.email}` : ""}
                  </option>
                ))}
              </select>
              <p className="mt-1 text-xs text-slate-400">
                Only users who already have a Keycloak account and aren't yet a sales person are
                listed — they need to log in.
              </p>
            </Field>
          )}

          <div className="grid grid-cols-2 gap-3">
            <Field label="First name">
              <input
                autoFocus={!!editing}
                readOnly={!editing}
                value={firstName}
                onChange={(e) => setFirstName(e.target.value)}
                className={`${inputClass} ${!editing ? "bg-slate-50 dark:bg-slate-800/50" : ""}`}
              />
            </Field>
            <Field label="Last name">
              <input
                readOnly={!editing}
                value={lastName}
                onChange={(e) => setLastName(e.target.value)}
                className={`${inputClass} ${!editing ? "bg-slate-50 dark:bg-slate-800/50" : ""}`}
              />
            </Field>
            <Field label="Email">
              <input
                type="email"
                readOnly
                value={email}
                className={`${inputClass} bg-slate-50 dark:bg-slate-800/50`}
                title="Owned by the linked Keycloak account — not editable here."
              />
            </Field>
            <Field label="Phone">
              <input value={phone ?? ""} onChange={(e) => setPhone(e.target.value)} className={inputClass} />
            </Field>
            <Field label="Job title">
              <input value={jobTitle ?? ""} onChange={(e) => setJobTitle(e.target.value)} className={inputClass} />
            </Field>
            <Field label="Employee number">
              <input
                value={employeeNumber ?? ""}
                onChange={(e) => setEmployeeNumber(e.target.value)}
                className={inputClass}
              />
            </Field>
            <Field label="Start date">
              <input
                type="date"
                value={startDate ?? ""}
                onChange={(e) => setStartDate(e.target.value)}
                className={inputClass}
              />
            </Field>
            {editing && (
              <Field label="End date">
                <input
                  type="date"
                  value={endDate ?? ""}
                  onChange={(e) => setEndDate(e.target.value)}
                  className={inputClass}
                />
              </Field>
            )}
          </div>

          {editing && (
            <label className="flex items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400">
              <input
                type="checkbox"
                checked={activeStatus}
                onChange={(e) => setActiveStatus(e.target.checked)}
              />
              Active
            </label>
          )}

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
              {saving ? "Saving…" : editing ? "Save" : "Create"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
