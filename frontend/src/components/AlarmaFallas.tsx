import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import type { NovedadesFallas, ReporteFalla } from '../api/types';
import { fDateTime } from '../util';

const INTERVALO_MS = 15_000;

const leer = (clave: string) => {
  try { const v = localStorage.getItem(clave); return v === null ? null : Number(v); } catch { return null; }
};
const guardar = (clave: string, valor: number) => {
  try { localStorage.setItem(clave, String(valor)); } catch { }
};

function sonar() {
  try {
    const ctx = new AudioContext();
    [0, 0.28].forEach(t => {
      const o = ctx.createOscillator();
      const g = ctx.createGain();
      o.type = 'square';
      o.frequency.value = 880;
      g.gain.setValueAtTime(0.0001, ctx.currentTime + t);
      g.gain.exponentialRampToValueAtTime(0.12, ctx.currentTime + t + 0.02);
      g.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + t + 0.22);
      o.connect(g).connect(ctx.destination);
      o.start(ctx.currentTime + t);
      o.stop(ctx.currentTime + t + 0.24);
    });
    setTimeout(() => ctx.close(), 800);
  } catch { }
}

/**
 * Consulta periódica de reportes de falla pendientes.
 * @param idUsuario usuario en sesión; la marca de "visto" se guarda por usuario.
 * @param habilitado false para roles que no gestionan mantenimiento.
 */
export function useNovedadesFallas(idUsuario: number, habilitado: boolean) {
  const clave = `nodus.fallas.visto.${idUsuario}`;
  const [pendientes, setPendientes] = useState(0);
  const [nuevos, setNuevos] = useState<ReporteFalla[]>([]);
  const ultimoAvisado = useRef(0);

  const consultar = useCallback(async () => {
    if (!habilitado) return;
    try {
      const visto = leer(clave);
      const r = await api.get<NovedadesFallas>(`/reportes-falla/novedades${visto === null ? '' : `?despuesDe=${visto}`}`);
      setPendientes(r.pendientes);
      if (visto === null) { guardar(clave, r.ultimoId); return; }
      setNuevos(r.nuevos);
      const max = r.nuevos.reduce((m, x) => Math.max(m, x.id), 0);
      if (max > ultimoAvisado.current) { ultimoAvisado.current = max; sonar(); }
    } catch { }
  }, [clave, habilitado]);

  useEffect(() => {
    if (!habilitado) return;
    consultar();
    const t = setInterval(consultar, INTERVALO_MS);
    const alVolver = () => { if (document.visibilityState === 'visible') consultar(); };
    document.addEventListener('visibilitychange', alVolver);
    return () => { clearInterval(t); document.removeEventListener('visibilitychange', alVolver); };
  }, [consultar, habilitado]);

  useEffect(() => {
    const base = document.title.replace(/^\(\d+\) /, '');
    document.title = nuevos.length ? `(${nuevos.length}) ${base}` : base;
  }, [nuevos.length]);

  const reconocer = useCallback(() => {
    const max = nuevos.reduce((m, x) => Math.max(m, x.id), leer(clave) ?? 0);
    guardar(clave, max);
    setNuevos([]);
  }, [clave, nuevos]);

  return { pendientes, nuevos, reconocer };
}

export default function AlarmaFallas({ nuevos, onReconocer }: { nuevos: ReporteFalla[]; onReconocer: () => void }) {
  const nav = useNavigate();
  if (nuevos.length === 0) return null;
  const [primero, ...resto] = nuevos;

  return (
    <div className="alarma-falla" role="alertdialog" aria-live="assertive" aria-label="Nuevo reporte de falla">
      <div className="alarma-icono"><i className="fa-solid fa-bell" /></div>
      <div className="alarma-cuerpo">
        <div className="alarma-titulo">
          {nuevos.length === 1 ? 'NUEVO REPORTE DE FALLA' : `${nuevos.length} NUEVOS REPORTES DE FALLA`}
        </div>
        <div className="alarma-equipo">
          <span className="mono">{primero.codigoPatrimonial}</span> · {primero.servicio}
        </div>
        <div className="alarma-desc">“{primero.descripcion}”</div>
        <div className="alarma-meta">{primero.solicitante} · Mat. {primero.matricula} · {fDateTime(primero.fechaHora)}</div>
        {resto.length > 0 && (
          <div className="alarma-resto">
            {resto.slice(0, 3).map(r => <span key={r.id} className="mono">{r.codigoPatrimonial}</span>)}
            {resto.length > 3 && <span>+{resto.length - 3}</span>}
          </div>
        )}
      </div>
      <div className="alarma-acciones">
        <button className="btn btn-sm alarma-ver" onClick={() => { onReconocer(); nav('/mantenimiento'); }}>
          <i className="fa-solid fa-arrow-right" /> Ver reportes
        </button>
        <button className="btn btn-sm alarma-ok" onClick={onReconocer}>Entendido</button>
      </div>
    </div>
  );
}
