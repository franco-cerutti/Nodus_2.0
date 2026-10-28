import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api, qs } from '../api/client';
import type { Catalogos, Equipo, EstadoOt, Ot, ReporteFalla, TipoMantenimiento } from '../api/types';
import { Alerta, Campo, Cargando, EstadoBadge, Modal, Vacio, useToast } from '../components/ui';
import { errMsg, fDate, fDateTime, hoyISO, label, tonoEquipo, tonoOt, useLoad } from '../util';

type Tab = 'reportes' | 'ots';

export default function Mantenimiento() {
  const [tab, setTab] = useState<Tab>('reportes');
  const [nueva, setNueva] = useState(false);
  const cat = useLoad(() => api.get<Catalogos>('/catalogos'));

  return (
    <div>
      <div className="cabecera">
        <div>
          <h1>Mantenimiento</h1>
          <p className="sub">Reportes de falla del personal asistencial y órdenes de trabajo del taller.</p>
        </div>
        <button className="btn btn-primario" onClick={() => setNueva(true)}>+ Nueva OT</button>
      </div>
      <div className="tabs">
        <button className={tab === 'reportes' ? 'activo' : ''} onClick={() => setTab('reportes')}>Reportes de falla</button>
        <button className={tab === 'ots' ? 'activo' : ''} onClick={() => setTab('ots')}>Órdenes de trabajo</button>
      </div>
      {tab === 'reportes' ? <Reportes tecnicos={cat.data?.tecnicos ?? []} /> : <Ots />}
      {nueva && cat.data && <NuevaOt cat={cat.data} onClose={() => setNueva(false)} />}
    </div>
  );
}

function Reportes({ tecnicos }: { tecnicos: Catalogos['tecnicos'] }) {
  const toast = useToast();
  const nav = useNavigate();
  const [estado, setEstado] = useState('PENDIENTE');
  const { data, error, loading, reload } = useLoad(() => api.get<ReporteFalla[]>(`/reportes-falla${qs({ estado })}`), [estado]);
  const [convertir, setConvertir] = useState<ReporteFalla | null>(null);
  const [descartar, setDescartar] = useState<ReporteFalla | null>(null);
  const [tecnico, setTecnico] = useState('');
  const [motivo, setMotivo] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const accion = async (fn: () => Promise<void>) => {
    setEnviando(true); setErr(null);
    try { await fn(); } catch (e) { setErr(errMsg(e)); } finally { setEnviando(false); }
  };

  return (
    <>
      <div className="filtros">
        <select value={estado} onChange={e => setEstado(e.target.value)}>
          <option value="PENDIENTE">Pendientes</option>
          <option value="CONVERTIDO_EN_OT">Convertidos en OT</option>
          <option value="DESCARTADO">Descartados</option>
          <option value="">Todos</option>
        </select>
      </div>
      <Alerta>{error}</Alerta>
      {loading && !data ? <Cargando /> : data?.length === 0 ? <Vacio>No hay reportes {estado ? label(estado).toLowerCase() + 's' : ''}.</Vacio> : (
        <div className="lista-cards">
          {data?.map(r => (
            <article key={r.id} className="card-reporte">
              <div className="card-reporte-cab">
                <div>
                  <span className="codigo">{r.codigoPatrimonial}</span> <span className="sub">· {r.servicio}</span>
                  <div className="sub">{r.equipo}</div>
                </div>
                <div className="sub alinear-der">N° {r.id}<br />{fDateTime(r.fechaHora)}</div>
              </div>
              <p className="card-reporte-desc">“{r.descripcion}”</p>
              <div className="sub">Reportó {r.solicitante} · Mat. {r.matricula} · sesión {r.usuarioSesion}</div>
              {r.estado === 'DESCARTADO' && <div className="sub">Descartado: {r.motivoDescarte}</div>}
              {r.estado === 'PENDIENTE' ? (
                <div className="fila-acciones">
                  <button className="btn btn-primario btn-sm" onClick={() => { setConvertir(r); setTecnico(''); setErr(null); }}>Confirmar y generar OT</button>
                  <button className="btn btn-sm" onClick={() => { setDescartar(r); setMotivo(''); setErr(null); }}>Descartar</button>
                </div>
              ) : <div className="sub">Estado: {label(r.estado)}</div>}
            </article>
          ))}
        </div>
      )}

      {convertir && (
        <Modal titulo={`Generar OT correctiva — ${convertir.codigoPatrimonial}`} onClose={() => setConvertir(null)} pie={<>
          <button className="btn" onClick={() => setConvertir(null)}>Cancelar</button>
          <button className="btn btn-primario" disabled={enviando} onClick={() => accion(async () => {
            const ot = await api.post<Ot>(`/reportes-falla/${convertir.id}/convertir`, { idTecnicoAsignado: tecnico ? Number(tecnico) : null });
            toast(`OT #${ot.id} creada. El equipo quedó FUERA DE SERVICIO.`);
            setConvertir(null); reload(); nav(`/ots/${ot.id}`);
          })}>Generar OT</button>
        </>}>
          <p>Al confirmar el reporte se crea una OT <strong>correctiva</strong> y el equipo pasa a <strong>fuera de servicio</strong>.</p>
          <Campo label="Técnico asignado (opcional)">
            <select value={tecnico} onChange={e => setTecnico(e.target.value)}>
              <option value="">Sin asignar</option>
              {tecnicos.map(t => <option key={t.id} value={t.id}>{t.nombre}</option>)}
            </select>
          </Campo>
          <Alerta>{err}</Alerta>
        </Modal>
      )}

      {descartar && (
        <Modal titulo="Descartar reporte" onClose={() => setDescartar(null)} pie={<>
          <button className="btn" onClick={() => setDescartar(null)}>Cancelar</button>
          <button className="btn btn-peligro" disabled={enviando || motivo.trim().length < 3} onClick={() => accion(async () => {
            await api.post(`/reportes-falla/${descartar.id}/descartar`, { motivo });
            toast('Reporte descartado'); setDescartar(null); reload();
          })}>Descartar</button>
        </>}>
          <Campo label="Motivo del descarte (obligatorio)">
            <textarea rows={3} value={motivo} onChange={e => setMotivo(e.target.value)} placeholder="Ej.: uso incorrecto, se capacitó al personal; falla no reproducible…" />
          </Campo>
          <Alerta>{err}</Alerta>
        </Modal>
      )}
    </>
  );
}

