import * as signalR from "@microsoft/signalr";

/** Mirrors OpenHub.RealtimeMessaging.Contracts.MessageEnvelope's JSON shape. */
export interface RealtimeMessageEnvelope {
  messageType: string;
  payloadJson: string;
  correlationId: string | null;
  sentAtUtc: string;
}

export type RealtimeMessageListener = (envelope: RealtimeMessageEnvelope) => void;

/**
 * Connection-only plumbing — no visible UI. Builds a SignalR connection to
 * OpenHub.RealtimeMessaging.Api's hub and exposes start/stop/subscribe. `accessTokenFactory` is
 * SignalR's own supported hook for the browser-can't-set-Authorization-header-on-WebSocket
 * problem: it appends `?access_token=` for the WebSocket transport and sends a real
 * Authorization header for the HTTP-based negotiate/fallback transports, so no manual
 * query-string handling is needed here — the matching server-side piece lives in
 * OpenHub.RealtimeMessaging.Api's Program.cs (JwtBearerOptions.Events.OnMessageReceived).
 */
export function createNotificationConnection(
  hubUrl: string,
  getToken: () => Promise<string>
) {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, { accessTokenFactory: getToken })
    .withAutomaticReconnect()
    .build();

  const listeners = new Set<RealtimeMessageListener>();
  connection.on("ReceiveMessage", (envelope: RealtimeMessageEnvelope) => {
    listeners.forEach((listener) => listener(envelope));
  });

  return {
    start: () => connection.start(),
    stop: () => connection.stop(),
    subscribe: (listener: RealtimeMessageListener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
  };
}

export type NotificationConnection = ReturnType<typeof createNotificationConnection>;
