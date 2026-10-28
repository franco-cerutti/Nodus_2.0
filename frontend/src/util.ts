import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from './api/client';
import type { EstadoEquipo, EstadoOt, EstadoPrestamo, EstadoStock, ResultadoSeguridad } from './api/types';

const tz = 'America/Argentina/Cordoba';

/** "2026-10-26" (DateOnly) → "26/10/2026" sin corrimiento de zona horaria */
export const fDate = (d?: string | null) => {
  if (!d) return '—';
  const [y, m, day] = d.slice(0, 10).split('-');
  return `${day}/${m}/${y}`;
};

/** Timestamp UTC → fecha y hora local de Córdoba */
export const fDateTime = (d?: string | null) =>
  d ? new Date(d).toLocaleString('es-AR', { timeZone: tz, day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '—';

/** Hoy en Córdoba como yyyy-mm-dd (para <input type="date">) */
export const hoyISO = () => new Date().toLocaleDateString('en-CA', { timeZone: tz });
export const sumarDias = (iso: string, dias: number) => {
  const d = new Date(`${iso}T12:00:00Z`);
  d.setUTCDate(d.getUTCDate() + dias);
  return d.toISOString().slice(0, 10);
};

/** Igual que DateOnly.AddYears de .NET: 29/02 + 1 año → 28/02 */
export const sumarAnios = (iso: string, anios: number) => {
  const [y, m, d] = iso.split('-').map(Number);
  const ultimo = new Date(Date.UTC(y + anios, m, 0)).getUTCDate();
  return `${y + anios}-${String(m).padStart(2, '0')}-${String(Math.min(d, ultimo)).padStart(2, '0')}`;
};

export const label = (s?: string | null) => (s ?? '').replace(/_/g, ' ').toLowerCase().replace(/^\w/, c => c.toUpperCase());

export const errMsg = (e: unknown) => e instanceof ApiError ? e.message : e instanceof Error ? e.message : 'Error inesperado';

export type Tono = 'ok' | 'warn' | 'bad' | 'info' | 'muted' | 'accent';

export const tonoEquipo: Record<EstadoEquipo, Tono> = {
  OPERATIVO: 'ok', FUERA_DE_SERVICIO: 'bad', EN_MANTENIMIENTO: 'warn', EN_PRESTAMO: 'info', BAJA_PATRIMONIAL: 'muted',
};
export const tonoOt: Record<EstadoOt, Tono> = { PENDIENTE: 'bad', EN_CURSO: 'warn', FINALIZADA: 'ok' };
export const tonoStock: Record<EstadoStock, Tono> = { STOCK_NORMAL: 'ok', STOCK_BAJO: 'warn', AGOTADO: 'bad' };
export const tonoPrestamo: Record<EstadoPrestamo, Tono> = { EN_PRESTAMO: 'info', PRORROGA: 'accent', VENCIDO: 'bad', FINALIZADO: 'muted' };
export const tonoSeg: Record<ResultadoSeguridad, Tono> = { PENDIENTE: 'muted', APROBADA: 'ok', NO_APROBADA: 'bad' };

export function useLoad<T>(fn: () => Promise<T>, deps: unknown[] = []) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const fnRef = useRef(fn);
  fnRef.current = fn;

  const reload = useCallback(async () => {
    setLoading(true);
    try {
      setData(await fnRef.current());
      setError(null);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { reload(); }, deps);
  return { data, setData, error, loading, reload };
}

/** URL pública que se codifica en el QR de cada equipo */
export const urlQr = (codigo: string) => {
  const base = (import.meta.env.VITE_APP_PUBLIC_URL as string | undefined)?.replace(/\/$/, '') || window.location.origin;
  return `${base}/q/${encodeURIComponent(codigo)}`;
};