function Ots() {
  const [f, setF] = useState({ estado: '', tipo: '', soloAbiertas: true });
  const { data, error, loading } = useLoad(
    () => api.get<Ot[]>(`/ots${qs({ estado: f.estado, tipo: f.tipo, soloAbiertas: !f.estado && f.soloAbiertas })}`),
    [f.estado, f.tipo, f.soloAbiertas]);

  return (
    <>
      <div className="filtros">
        <select value={f.estado} onChange={e => setF({ ...f, estado: e.target.value })}>
          <option value="">{f.soloAbiertas ? 'Abiertas' : 'Todos los estados'}</option>
          {(['PENDIENTE', 'EN_CURSO', 'FINALIZADA'] as EstadoOt[]).map(s => <option key={s} value={s}>{label(s)}</option>)}
        </select>
        <select value={f.tipo} onChange={e => setF({ ...f, tipo: e.target.value })}>
          <option value="">Todos los tipos</option>
          {(['CORRECTIVO', 'PREVENTIVO', 'TERCERIZADO'] as TipoMantenimiento[]).map(s => <option key={s} value={s}>{label(s)}</option>)}
        </select>
        <label className="check"><input type="checkbox" checked={f.soloAbiertas} onChange={e => setF({ ...f, soloAbiertas: e.target.checked })} /> Solo abiertas</label>
      </div>
      <Alerta>{error}</Alerta>
      {loading && !data ? <Cargando /> : data?.length === 0 ? <Vacio>Sin órdenes de trabajo.</Vacio> : (
        <div className="panel panel-tabla"><div className="tabla-scroll">
          <table className="tabla">
            <thead><tr><th>OT</th><th>Equipo</th><th>Tipo</th><th>Estado</th><th>Seg. eléctrica</th><th>Técnico</th><th>Apertura</th><th>Equipo</th></tr></thead>
            <tbody>{data?.map(o => (
              <tr key={o.id}>
                <td><Link to={`/ots/${o.id}`}>#{o.id}</Link></td>
                <td><span className="codigo">{o.codigoPatrimonial}</span><div className="sub">{o.servicio}</div></td>
                <td>{label(o.tipo)}{o.tercerizado?.vencido && <span className="chip chip-bad">Retorno vencido</span>}</td>
                <td><EstadoBadge valor={o.estado} tonos={tonoOt} /></td>
                <td>{label(o.seguridadElectrica)}</td>
                <td>{o.tecnicoAsignado ?? <span className="sub">—</span>}</td>
                <td>{fDateTime(o.fechaApertura)}</td>
                <td><EstadoBadge valor={o.estadoEquipo} tonos={tonoEquipo} /></td>
              </tr>
            ))}</tbody>
          </table>
        </div></div>
      )}
    </>
  );
}

