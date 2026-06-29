export default function AdminApp() {
  return (
    <div>
      <h1 className="mb-1 text-xl font-semibold text-slate-800 dark:text-slate-100">Administration</h1>
      <p className="mb-6 text-sm text-slate-500 dark:text-slate-400">Users, roles, and system settings.</p>
      <div className="rounded-lg border border-slate-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900 text-sm text-slate-500 dark:text-slate-400">
        This is where the Administration module mounts. Replace this with real
        routes/components — or, later, swap it for a remotely loaded
        micro-frontend without touching the shell.
      </div>
    </div>
  );
}
