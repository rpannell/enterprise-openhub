import { useState, type ReactNode } from "react";
import { X, Plus, Trash2 } from "lucide-react";
import { useCrmRuntime } from "../context/CrmRuntimeContext";
import type {
  AccountTier,
  Client,
  ClientAddress,
  ClientContact,
  ClientStatus,
  ContactPhone,
  SalesPersonPicklistItem,
} from "../types";

interface ClientFormProps {
  salesPeople: SalesPersonPicklistItem[];
  editing?: Client | null;
  onSaved: () => void;
  onCancel: () => void;
}

const STATUSES: ClientStatus[] = ["prospect", "active", "on_hold", "churned"];
const TIERS: AccountTier[] = ["strategic", "key", "standard", "small"];

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

const emptyAddress: ClientAddress = {
  addressType: "billing",
  line1: "",
  city: "",
  countryCode: "",
  isPrimary: false,
};

const emptyContact: ClientContact = {
  firstName: "",
  lastName: "",
  isPrimary: false,
  phones: [],
};

const emptyPhone: ContactPhone = {
  phoneType: "work",
  phoneNumber: "",
  isPrimary: false,
};

export function ClientForm({ salesPeople, editing, onSaved, onCancel }: ClientFormProps) {
  const { api } = useCrmRuntime();
  const [name, setName] = useState(editing?.name ?? "");
  const [legalName, setLegalName] = useState(editing?.legalName ?? "");
  const [salesPersonId, setSalesPersonId] = useState(editing?.salesPersonId ?? "");
  const [status, setStatus] = useState<ClientStatus>(editing?.status ?? "prospect");
  const [accountTier, setAccountTier] = useState<AccountTier>(editing?.accountTier ?? "standard");
  const [industry, setIndustry] = useState(editing?.industry ?? "");
  const [website, setWebsite] = useState(editing?.website ?? "");
  const [phone, setPhone] = useState(editing?.phone ?? "");
  const [notes, setNotes] = useState(editing?.notes ?? "");
  const [addresses, setAddresses] = useState<ClientAddress[]>(editing?.addresses ?? []);
  const [contacts, setContacts] = useState<ClientContact[]>(editing?.contacts ?? []);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  function updateAddress(index: number, patch: Partial<ClientAddress>) {
    setAddresses((prev) => prev.map((a, i) => (i === index ? { ...a, ...patch } : a)));
  }

  function updateContact(index: number, patch: Partial<ClientContact>) {
    setContacts((prev) => prev.map((c, i) => (i === index ? { ...c, ...patch } : c)));
  }

  function addContactPhone(contactIndex: number) {
    setContacts((prev) =>
      prev.map((c, i) => (i === contactIndex ? { ...c, phones: [...c.phones, { ...emptyPhone }] } : c))
    );
  }

  function updateContactPhone(contactIndex: number, phoneIndex: number, patch: Partial<ContactPhone>) {
    setContacts((prev) =>
      prev.map((c, i) =>
        i === contactIndex
          ? { ...c, phones: c.phones.map((p, pi) => (pi === phoneIndex ? { ...p, ...patch } : p)) }
          : c
      )
    );
  }

  function removeContactPhone(contactIndex: number, phoneIndex: number) {
    setContacts((prev) =>
      prev.map((c, i) => (i === contactIndex ? { ...c, phones: c.phones.filter((_, pi) => pi !== phoneIndex) } : c))
    );
  }

  async function handleSubmit() {
    if (!name.trim() || !salesPersonId) {
      setError("Name and sales person are required.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const payload = {
        name: name.trim(),
        legalName: legalName.trim() || null,
        salesPersonId,
        status,
        accountTier,
        industry: industry.trim() || null,
        website: website.trim() || null,
        phone: phone.trim() || null,
        notes: notes.trim() || null,
        addresses,
        contacts,
      };
      if (editing) {
        await api.updateClient(editing.id, payload);
      } else {
        await api.createClient(payload);
      }
      onSaved();
    } catch (err: any) {
      setError(err?.body?.detail ?? "Could not save client.");
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-black/40 py-8">
      <div className="w-full max-w-2xl rounded-lg border border-slate-200 bg-white p-6 shadow-lg dark:border-slate-700 dark:bg-slate-900">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-sm font-semibold text-slate-800 dark:text-slate-100">
            {editing ? "Edit Client" : "New Client"}
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
          <div className="grid grid-cols-2 gap-3">
            <Field label="Name">
              <input autoFocus value={name} onChange={(e) => setName(e.target.value)} className={inputClass} />
            </Field>
            <Field label="Legal name">
              <input value={legalName} onChange={(e) => setLegalName(e.target.value)} className={inputClass} />
            </Field>
            <Field label="Sales person">
              <select value={salesPersonId} onChange={(e) => setSalesPersonId(e.target.value)} className={inputClass}>
                <option value="">Select a sales person…</option>
                {salesPeople.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.displayName}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Status">
              <select value={status} onChange={(e) => setStatus(e.target.value as ClientStatus)} className={inputClass}>
                {STATUSES.map((s) => (
                  <option key={s} value={s}>
                    {s.replace("_", " ")}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Account tier">
              <select value={accountTier} onChange={(e) => setAccountTier(e.target.value as AccountTier)} className={inputClass}>
                {TIERS.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Industry">
              <input value={industry} onChange={(e) => setIndustry(e.target.value)} className={inputClass} />
            </Field>
            <Field label="Website">
              <input value={website} onChange={(e) => setWebsite(e.target.value)} className={inputClass} />
            </Field>
            <Field label="Phone">
              <input value={phone} onChange={(e) => setPhone(e.target.value)} className={inputClass} />
            </Field>
          </div>
          <Field label="Notes">
            <textarea value={notes} onChange={(e) => setNotes(e.target.value)} className={inputClass} rows={2} />
          </Field>

          <div>
            <div className="mb-2 flex items-center justify-between">
              <h3 className="text-xs font-semibold text-slate-600 dark:text-slate-400">Addresses</h3>
              <button
                type="button"
                onClick={() => setAddresses((prev) => [...prev, { ...emptyAddress }])}
                className="inline-flex items-center gap-1 text-xs text-accent hover:underline"
              >
                <Plus size={12} /> Add address
              </button>
            </div>
            {addresses.map((a, i) => (
              <div key={i} className="mb-2 space-y-2 rounded-md border border-slate-200 p-2 dark:border-slate-700">
                <div className="grid grid-cols-6 gap-2">
                  <input
                    value={a.line1}
                    onChange={(e) => updateAddress(i, { line1: e.target.value })}
                    placeholder="Address line 1"
                    className={`${inputClass} col-span-3`}
                  />
                  <input
                    value={a.line2 ?? ""}
                    onChange={(e) => updateAddress(i, { line2: e.target.value })}
                    placeholder="Address line 2 (optional)"
                    className={`${inputClass} col-span-2`}
                  />
                  <select
                    value={a.addressType}
                    onChange={(e) => updateAddress(i, { addressType: e.target.value as ClientAddress["addressType"] })}
                    className={inputClass}
                  >
                    <option value="billing">Billing</option>
                    <option value="shipping">Shipping</option>
                    <option value="headquarters">HQ</option>
                    <option value="other">Other</option>
                  </select>
                </div>
                <div className="grid grid-cols-6 gap-2">
                  <input
                    value={a.city}
                    onChange={(e) => updateAddress(i, { city: e.target.value })}
                    placeholder="City"
                    className={`${inputClass} col-span-2`}
                  />
                  <input
                    value={a.stateProvince ?? ""}
                    onChange={(e) => updateAddress(i, { stateProvince: e.target.value })}
                    placeholder="State / province / region (if applicable)"
                    className={`${inputClass} col-span-2`}
                  />
                  <input
                    value={a.postalCode ?? ""}
                    onChange={(e) => updateAddress(i, { postalCode: e.target.value })}
                    placeholder="Postal code"
                    className={inputClass}
                  />
                  <input
                    value={a.countryCode}
                    onChange={(e) => updateAddress(i, { countryCode: e.target.value.toUpperCase() })}
                    placeholder="US"
                    maxLength={2}
                    title="ISO 3166-1 alpha-2 country code, e.g. US, GB, DE, JP"
                    className={inputClass}
                  />
                </div>
                <div className="flex items-center justify-between">
                  <label className="flex items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400">
                    <input
                      type="checkbox"
                      checked={a.isPrimary}
                      onChange={(e) => updateAddress(i, { isPrimary: e.target.checked })}
                    />
                    Primary address of this type
                  </label>
                  <button
                    type="button"
                    onClick={() => setAddresses((prev) => prev.filter((_, idx) => idx !== i))}
                    className="flex items-center gap-1 text-xs text-slate-400 hover:text-red-600"
                  >
                    <Trash2 size={13} /> Remove
                  </button>
                </div>
              </div>
            ))}
          </div>

          <div>
            <div className="mb-2 flex items-center justify-between">
              <h3 className="text-xs font-semibold text-slate-600 dark:text-slate-400">Contacts</h3>
              <button
                type="button"
                onClick={() => setContacts((prev) => [...prev, { ...emptyContact }])}
                className="inline-flex items-center gap-1 text-xs text-accent hover:underline"
              >
                <Plus size={12} /> Add contact
              </button>
            </div>
            {contacts.map((c, i) => (
              <div key={i} className="mb-2 space-y-2 rounded-md border border-slate-200 p-2 dark:border-slate-700">
                <div className="grid grid-cols-6 gap-2">
                  <input
                    value={c.firstName}
                    onChange={(e) => updateContact(i, { firstName: e.target.value })}
                    placeholder="First name"
                    className={inputClass}
                  />
                  <input
                    value={c.lastName}
                    onChange={(e) => updateContact(i, { lastName: e.target.value })}
                    placeholder="Last name"
                    className={inputClass}
                  />
                  <input
                    value={c.title ?? ""}
                    onChange={(e) => updateContact(i, { title: e.target.value })}
                    placeholder="Title"
                    className={inputClass}
                  />
                  <input
                    value={c.email ?? ""}
                    onChange={(e) => updateContact(i, { email: e.target.value })}
                    placeholder="Email"
                    className={`${inputClass} col-span-2`}
                  />
                  <button
                    type="button"
                    onClick={() => setContacts((prev) => prev.filter((_, idx) => idx !== i))}
                    className="flex items-center justify-center text-slate-400 hover:text-red-600"
                    aria-label="Remove contact"
                  >
                    <Trash2 size={15} />
                  </button>
                </div>

                <div className="space-y-1.5 border-t border-slate-100 pt-2 dark:border-slate-800">
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-medium text-slate-500 dark:text-slate-400">Phones</span>
                    <button
                      type="button"
                      onClick={() => addContactPhone(i)}
                      className="inline-flex items-center gap-1 text-xs text-accent hover:underline"
                    >
                      <Plus size={11} /> Add phone
                    </button>
                  </div>
                  {c.phones.map((p, pi) => (
                    <div key={pi} className="grid grid-cols-6 gap-2">
                      <select
                        value={p.phoneType}
                        onChange={(e) =>
                          updateContactPhone(i, pi, { phoneType: e.target.value as ContactPhone["phoneType"] })
                        }
                        className={inputClass}
                      >
                        <option value="work">Work</option>
                        <option value="cell">Cell</option>
                        <option value="home">Home</option>
                        <option value="other">Other</option>
                      </select>
                      <input
                        value={p.phoneNumber}
                        onChange={(e) => updateContactPhone(i, pi, { phoneNumber: e.target.value })}
                        placeholder="Phone number"
                        className={`${inputClass} col-span-3`}
                      />
                      <label className="flex items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400">
                        <input
                          type="checkbox"
                          checked={p.isPrimary}
                          onChange={(e) => updateContactPhone(i, pi, { isPrimary: e.target.checked })}
                        />
                        Primary
                      </label>
                      <button
                        type="button"
                        onClick={() => removeContactPhone(i, pi)}
                        className="flex items-center justify-center text-slate-400 hover:text-red-600"
                        aria-label="Remove phone"
                      >
                        <Trash2 size={13} />
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>

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