function NuevaOt({ cat, onClose }: { cat: Catalogos; onClose: () => void }) {
  const nav = useNavigate();
  const toast = useToast();
  const equipos = useLoad(() => api.get<Equipo[]>('/equipos'));
  const [f, setF] = useState({ idEquipo: '', tipo: 'PREVENTIVO' as TipoMantenimiento, idTecnicoAsignado: '', descripcion: '',
    proveedor: '', enGarantia: false, fechaEnvio: hoyISO() });
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  const eq = equipos.data?.find(e => String(e.id) === f.idEquipo);

  const enviar = async (e: FormEvent) => {
    e.preventDefault();
    setEnviando(true); setError(null);
    try {
      const ot = await api.post<Ot>('/ots', {
        idEquipo: Number(f.idEquipo), tipo: f.tipo,
        idTecnicoAsignado: f.idTecnicoAsignado ? Number(f.idTecnicoAsignado) : null,
        descripcionTareaTecnica: f.descripcion || null,
        tercerizado: f.tipo === 'TERCERIZADO' ? { proveedorFabricaNombre: f.proveedor, enGarantia: f.enGarantia, fechaEnvio: f.fechaEnvio } : null,
      });
      toast(`OT #${ot.id} creada`);
      nav(`/ots/${ot.id}`);
    } catch (ex) {
      setError(errMsg(ex));
    } finally {
      setEnviando(false);
    }
  };

  return (
    <Modal titulo="Nueva orden de trabajo" onClose={onClose} ancho={620}>
      <form onSubmit={enviar} className="form-grid">
        <Campo label="Equipo" ancho="full">
          <select required value={f.idEquipo} onChange={e => {
            const sel = equipos.data?.find(x => String(x.id) === e.target.value);
            setF({ ...f, idEquipo: e.target.value, enGarantia: sel?.enGarantia ?? false });
          }}>
            <option value="">{equipos.loading ? 'Cargando…' : 'Seleccionar…'}</option>
            {equipos.data?.map(x => <option key={x.id} value={x.id}>{x.codigoPatrimonial} — {x.familia} {x.marca} {x.modelo} ({label(x.estado)})</option>)}
          </select>
        </Campo>
        <Campo label="Tipo">
          <select value={f.tipo} onChange={e => setF({ ...f, tipo: e.target.value as TipoMantenimiento })}>
            <option value="PREVENTIVO">Preventivo</option>
            <option value="CORRECTIVO">Correctivo (sin reporte)</option>
            <option value="TERCERIZADO">Tercerizado (reparación externa)</option>
          </select>
        </Campo>
        <Campo label="Técnico asignado">
          <select value={f.idTecnicoAsignado} onChange={e => setF({ ...f, idTecnicoAsignado: e.target.value })}>
            <option value="">Sin asignar</option>
            {cat.tecnicos.map(t => <option key={t.id} value={t.id}>{t.nombre}</option>)}
          </select>
        </Campo>
        {f.tipo === 'TERCERIZADO' && <>
          <Campo label="Proveedor / fábrica"><input required maxLength={150} value={f.proveedor} onChange={e => setF({ ...f, proveedor: e.target.value })} /></Campo>
          <Campo label="Fecha de envío"><input type="date" required value={f.fechaEnvio} onChange={e => setF({ ...f, fechaEnvio: e.target.value })} /></Campo>
          <label className="check campo-full"><input type="checkbox" checked={f.enGarantia} onChange={e => setF({ ...f, enGarantia: e.target.checked })} />
            En garantía {eq && <span className="sub">(vence {fDate(eq.fechaGarantiaVencimiento)})</span>}</label>
          <p className="sub campo-full">La OT nace en curso, el equipo pasa a mantenimiento y la fecha probable de retorno se calcula con el parámetro del sistema.</p>
        </>}
        <Campo label="Descripción / tarea a realizar" ancho="full">
          <textarea rows={3} value={f.descripcion} onChange={e => setF({ ...f, descripcion: e.target.value })} />
        </Campo>
        <div className="campo-full"><Alerta>{error}</Alerta></div>
        <div className="campo-full modal-pie-inline">
          <button type="button" className="btn" onClick={onClose}>Cancelar</button>
          <button className="btn btn-primario" disabled={enviando}>{enviando ? 'Creando…' : 'Crear OT'}</button>
        </div>
      </form>
    </Modal>
  );
}
