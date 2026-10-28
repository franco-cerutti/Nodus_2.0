import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import QRCode from 'qrcode';
import { api } from '../api/client';
import type { EquipoDetalle, MotivoBaja, Ot } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { Alerta, Campo, Cargando, EstadoBadge, Modal, Vacio, useToast } from '../components/ui';
import { errMsg, fDate, fDateTime, label, tonoEquipo, tonoOt, tonoPrestamo, urlQr, useLoad } from '../util';

const MOTIVOS: { v: MotivoBaja; t: string }[] = [
  { v: 'OBSOLESCENCIA', t: 'Obsolescencia tecnológica' },
  { v: 'IRREPARABILIDAD', t: 'Irreparabilidad' },
  { v: 'COSTO_REPARACION_MAYOR_60', t: 'Costo de reparación mayor al 60% del valor' },
];

export default function EquipoDetallePage() {
  const { id } = useParams();
  const { tiene } = useAuth();
  const nav = useNavigate();
  const toast = useToast();
  const { data, error, loading, reload } = useLoad(() => api.get<EquipoDetalle>(`/equipos/${id}`), [id]);
  const [qr, setQr] = useState<string>('');
  const [baja, setBaja] = useState(false);
  const [creandoPrev, setCreandoPrev] = useState(false);

  const codigo = data?.equipo.codigoPatrimonial;
  useEffect(() => {
    if (!codigo) return;
    QRCode.toDataURL(urlQr(codigo), { width: 360, margin: 1, errorCorrectionLevel: 'M' }).then(setQr);
  }, [codigo]);

  if (loading && !data) return <Cargando />;
  if (error && !data) return <Alerta>{error}</Alerta>;
  if (!data) return null;
  const { equipo: e } = data;
  const dadoDeBaja = e.estado === 'BAJA_PATRIMONIAL';
  const otAbierta = data.ordenes.find(o => o.estado !== 'FINALIZADA');

  const imprimirEtiqueta = () => {
    const w = window.open('', '_blank', 'width=420,height=560');
    if (!w) return;
    w.document.write(`<!doctype html><html><head><title>${e.codigoPatrimonial}</title>
      <style>body{font-family:system-ui,sans-serif;text-align:center;margin:0;padding:16px}
      .et{border:1.5px solid #111;border-radius:8px;padding:12px;display:inline-block;width:62mm}
      img{width:52mm;height:52mm}.c{font:700 14px ui-monospace,monospace;letter-spacing:.5px}
      .s{font-size:11px;color:#333}.h{font-size:10px;text-transform:uppercase;letter-spacing:1px}</style></head>
      <body><div class="et"><div class="h">Bioingeniería · HMN</div><img src="${qr}"/><div class="c">${e.codigoPatrimonial}</div>
      <div class="s">${e.familia} · ${e.marca} ${e.modelo}</div><div class="s">Escanear para reportar fallas</div></div>
      <script>onload=()=>{print();}</script></body></html>`);
    w.document.close();
  };

  const nuevaPreventiva = async () => {
    setCreandoPrev(true);
    try {
      const ot = await api.post<Ot>('/ots', { idEquipo: e.id, tipo: 'PREVENTIVO' });
      toast(`OT preventiva #${ot.id} creada`);
      nav(`/ots/${ot.id}`);
    } catch (ex) {
      toast(errMsg(ex), 'bad');
    } finally {
      setCreandoPrev(false);
    }
  };

  return (
    <div>
      <div className="cabecera">
        <div>
          <Link to="/equipos" className="volver">← Activos</Link>
          <h1 className="mono">{e.codigoPatrimonial}</h1>
          <p className="sub">{e.familia} · {e.marca} {e.modelo} · S/N {e.nroSerie}</p>
        </div>
        <div className="acciones">
          <EstadoBadge valor={e.estado} tonos={tonoEquipo} />
          {!dadoDeBaja && tiene('Bioingeniero', 'Técnico') && !otAbierta && e.estado !== 'EN_PRESTAMO' && (
            <button className="btn" onClick={nuevaPreventiva} disabled={creandoPrev}>Nueva OT preventiva</button>
          )}
          {!dadoDeBaja && tiene('Bioingeniero') && (
            <button className="btn btn-peligro-sutil" onClick={() => setBaja(true)}>Baja patrimonial</button>
          )}
        </div>
      </div>

      {data.baja && (
        <Alerta tono="muted">
          Dado de baja el {fDate(data.baja.fecha)} por <strong>{label(data.baja.motivo)}</strong> ({data.baja.bioingeniero}).
          {data.baja.observaciones && <> {data.baja.observaciones}</>} El equipo no admite más movimientos.
        </Alerta>
      )}
      {data.reportePendiente && (
        <Alerta tono="warn">
          Falla reportada el {fDateTime(data.reportePendiente.fechaHora)} por {data.reportePendiente.solicitante} (Mat. {data.reportePendiente.matricula}):
          “{data.reportePendiente.descripcion}”. {tiene('Bioingeniero', 'Técnico') && <Link to="/mantenimiento">Ver reportes →</Link>}
        </Alerta>
      )}

      <div className="grid-detalle">
        <section className="panel">
          <h2>Datos patrimoniales</h2>
          <dl className="datos">
            <div><dt>Servicio actual</dt><dd>{e.servicioActual}</dd></div>
            <div><dt>Adquisición</dt><dd>{fDate(e.fechaAdquisicion)}</dd></div>
            <div><dt>Vencimiento de garantía</dt><dd>{fDate(e.fechaGarantiaVencimiento)} {e.enGarantia && <span className="chip">Vigente</span>}</dd></div>
            <div><dt>Próximo preventivo</dt><dd className={e.preventivoProximo ? 'txt-warn' : ''}>{fDate(e.fechaMantenimientoPreventivo)} {e.preventivoProximo && <span className="chip chip-warn">Próximo / vencido</span>}</dd></div>
            {otAbierta && <div><dt>OT abierta</dt><dd><Link to={`/ots/${otAbierta.id}`}>#{otAbierta.id} · {label(otAbierta.tipo)} · {label(otAbierta.estado)}</Link></dd></div>}
          </dl>
        </section>

        <section className="panel qr-panel">
          <h2>Código QR</h2>
          {qr ? <img src={qr} alt={`QR ${e.codigoPatrimonial}`} className="qr-img" /> : <Cargando />}
          <p className="sub mono qr-url">{urlQr(e.codigoPatrimonial)}</p>
          <button className="btn btn-sm" onClick={imprimirEtiqueta} disabled={!qr}>Imprimir etiqueta</button>
        </section>
      </div>

      <section className="panel">
        <h2>Órdenes de trabajo</h2>
        {data.ordenes.length === 0 ? <Vacio>Sin órdenes de trabajo.</Vacio> : (
          <table className="tabla">
            <thead><tr><th>OT</th><th>Tipo</th><th>Estado</th><th>Apertura</th><th>Cierre</th><th>Tarea</th></tr></thead>
            <tbody>{data.ordenes.map(o => (
              <tr key={o.id}>
                <td>{tiene('Bioingeniero', 'Técnico') ? <Link to={`/ots/${o.id}`}>#{o.id}</Link> : `#${o.id}`}</td>
                <td>{label(o.tipo)}</td>
                <td><EstadoBadge valor={o.estado} tonos={tonoOt} /></td>
                <td>{fDateTime(o.fechaApertura)}</td>
                <td>{fDateTime(o.fechaCierre)}</td>
                <td className="texto-largo">{o.descripcion ?? '—'}</td>
              </tr>
            ))}</tbody>
          </table>
        )}
      </section>

      <div className="grid-2">
        <section className="panel">
          <h2>Historial de ubicación</h2>
          {data.historial.length === 0 ? <Vacio>Sin traslados desde el alta.</Vacio> : (
            <ol className="linea-tiempo">
              {data.historial.map((h, i) => (
                <li key={i}>
                  <div><strong>{h.origen}</strong> → <strong>{h.destino}</strong></div>
                  <div className="sub">{fDateTime(h.fechaHora)} · {h.usuario}</div>
                </li>
              ))}
            </ol>
          )}
        </section>
        <section className="panel">
          <h2>Préstamos</h2>
          {data.prestamos.length === 0 ? <Vacio>Nunca fue prestado.</Vacio> : (
            <ul className="lista-simple">
              {data.prestamos.map(p => (
                <li key={p.id}>
                  <div><EstadoBadge valor={p.estado} tonos={tonoPrestamo} /> <strong>{p.hospital}</strong></div>
                  <div className="sub">{fDate(p.fechaSalida)} → {fDate(p.fechaDevolucionReal ?? p.fechaDevolucionEstimada)}</div>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      {baja && <BajaModal detalle={data} onClose={() => setBaja(false)} onHecho={() => { setBaja(false); reload(); toast('Baja patrimonial registrada'); }} />}
    </div>
  );
}

/** Doble confirmación: (1) motivo y observaciones, (2) reescribir el código patrimonial. */
function BajaModal({ detalle, onClose, onHecho }: { detalle: EquipoDetalle; onClose: () => void; onHecho: () => void }) {
  const e = detalle.equipo;
  const [paso, setPaso] = useState<1 | 2>(1);
  const [motivo, setMotivo] = useState<MotivoBaja>('OBSOLESCENCIA');
  const [obs, setObs] = useState('');
  const [confirmacion, setConfirmacion] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  const bloqueos = [
    detalle.ordenes.some(o => o.estado !== 'FINALIZADA') && 'tiene una OT abierta',
    detalle.prestamos.some(p => p.estado !== 'FINALIZADO') && 'está en un préstamo activo',
  ].filter(Boolean) as string[];

  const confirmar = async () => {
    setEnviando(true);
    setError(null);
    try {
      await api.post(`/equipos/${e.id}/baja`, { motivo, observaciones: obs || null, confirmacionCodigo: confirmacion });
      onHecho();
    } catch (ex) {
      setError(errMsg(ex));
    } finally {
      setEnviando(false);
    }
  };

  return (
    <Modal titulo={paso === 1 ? 'Baja patrimonial — paso 1 de 2' : 'Baja patrimonial — confirmación final'} onClose={onClose}
      pie={paso === 1 ? <>
        <button className="btn" onClick={onClose}>Cancelar</button>
        <button className="btn btn-peligro" disabled={bloqueos.length > 0 || (motivo === 'COSTO_REPARACION_MAYOR_60' && !obs.trim())} onClick={() => setPaso(2)}>Continuar</button>
      </> : <>
        <button className="btn" onClick={() => setPaso(1)} disabled={enviando}>Volver</button>
        <button className="btn btn-peligro" disabled={enviando || confirmacion.trim().toUpperCase() !== e.codigoPatrimonial} onClick={confirmar}>
          {enviando ? 'Registrando…' : 'Dar de baja definitivamente'}
        </button>
      </>}>
      {paso === 1 ? (
        <>
          {bloqueos.length > 0 && <Alerta>No se puede dar de baja: el equipo {bloqueos.join(' y ')}.</Alerta>}
          <Campo label="Motivo">
            <select value={motivo} onChange={ev => setMotivo(ev.target.value as MotivoBaja)}>
              {MOTIVOS.map(m => <option key={m.v} value={m.v}>{m.t}</option>)}
            </select>
          </Campo>
          <Campo label="Observaciones" ayuda={motivo === 'COSTO_REPARACION_MAYOR_60' ? 'Obligatorio: documentá presupuesto de reparación y valor del equipo.' : undefined}>
            <textarea rows={3} value={obs} onChange={ev => setObs(ev.target.value)} maxLength={2000} />
          </Campo>
        </>
      ) : (
        <>
          <Alerta tono="bad">
            Esta acción es <strong>irreversible</strong>. El equipo quedará en BAJA PATRIMONIAL y bloqueado para cualquier movimiento futuro.
          </Alerta>
          <Campo label={`Para confirmar, escribí el código patrimonial: ${e.codigoPatrimonial}`}>
            <input value={confirmacion} onChange={ev => setConfirmacion(ev.target.value)} autoFocus className="mono" autoCapitalize="characters" />
          </Campo>
          <Alerta>{error}</Alerta>
        </>
      )}
    </Modal>
  );
}
