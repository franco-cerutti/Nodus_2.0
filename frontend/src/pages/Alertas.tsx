import { useState } from 'react';
import { Link } from 'react-router-dom';
import { api, qs } from '../api/client';
import type { Alerta as A, TipoAlerta } from '../api/types';
import { Alerta, Badge, Cargando, Vacio, useToast } from '../components/ui';
import { errMsg, fDateTime, type Tono, useLoad } from '../util';

const INFO: Record<TipoAlerta, { texto: string; tono: Tono; link: (id: number) => string }> = {
  PREVENTIVO_PROXIMO: { texto: 'Preventivo', tono: 'warn', link: id => `/equipos/${id}` },
  PRESTAMO_VENCIDO: { texto: 'Préstamo vencido', tono: 'bad', link: () => '/prestamos' },
  TERCERIZADO_VENCIDO: { texto: 'Tercerizado vencido', tono: 'bad', link: () => '/mantenimiento' },
  STOCK_BAJO: { texto: 'Stock bajo', tono: 'warn', link: () => '/insumos' },
  REPORTE_FALLA: { texto: 'Reporte de falla', tono: 'bad', link: () => '/mantenimiento' },
};

export default function Alertas() {
  const toast = useToast();
  const [soloAbiertas, setSoloAbiertas] = useState(true);
  const { data, setData, error, loading } = useLoad(() => api.get<A[]>(`/alertas${qs({ soloAbiertas })}`), [soloAbiertas]);

  const leer = async (a: A) => {
    try {
      await api.put(`/alertas/${a.id}/leida`);
      setData(d => d && d.map(x => x.id === a.id ? { ...x, leida: true } : x));
    } catch (e) { toast(errMsg(e), 'bad'); }
  };

  return (
    <div>
      <div className="cabecera">
        <div>
          <h1>Alertas</h1>
          <p className="sub">Se generan a diario (preventivos, préstamos y tercerizados vencidos) y en tiempo real (stock bajo). Se resuelven solas al corregirse la causa.</p>
        </div>
        <label className="check"><input type="checkbox" checked={soloAbiertas} onChange={e => setSoloAbiertas(e.target.checked)} /> Solo sin resolver</label>
      </div>
      <Alerta>{error}</Alerta>
      {loading && !data ? <Cargando /> : data?.length === 0 ? <Vacio>Sin alertas pendientes. 👍</Vacio> : (
        <ul className="alertas">
          {data?.map(a => {
            const i = INFO[a.tipo];
            return (
              <li key={a.id} className={`alerta-item ${a.leida ? 'leida' : ''} ${a.resuelta ? 'resuelta' : ''}`}>
                <Badge tono={a.resuelta ? 'muted' : i.tono}>{i.texto}</Badge>
                <div className="alerta-txt">
                  <Link to={i.link(a.idReferencia)}>{a.mensaje}</Link>
                  <div className="sub">{fDateTime(a.fecha)}{a.resuelta && ' · resuelta'}</div>
                </div>
                {!a.leida && !a.resuelta && <button className="btn btn-sm btn-ghost" onClick={() => leer(a)}>Marcar leída</button>}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
