/**
 * The host's copy of the contract shared with OpenHub.Administration.UI —
 * see that project's src/federation/HostAuthContext.ts for the full
 * rationale. Deliberately duplicated, not imported across the federation
 * boundary: host and remote are separate packages/repos in a real setup, so
 * this is a versioned interface both sides commit to, not shared code. A
 * platform with several federated remotes would typically publish this as a
 * small `@openhub/mf-contracts` package instead of hand-copying it per
 * remote — out of scope for this one-remote example, but the natural next
 * step. Keep the two copies in sync by hand for now.
 */
export interface HostAuthContext {
  roles: string[];
  realmSlug: string;
  getToken: () => Promise<string>;
  /**
   * Subscribes to real-time messages pushed from the backend over the shell's single SignalR
   * connection (see OpenHub.UI/src/services/realtimeMessaging.ts). Returns an unsubscribe
   * function. Connection-only — no visible UI on the shell side; the remote decides how (or
   * whether) to display anything.
   */
  onRealtimeMessage: (listener: (envelope: {
    messageType: string;
    payloadJson: string;
    correlationId: string | null;
    sentAtUtc: string;
  }) => void) => () => void;
}
