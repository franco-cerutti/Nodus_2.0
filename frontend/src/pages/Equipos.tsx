import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api, qs } from '../api/client';
import type { Catalogos, Equipo, EstadoEquipo } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { Alerta, Campo, Cargando, EstadoBadge, Modal, Vacio, useToast } from '../components/ui';
import { errMsg, fDate, hoyISO, label, sumarAnios, sumarDias, tonoEquipo, useLoad } from '../util';

const ESTADOS: EstadoEquipo[] = ['OPERATIVO', 'FUERA_DE_SERVICIO', 'EN_MANTENIMIENTO', 'EN_PRESTAMO', 'BAJA_PATRIMONIAL'];

export default function Equipos() {
  const { tiene } = useAuth();
  const [f, setF] = useState({ q: '', servicio: '', familia: '', estado: '' });
  const [alta, setAlta] = useState(false);
  const cat = useLoad(() => api.get<Catalogos>('/catalogos'));
  const { data, error, loading, reload } = useLoad(
    () => api.get<Equipo[]>(`/equipos${qs({ ...f, incluirBajas: f.estado === 'BAJA_PATRIMONIAL' })}`),
    [f.servicio, f.familia, f.estado, f.q]);

  return (
    <div>
      <div className="cabecera">
        <div>
          <h1>Activos</h1>
          <p className="sub">Inventario patrimonial de equipos de soporte ventilatorio.</p>
        </div>
        {tiene('Bioingeniero') && <button className="btn btn-primario" onClick={() => setAlta(true)}>+ Alta de equipo</button>}
      </div>

      <div className="filtros">
        <input className="buscador" placeholder="Código, serie o modelo…" value={f.q} onChange={e => setF({ ...f, q: e.target.value })} />
        <select value={f.servicio} onChange={e => setF({ ...f, servicio: e.target.value })}>
          <option value="">Todos los servicios</option>
          {cat.data?.servicios.map(s => <option key={s.id} value={s.id}>{s.nombre}</option>)}
        </select>
        <select value={f.familia} onChange={e => setF({ ...f, familia: e.target.value })}>
          <option value="">Todos los tipos</option>
          {cat.data?.familias.map(s => <option key={s.id} value={s.id}>{s.nombre}</option>)}
        </select>
        <select value={f.estado} onChange={e => setF({ ...f, estado: e.target.value })}>
          <option value="">Activos (sin bajas)</option>
          {ESTADOS.map(s => <option key={s} value={s}>{label(s)}</option>)}
        </select>
      </div>

      <Alerta>{error}</Alerta>
      {loading && !data ? <Cargando /> : data && data.length === 0 ? <Vacio>No hay equipos con esos filtros.</Vacio> : data && (
        <div className="panel panel-tabla">
          <div className="tabla-scroll">
            <table className="tabla">
              <thead><tr><th>Código patrimonial</th><th>Tipo</th><th>Marca / modelo</th><th>N° serie</th><th>Servicio</th><th>Estado</th><th>Garantía</th><th>Próx. preventivo</th></tr></thead>
              <tbody>
                {data.map(e => (
                  <tr key={e.id} className="fila-link">
                    <td><Link to={`/equipos/${e.id}`} className="codigo">{e.codigoPatrimonial}</Link></td>
                    <td>{e.familia}</td>
                    <td>{e.marca} <span className="sub">{e.modelo}</span></td>
                    <td className="mono">{e.nroSerie}</td>
                    <td>{e.servicioActual}</td>
                    <td><EstadoBadge valor={e.estado} tonos={tonoEquipo} /></td>
                    <td className={e.enGarantia ? '' : 'sub'}>{fDate(e.fechaGarantiaVencimiento)}</td>
                    <td className={e.preventivoProximo ? 'txt-warn' : ''}>{fDate(e.fechaMantenimientoPreventivo)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {alta && cat.data && <AltaEquipo cat={cat.data} onClose={() => setAlta(false)} onCreado={reload} />}
    </div>
  );
}

function AltaEquipo({ cat, onClose, onCreado }: { cat: Catalogos; onClose: () => void; onCreado: () => void }) {
  const toast = useToast();
  const nav = useNavigate();
  const [f, setF] = useState({ idServicio: '', idFamilia: '', idMarca: '', modelo: '', nroSerie: '', fechaAdquisicion: hoyISO() });
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  const garantia = f.fechaAdquisicion ? sumarAnios(f.fechaAdquisicion, 1) : '';
  const preventivo = garantia ? sumarDias(garantia, 365) : '';
  const serv = cat.servicios.find(s => String(s.id) === f.idServicio);
  const fam = cat.familias.find(s => String(s.id) === f.idFamilia);

  const enviar = async (e: FormEvent) => {
    e.preventDefault();
    setEnviando(true);
    setError(null);
    try {
      const eq = await api.post<Equipo>('/equipos', {
        ...f, idServicio: Number(f.idServicio), idFamilia: Number(f.idFamilia), idMarca: Number(f.idMarca),
      });
      toast(`Equipo dado de alta: ${eq.codigoPatrimonial}`);
      onCreado();
      nav(`/equipos/${eq.id}`);
    } catch (ex) {
      setError(errMsg(ex));
    } finally {
      setEnviando(false);
    }
  };

  return (
    <Modal titulo="Alta de equipo" onClose={onClose} ancho={640}>
      <form onSubmit={enviar} className="form-grid">
        <Campo label="Servicio de alta">
          <select required value={f.idServicio} onChange={e => setF({ ...f, idServicio: e.target.value })}>
            <option value="">Seleccionar…</option>
            {cat.servicios.map(s => <option key={s.id} value={s.id}>{s.nombre}</option>)}
          </select>
        </Campo>
        <Campo label="Tipo de equipo (familia)">
          <select required value={f.idFamilia} onChange={e => setF({ ...f, idFamilia: e.target.value })}>
            <option value="">Seleccionar…</option>
            {cat.familias.map(s => <option key={s.id} value={s.id}>{s.nombre}</option>)}
          </select>
        </Campo>
        <Campo label="Marca">
          <select required value={f.idMarca} onChange={e => setF({ ...f, idMarca: e.target.value })}>
            <option value="">Seleccionar…</option>
            {cat.marcas.map(s => <option key={s.id} value={s.id}>{s.nombre}</option>)}
          </select>
        </Campo>
        <Campo label="Modelo"><input required maxLength={80} value={f.modelo} onChange={e => setF({ ...f, modelo: e.target.value })} /></Campo>
        <Campo label="N° de serie"><input required maxLength={80} value={f.nroSerie} onChange={e => setF({ ...f, nroSerie: e.target.value })} /></Campo>
        <Campo label="Fecha de adquisición"><input type="date" required max={hoyISO()} value={f.fechaAdquisicion} onChange={e => setF({ ...f, fechaAdquisicion: e.target.value })} /></Campo>

        <div className="campo-full resumen-alta">
          <div><span className="sub">Código patrimonial</span><strong className="mono">HMN-{serv?.codigo ?? '···'}-{fam?.codigo ?? '···'}-####</strong></div>
          <div><span className="sub">Vence garantía</span><strong>{fDate(garantia)}</strong></div>
          <div><span className="sub">Primer preventivo</span><strong>{fDate(preventivo)}</strong></div>
        </div>
        <p className="campo-full sub">El correlativo se asigna en el momento del alta y el código no cambia aunque el equipo se traslade.</p>

        <div className="campo-full"><Alerta>{error}</Alerta></div>
        <div className="campo-full modal-pie-inline">
          <button type="button" className="btn" onClick={onClose}>Cancelar</button>
          <button className="btn btn-primario" disabled={enviando}>{enviando ? 'Guardando…' : 'Dar de alta'}</button>
        </div>
      </form>
    </Modal>
  );
}
