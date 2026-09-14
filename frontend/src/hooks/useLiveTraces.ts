import { useEffect, useState } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import type { LiveTraceEvent } from '../live/types';

// URL del hub: misma base que el API REST (VITE_API_URL) + la ruta del hub.
const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';
const HUB_URL = `${BASE_URL}/hubs/architecture-map`;

// Nombre del evento servidor→cliente (debe coincidir con SignalRLiveTraceNotifier.EventName).
const TRACE_INGESTED_EVENT = 'TraceIngested';

// Estado de la conexión realtime, expuesto para que la UI muestre feedback.
export type LiveStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface LiveTracesState {
  status: LiveStatus;
  events: LiveTraceEvent[]; // acumulados, más reciente al final
  latest: LiveTraceEvent | null;
}

// Hook del modo LIVE: se conecta al hub del proyecto, se une a su grupo y expone los
// eventos 'TraceIngested' que llegan. Maneja reconexión automática y re-suscripción al
// grupo tras reconectar (los grupos se pierden con el nuevo connectionId).
export function useLiveTraces(projectId: string | null): LiveTracesState {
  const [status, setStatus] = useState<LiveStatus>('disconnected');
  const [events, setEvents] = useState<LiveTraceEvent[]>([]);

  useEffect(() => {
    // Sin proyecto no hay a qué suscribirse: estado limpio.
    if (!projectId) {
      setStatus('disconnected');
      setEvents([]);
      return;
    }

    // Una conexión por proyecto.
    // - withCredentials: manda la cookie de sesión (auth por cookie, igual que el resto).
    // - withAutomaticReconnect: SignalR reintenta solo tras una caída, sin recargar la página.
    const connection = new HubConnectionBuilder()
      .withUrl(HUB_URL, { withCredentials: true })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    // Bandera para no tocar el estado si el efecto se limpió (cambio de projectId/desmontaje).
    let cancelled = false;

    // Cada evento LIVE recibido se acumula.
    connection.on(TRACE_INGESTED_EVENT, (event: LiveTraceEvent) => {
      if (cancelled) return;
      // Verificable en consola (criterio de aceptación de T21); la animación llega en T22.
      console.log('[LIVE] TraceIngested', event);
      setEvents((prev) => [...prev, event]);
    });

    // SignalR nos avisa del ciclo de reconexión.
    connection.onreconnecting(() => {
      if (!cancelled) setStatus('reconnecting');
    });
    connection.onreconnected(() => {
      if (cancelled) return;
      setStatus('connected');
      // Tras reconectar el connectionId es NUEVO y los grupos se pierden: hay que
      // volver a unirse al grupo del proyecto o dejaríamos de recibir sus eventos.
      void connection.invoke('JoinProject', projectId);
    });
    connection.onclose(() => {
      if (!cancelled) setStatus('disconnected');
    });

    setStatus('connecting');
    connection
      .start()
      .then(() => connection.invoke('JoinProject', projectId))
      .then(() => {
        if (!cancelled) setStatus('connected');
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setStatus('disconnected');
          console.error('[LIVE] no se pudo conectar al hub', error);
        }
      });

    // Cleanup: al cambiar de proyecto o desmontar, cerramos la conexión.
    return () => {
      cancelled = true;
      void connection.stop();
    };
  }, [projectId]);

  return {
    status,
    events,
    latest: events.length > 0 ? events[events.length - 1] : null,
  };
}
