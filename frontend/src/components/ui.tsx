import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import { label, type Tono } from '../util';

export function Badge({ tono, children }: { tono: Tono; children: ReactNode }) {
  return <span className={`badge badge-${tono}`}>{children}</span>;
}

export function EstadoBadge<T extends string>({ valor, tonos }: { valor: T; tonos: Record<T, Tono> }) {
  return <Badge tono={tonos[valor]}>{label(valor)}</Badge>;
}

export function Modal({ titulo, onClose, children, ancho = 520, pie }: {
  titulo: string; onClose: () => void; children: ReactNode; ancho?: number; pie?: ReactNode;
}) {
  useEffect(() => {
    const h = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, [onClose]);

  return (
    <div className="modal-fondo" onMouseDown={e => { if (e.target === e.currentTarget) onClose(); }}>
      <div className="modal" style={{ maxWidth: ancho }} role="dialog" aria-modal="true" aria-label={titulo}>
        <div className="modal-cab">
          <h3>{titulo}</h3>
          <button className="icono" onClick={onClose} aria-label="Cerrar">✕</button>
        </div>
        <div className="modal-cuerpo">{children}</div>
        {pie && <div className="modal-pie">{pie}</div>}
      </div>
    </div>
  );
}

export function Campo({ label: l, children, ayuda, ancho }: { label: string; children: ReactNode; ayuda?: ReactNode; ancho?: 'full' }) {
  return (
    <label className={`campo ${ancho === 'full' ? 'campo-full' : ''}`}>
      <span className="campo-label">{l}</span>
      {children}
      {ayuda && <span className="campo-ayuda">{ayuda}</span>}
    </label>
  );
}

export function Alerta({ tono = 'bad', children }: { tono?: Tono; children: ReactNode }) {
  if (!children) return null;
  return <div className={`aviso aviso-${tono}`} role={tono === 'bad' ? 'alert' : 'status'}>{children}</div>;
}

export function Cargando({ texto = 'Cargando…' }: { texto?: string }) {
  return <div className="cargando"><span className="spinner" />{texto}</div>;
}

export function Vacio({ children }: { children: ReactNode }) {
  return <div className="vacio">{children}</div>;
}

export function Kpi({ titulo, valor, detalle, tono }: { titulo: string; valor: ReactNode; detalle?: ReactNode; tono?: Tono }) {
  return (
    <div className={`kpi ${tono ? `kpi-${tono}` : ''}`}>
      <div className="kpi-titulo">{titulo}</div>
      <div className="kpi-valor">{valor}</div>
      {detalle && <div className="kpi-detalle">{detalle}</div>}
    </div>
  );
}

interface Toast { id: number; texto: string; tono: Tono }
const ToastCtx = createContext<(texto: string, tono?: Tono) => void>(() => {});

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const push = useCallback((texto: string, tono: Tono = 'ok') => {
    const id = Date.now() + Math.random();
    setToasts(t => [...t, { id, texto, tono }]);
    setTimeout(() => setToasts(t => t.filter(x => x.id !== id)), tono === 'bad' ? 7000 : 4000);
  }, []);
  return (
    <ToastCtx.Provider value={push}>
      {children}
      <div className="toasts" aria-live="polite">
        {toasts.map(t => <div key={t.id} className={`toast toast-${t.tono}`}>{t.texto}</div>)}
      </div>
    </ToastCtx.Provider>
  );
}
export const useToast = () => useContext(ToastCtx);
