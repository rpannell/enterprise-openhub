import { useMemo, useState, type ReactNode } from "react";
import { X, Plus, Trash2 } from "lucide-react";
import { useCrmRuntime } from "../context/CrmRuntimeContext";
import { SearchableSelect } from "../components/SearchableSelect";
import { COUNTRIES } from "../data/countries";
import { useLockBodyScroll } from "../hooks/useLockBodyScroll";
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
const COUNTRY_OPTIONS = COUNTRIES.map((c) => ({ value: c.code, label: `${c.name} (${c.code})` }));

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

const baseFieldClass =
  "w-full rounded-md border bg-white px-3 py-2 text-sm text-slate-800 placeholder-slate-400 focus:outline-none dark:bg-slate-800 dark:text-slate-100";

// One class string per state, not both stacked — conflicting Tailwind border-color utilities
// in the same class attribute resolve by generated stylesheet order, not attribute order, so
// picking exactly one avoids depending on that.
function fieldClass(hasError?: boolean): string {
  return hasError
    ? `${baseFieldClass} border-red-400 focus:border-red-500 dark:border-red-500`
    : `${baseFieldClass} border-slate-300 focus:border-accent dark:border-slate-600`;
}

function Field({
  label,
  hint,
  error,
  children,
}: {
  label: string;
  hint?: string;
  error?: string;
  children: ReactNode;
}) {
  return (
    <div>
      <label className="mb-1 block text-xs font-medium text-slate-600 dark:text-slate-400">
        {label}
      </label>
      {children}
      {error ? (
        <p className="mt-1 text-xs text-red-500">{error}</p>
      ) : (
        hint && <p className="mt-1 text-xs text-slate-400">{hint}</p>
      )}
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

interface AddressErrors {
  line1?: string;
  city?: string;
  countryCode?: string;
}

interface PhoneErrors {
  phoneNumber?: string;
}

interface ContactErrors {
  firstName?: string;
  lastName?: string;
  email?: string;
  phones: PhoneErrors[];
}

interface FormErrors {
  name?: string;
  salesPersonId?: string;
  addresses: AddressErrors[];
  contacts: ContactErrors[];
}

// Mirrors OpenHub.CRM.Api's CreateClientRequestValidator/ClientAddressRequestValidator/
// ClientContactRequestValidator/ContactPhoneRequestValidator as closely as this form's fields
// allow, so a submission that passes here doesn't come back with a 400 from the API.
function validateForm(input: {
  name: string;
  salesPersonId: string;
  addresses: ClientAddress[];
  contacts: ClientContact[];
}): FormErrors {
  const errors: FormErrors = { addresses: [], contacts: [] };

  const name = input.name.trim();
  if (!name) errors.name = "Name is required.";
  else if (name.length > 500) errors.name = "Must be 500 characters or fewer.";

  if (!input.salesPersonId) errors.salesPersonId = "Sales person is required.";

  errors.addresses = input.addresses.map((a) => {
    const e: AddressErrors = {};
    const line1 = a.line1.trim();
    const city = a.city.trim();
    const countryCode = a.countryCode.trim();

    if (!line1) e.line1 = "Address line 1 is required.";
    else if (line1.length > 500) e.line1 = "Must be 500 characters or fewer.";

    if (!city) e.city = "City is required.";
    else if (city.length > 200) e.city = "Must be 200 characters or fewer.";

    if (!countryCode) e.countryCode = "Country code is required.";
    else if (countryCode.length !== 2) e.countryCode = "Must be exactly 2 letters (e.g. US, GB, DE).";

    return e;
  });

  errors.contacts = input.contacts.map((c) => {
    const e: ContactErrors = { phones: [] };
    const firstName = c.firstName.trim();
    const lastName = c.lastName.trim();

    if (!firstName) e.firstName = "First name is required.";
    if (!lastName) e.lastName = "Last name is required.";
    if (c.email && !EMAIL_RE.test(c.email)) e.email = "Must be a valid email address.";

    e.phones = c.phones.map((p) => {
      const pe: PhoneErrors = {};
      const phoneNumber = p.phoneNumber.trim();
      if (!phoneNumber) pe.phoneNumber = "Phone number is required.";
      else if (phoneNumber.length > 30) pe.phoneNumber = "Must be 30 characters or fewer.";
      return pe;
    });

    return e;
  });

  return errors;
}

function hasFormErrors(errors: FormErrors): boolean {
  return (
    !!errors.name ||
    !!errors.salesPersonId ||
    errors.addresses.some((a) => a.line1 || a.city || a.countryCode) ||
    errors.contacts.some((c) => c.firstName || c.lastName || c.email || c.phones.some((p) => p.phoneNumber))
  );
}

export function ClientForm({ salesPeople, editing, onSaved, onCancel }: ClientFormProps) {
  useLockBodyScroll();
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

  // Only shown after a first submit attempt — a brand-new blank form shouldn't light up red
  // before the user has done anything. Once shown, it updates live as fields are edited.
  const [attempted, setAttempted] = useState(false);

  const errors = useMemo(
    () => validateForm({ name, salesPersonId, addresses, contacts }),
    [name, salesPersonId, addresses, contacts]
  );

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
    setAttempted(true);
    if (hasFormErrors(errors)) {
      setError("Please fix the highlighted fields below.");
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
            <Field label="Name" error={attempted ? errors.name : undefined}>
              <input
                autoFocus
                maxLength={500}
                value={name}
                onChange={(e) => setName(e.target.value)}
                className={fieldClass(attempted && !!errors.name)}
              />
            </Field>
            <Field label="Legal name">
              <input
                maxLength={500}
                value={legalName}
                onChange={(e) => setLegalName(e.target.value)}
                className={fieldClass()}
              />
            </Field>
            <Field label="Sales person" error={attempted ? errors.salesPersonId : undefined}>
              <select
                value={salesPersonId}
                onChange={(e) => setSalesPersonId(e.target.value)}
                className={fieldClass(attempted && !!errors.salesPersonId)}
              >
                <option value="">Select a sales person…</option>
                {salesPeople.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.displayName}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Status">
              <select
                value={status}
                onChange={(e) => setStatus(e.target.value as ClientStatus)}
                className={fieldClass()}
              >
                {STATUSES.map((s) => (
                  <option key={s} value={s}>
                    {s.replace("_", " ")}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Account tier">
              <select
                value={accountTier}
                onChange={(e) => setAccountTier(e.target.value as AccountTier)}
                className={fieldClass()}
              >
                {TIERS.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Industry">
              <input value={industry} onChange={(e) => setIndustry(e.target.value)} className={fieldClass()} />
            </Field>
            <Field label="Website">
              <input value={website} onChange={(e) => setWebsite(e.target.value)} className={fieldClass()} />
            </Field>
            <Field label="Phone">
              <input value={phone} onChange={(e) => setPhone(e.target.value)} className={fieldClass()} />
            </Field>
          </div>
          <Field label="Notes">
            <textarea value={notes} onChange={(e) => setNotes(e.target.value)} className={fieldClass()} rows={2} />
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
            {addresses.map((a, i) => {
              const addrErrors = errors.addresses[i];
              return (
                <div key={i} className="mb-2 space-y-2 rounded-md border border-slate-200 p-2 dark:border-slate-700">
                  <div className="grid grid-cols-6 gap-2">
                    <div className="col-span-3">
                      <input
                        value={a.line1}
                        onChange={(e) => updateAddress(i, { line1: e.target.value })}
                        placeholder="Address line 1"
                        maxLength={500}
                        className={fieldClass(attempted && !!addrErrors?.line1)}
                      />
                      {attempted && addrErrors?.line1 && (
                        <p className="mt-1 text-xs text-red-500">{addrErrors.line1}</p>
                      )}
                    </div>
                    <input
                      value={a.line2 ?? ""}
                      onChange={(e) => updateAddress(i, { line2: e.target.value })}
                      placeholder="Address line 2 (optional)"
                      maxLength={500}
                      className={`${fieldClass()} col-span-2`}
                    />
                    <select
                      value={a.addressType}
                      onChange={(e) => updateAddress(i, { addressType: e.target.value as ClientAddress["addressType"] })}
                      className={fieldClass()}
                    >
                      <option value="billing">Billing</option>
                      <option value="shipping">Shipping</option>
                      <option value="headquarters">HQ</option>
                      <option value="other">Other</option>
                    </select>
                  </div>
                  <div className="grid grid-cols-6 gap-2">
                    <div className="col-span-3">
                      <input
                        value={a.city}
                        onChange={(e) => updateAddress(i, { city: e.target.value })}
                        placeholder="City"
                        maxLength={200}
                        className={fieldClass(attempted && !!addrErrors?.city)}
                      />
                      {attempted && addrErrors?.city && (
                        <p className="mt-1 text-xs text-red-500">{addrErrors.city}</p>
                      )}
                    </div>
                    <input
                      value={a.stateProvince ?? ""}
                      onChange={(e) => updateAddress(i, { stateProvince: e.target.value })}
                      placeholder="State / province / region (if applicable)"
                      maxLength={200}
                      className={`${fieldClass()} col-span-2`}
                    />
                    <input
                      value={a.postalCode ?? ""}
                      onChange={(e) => updateAddress(i, { postalCode: e.target.value })}
                      placeholder="Postal code"
                      maxLength={20}
                      className={fieldClass()}
                    />
                  </div>
                  <div className="grid grid-cols-6 gap-2">
                    <div className="col-span-2">
                      <SearchableSelect
                        value={a.countryCode}
                        onChange={(v) => updateAddress(i, { countryCode: v })}
                        options={COUNTRY_OPTIONS}
                        placeholder="Country…"
                        className={fieldClass(attempted && !!addrErrors?.countryCode)}
                      />
                      {attempted && addrErrors?.countryCode && (
                        <p className="mt-1 text-xs text-red-500">{addrErrors.countryCode}</p>
                      )}
                    </div>
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
              );
            })}
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
            {contacts.map((c, i) => {
              const contactErrors = errors.contacts[i];
              return (
                <div key={i} className="mb-2 space-y-2 rounded-md border border-slate-200 p-2 dark:border-slate-700">
                  <div className="grid grid-cols-6 gap-2">
                    <div>
                      <input
                        value={c.firstName}
                        onChange={(e) => updateContact(i, { firstName: e.target.value })}
                        placeholder="First name"
                        maxLength={200}
                        className={fieldClass(attempted && !!contactErrors?.firstName)}
                      />
                      {attempted && contactErrors?.firstName && (
                        <p className="mt-1 text-xs text-red-500">{contactErrors.firstName}</p>
                      )}
                    </div>
                    <div>
                      <input
                        value={c.lastName}
                        onChange={(e) => updateContact(i, { lastName: e.target.value })}
                        placeholder="Last name"
                        maxLength={200}
                        className={fieldClass(attempted && !!contactErrors?.lastName)}
                      />
                      {attempted && contactErrors?.lastName && (
                        <p className="mt-1 text-xs text-red-500">{contactErrors.lastName}</p>
                      )}
                    </div>
                    <input
                      value={c.title ?? ""}
                      onChange={(e) => updateContact(i, { title: e.target.value })}
                      placeholder="Title"
                      className={fieldClass()}
                    />
                    <div className="col-span-2">
                      <input
                        type="email"
                        value={c.email ?? ""}
                        onChange={(e) => updateContact(i, { email: e.target.value })}
                        placeholder="Email"
                        className={fieldClass(attempted && !!contactErrors?.email)}
                      />
                      {attempted && contactErrors?.email && (
                        <p className="mt-1 text-xs text-red-500">{contactErrors.email}</p>
                      )}
                    </div>
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
                    {c.phones.map((p, pi) => {
                      const phoneError = contactErrors?.phones[pi]?.phoneNumber;
                      return (
                        <div key={pi} className="grid grid-cols-6 gap-2">
                          <select
                            value={p.phoneType}
                            onChange={(e) =>
                              updateContactPhone(i, pi, { phoneType: e.target.value as ContactPhone["phoneType"] })
                            }
                            className={fieldClass()}
                          >
                            <option value="work">Work</option>
                            <option value="cell">Cell</option>
                            <option value="home">Home</option>
                            <option value="other">Other</option>
                          </select>
                          <div className="col-span-3">
                            <input
                              value={p.phoneNumber}
                              onChange={(e) => updateContactPhone(i, pi, { phoneNumber: e.target.value })}
                              placeholder="Phone number"
                              maxLength={30}
                              className={fieldClass(attempted && !!phoneError)}
                            />
                            {attempted && phoneError && <p className="mt-1 text-xs text-red-500">{phoneError}</p>}
                          </div>
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
                      );
                    })}
                  </div>
                </div>
              );
            })}
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
