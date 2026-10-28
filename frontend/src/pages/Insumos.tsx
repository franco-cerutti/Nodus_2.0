import { useState, type FormEvent } from 'react';
import { api, qs } from '../api/client';
import type { Insumo, MovimientoInsumo, SolicitudCompra } from '../api/types';
import { ROLES_INSUMOS, useAuth } from '../auth/AuthContext';
import { Alerta, Campo, Cargando, EstadoBadge, Modal, Vacio, useToast } from '../components/ui';
import { errMsg, fDateTime, label, tonoStock, useLoad } from '../util';

type Accion = { tipo: 'reponer' | 'comprar' | 'editar' | 'movimientos'; insumo: Insumo } | { tipo: 'nuevo' } | null;

export default function Insumos() {
  const { tiene } = useAuth();
  const gestiona = tiene(...ROLES_INSUMOS);
  const toast = useToast();
  const [estado, setEstado] = useState('');
  const { data, error, loading, reload } = useLoad(() => api.get<Insumo[]>(`/insumos${qs({ estado })}`), [estado]);
  const solicitudes = useLoad(() => gestiona ? api.get<SolicitudCompra[]>('/insumos/solicitudes-compra') : Promise.resolve([]), [gestiona]);
  const [accion, setAccion] = useState<Accion>(null);

  const bajarNota = (s: SolicitudCompra) =>
    api.download(`/insumos/solicitudes-compra/${s.id}/pdf`, `Nota-de-compra-${s.id}.pdf`).catch(e => toast(errMsg(e), 'bad'));

  return (
    <div>
      <div className="cabecera">
        <div>
          <h1>Insumos</h1>
          <p className="sub">El stock se descuenta solo al registrar consumos en una OT.</p>
        </div>
        {gestiona && <button className="btn btn-primario" onClick={() => setAccion({ tipo: 'nuevo' })}>+ Nuevo insumo</button>}
      </div>

      <div className="filtros">
        <select value={estado} onChange={e => setEstado(e.target.value)}>
          <option value="">Todos</option>
          <option value="STOCK_BAJO">Stock bajo</option>
          <option value="AGOTADO">Agotados</option>
          <option value="STOCK_NORMAL">Stock normal</option>
        </select>
      </div>

      <Alerta>{error}</Alerta>
      {loading && !data ? <Cargando /> : data?.length === 0 ? <Vacio>Sin insumos.</Vacio> : (
        <div className="panel panel-tabla"><div className="tabla-scroll">
          <table className="tabla">
            <thead><tr><th>Insumo</th><th>Categoría</th><th className="num">Stock</th><th className="num">Mínimo</th><th>Estado</th><th /></tr></thead>
            <tbody>{data?.map(i => (
              <tr key={i.id}>
                <td><strong>{i.nombre}</strong></td>
                <td>{i.categoria}</td>
                <td className={`num ${i.estadoStock !== 'STOCK_NORMAL' ? 'txt-' + tonoStock[i.estadoStock] : ''}`}><strong>{i.stockActual}</strong></td>
                <td className="num sub">{i.stockMinimo}</td>
                <td><EstadoBadge valor={i.estadoStock} tonos={tonoStock} /></td>
                <td className="alinear-der acciones-fila">
                  <button className="btn btn-sm btn-ghost" onClick={() => setAccion({ tipo: 'movimientos', insumo: i })}>Movimientos</button>
                  {gestiona && <>
                    <button className="btn btn-sm" onClick={() => setAccion({ tipo: 'reponer', insumo: i })}>Reponer</button>
                    <button className={`btn btn-sm ${i.estadoStock !== 'STOCK_NORMAL' ? 'btn-primario' : ''}`} onClick={() => setAccion({ tipo: 'comprar', insumo: i })}>Nota de compra</button>
                    <button className="btn btn-sm btn-ghost" onClick={() => setAccion({ tipo: 'editar', insumo: i })}>Editar</button>
                  </>}
                </td>
              </tr>
            ))}</tbody>
          </table>
        </div></div>
      )}

      {gestiona && (
        <section className="panel">
          <h2>Solicitudes de compra</h2>
          {solicitudes.data?.length === 0 ? <Vacio>Sin solicitudes.</Vacio> : (
            <table className="tabla tabla-compacta">
              <thead><tr><th>N°</th><th>Insumo</th><th className="num">Cantidad</th><th>Fecha</th><th>Solicitó</th><th /></tr></thead>
              <tbody>{solicitudes.data?.map(s => (
                <tr key={s.id}>
                  <td>NC-{String(s.id).padStart(5, '0')}</td><td>{s.insumo}</td><td className="num">{s.cantidad}</td>
                  <td>{fDateTime(s.fecha)}</td><td>{s.usuario}</td>
                  <td className="alinear-der"><button className="btn btn-sm btn-ghost" onClick={() => bajarNota(s)}>PDF</button></td>
                </tr>
              ))}</tbody>
            </table>
          )}
        </section>
      )}

      {accion?.tipo === 'nuevo' && <FormInsumo onClose={() => setAccion(null)} onOk={() => { setAccion(null); reload(); }} />}
      {accion?.tipo === 'editar' && <FormInsumo insumo={accion.insumo} onClose={() => setAccion(null)} onOk={() => { setAccion(null); reload(); }} />}
      {(accion?.tipo === 'reponer' || accion?.tipo === 'comprar') && (
        <Cantidad
          insumo={accion.insumo} tipo={accion.tipo} onClose={() => setAccion(null)}
          onOk={async (s) => {
            setAccion(null); reload(); solicitudes.reload();
            if (s) { toast(`Nota de compra NC-${String(s.id).padStart(5, '0')} registrada`); await bajarNota(s); }
            else toast('Reposición registrada');
          }} />
      )}
      {accion?.tipo === 'movimientos' && <Movimientos insumo={accion.insumo} onClose={() => setAccion(null)} />}
    </div>
  );
}

function FormInsumo({ insumo, onClose, onOk }: { insumo?: Insumo; onClose: () => void; onOk: () => void }) {
  const [f, setF] = useState({ nombre: insumo?.nombre ?? '', categoria: insumo?.categoria ?? '', stockInicial: 0, stockMinimo: insumo?.stockMinimo ?? 1 });
  const [error, setError] = useState<string | null>(null);
  const enviar = async (e: FormEvent) => {
    e.preventDefault();
    try {
      if (insumo) await api.put(`/insumos/${insumo.id}`, { nombre: f.nombre, categoria: f.categoria, stockMinimo: f.stockMinimo });
      else await api.post('/insumos', f);
      onOk();
    } catch (ex) { setError(errMsg(ex)); }
  };
  return (
    <Modal titulo={insumo ? `Editar ${insumo.nombre}` : 'Nuevo insumo'} onClose={onClose}>
      <form onSubmit={enviar} className="form-grid">
        <Campo label="Nombre" ancho="full"><input required maxLength={100} value={f.nombre} onChange={e => setF({ ...f, nombre: e.target.value })} /></Campo>
        <Campo label="Categoría"><input required maxLength={50} value={f.categoria} onChange={e => setF({ ...f, categoria: e.target.value })} placeholder="Filtros, sensores, circuitos…" /></Campo>
        <Campo label="Stock mínimo (umbral de alerta)"><input type="number" min={0} required value={f.stockMinimo} onChange={e => setF({ ...f, stockMinimo: Number(e.target.value) })} /></Campo>
        {!insumo && <Campo label="Stock inicial"><input type="number" min={0} required value={f.stockInicial} onChange={e => setF({ ...f, stockInicial: Number(e.target.value) })} /></Campo>}
        {insumo && <p className="sub campo-full">El stock actual ({insumo.stockActual}) solo cambia con reposiciones y consumos.</p>}
        <div className="campo-full"><Alerta>{error}</Alerta></div>
        <div className="campo-full modal-pie-inline">
          <button type="button" className="btn" onClick={onClose}>Cancelar</button>
          <button className="btn btn-primario">Guardar</button>
        </div>
      </form>
    </Modal>
  );
}

function Cantidad({ insumo, tipo, onClose, onOk }: {
  insumo: Insumo; tipo: 'reponer' | 'comprar'; onClose: () => void; onOk: (s?: SolicitudCompra) => void;
}) {
  const sugerida = Math.max(1, insumo.stockMinimo * 2 - insumo.stockActual);
  const [cantidad, setCantidad] = useState(tipo === 'comprar' ? sugerida : 1);
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  const enviar = async (e: FormEvent) => {
    e.preventDefault();
    setEnviando(true);
    try {
      if (tipo === 'reponer') { await api.post(`/insumos/${insumo.id}/reposiciones`, { cantidad }); onOk(); }
      else onOk(await api.post<SolicitudCompra>(`/insumos/${insumo.id}/solicitudes-compra`, { cantidad }));
    } catch (ex) { setError(errMsg(ex)); } finally { setEnviando(false); }
  };
  return (
    <Modal titulo={tipo === 'reponer' ? `Reponer ${insumo.nombre}` : `Nota de compra — ${insumo.nombre}`} onClose={onClose}>
      <form onSubmit={enviar}>
        <p>Stock actual <strong>{insumo.stockActual}</strong> · mínimo {insumo.stockMinimo} · <EstadoBadge valor={insumo.estadoStock} tonos={tonoStock} /></p>
        <Campo label={tipo === 'reponer' ? 'Cantidad ingresada' : '¿Cuántas unidades comprar?'} ayuda={tipo === 'comprar' ? `Sugerido: ${sugerida} (para llegar al doble del mínimo)` : undefined}>
          <input type="number" min={1} required autoFocus value={cantidad} onChange={e => setCantidad(Number(e.target.value))} />
        </Campo>
        <Alerta>{error}</Alerta>
        <div className="modal-pie-inline">
          <button type="button" className="btn" onClick={onClose}>Cancelar</button>
          <button className="btn btn-primario" disabled={enviando}>{tipo === 'reponer' ? 'Registrar reposición' : 'Registrar y generar PDF'}</button>
        </div>
      </form>
    </Modal>
  );
}

function Movimientos({ insumo, onClose }: { insumo: Insumo; onClose: () => void }) {
  const { data, loading, error } = useLoad(() => api.get<MovimientoInsumo[]>(`/insumos/${insumo.id}/movimientos`));
  return (
    <Modal titulo={`Movimientos — ${insumo.nombre}`} onClose={onClose} ancho={720}>
      <Alerta>{error}</Alerta>
      {loading ? <Cargando /> : data?.length === 0 ? <Vacio>Sin movimientos.</Vacio> : (
        <table className="tabla tabla-compacta">
          <thead><tr><th>Fecha</th><th>Tipo</th><th className="num">Cantidad</th><th>Referencia</th><th>Usuario</th></tr></thead>
          <tbody>{data?.map((m, i) => (
            <tr key={i}>
              <td>{fDateTime(m.fecha)}</td><td>{label(m.tipo)}</td>
              <td className={`num ${m.cantidad < 0 ? 'txt-bad' : 'txt-ok'}`}>{m.cantidad > 0 ? `+${m.cantidad}` : m.cantidad}</td>
              <td>{m.referencia}</td><td>{m.usuario}</td>
            </tr>
          ))}</tbody>
        </table>
      )}
    </Modal>
  );
}
