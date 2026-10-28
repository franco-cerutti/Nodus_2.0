import { useState, type FormEvent } from 'react';
import { api } from '../api/client';
import type { Catalogos, Item, Parametro, Usuario } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { Alerta, Badge, Campo, Cargando, Modal, useToast } from '../components/ui';
import { errMsg, fDate, useLoad } from '../util';

const DESCRIPCION_PARAM: Record<string, string> = {
  CODIGO_HOSPITAL: 'Prefijo de los códigos patrimoniales nuevos (los ya emitidos no cambian).',
  DIAS_ALERTA_PREVENTIVO: 'Días de anticipación para alertar un mantenimiento preventivo.',
  UMBRAL_DISPONIBILIDAD_FLOTA_CRITICA: 'Porcentaje de disponibilidad por debajo del cual el semáforo pasa a crítico.',
  DIAS_RETORNO_TERCERIZADO: 'Días estimados de retorno de un equipo enviado a reparación externa.',
  VIDA_UTIL_ANIOS: 'Vida útil recomendada (años) para el gráfico de envejecimiento del parque.',
  FAMILIAS_RESPIRADOR: 'Códigos de familia que cuentan como respirador en el KPI de urgencias (separados por coma).',
};

export default function Admin() {
  const { tiene, usuario: yo } = useAuth();
  const esBio = tiene('Bioingeniero');
  const toast = useToast();
  const usuarios = useLoad(() => api.get<Usuario[]>('/admin/usuarios'));
  const roles = useLoad(() => api.get<Item[]>('/admin/roles'));
  const cat = useLoad(() => api.get<Catalogos>('/catalogos'));
  const params = useLoad(() => api.get<Parametro[]>('/admin/parametros'));
  const [editar, setEditar] = useState<Usuario | 'nuevo' | null>(null);
  const [valores, setValores] = useState<Record<string, string>>({});
  const [marca, setMarca] = useState('');

  const guardarParam = async (clave: string) => {
    try {
      await api.put(`/admin/parametros/${clave}`, { valor: valores[clave] });
      toast('Parámetro actualizado');
      setValores(v => { const n = { ...v }; delete n[clave]; return n; });
      params.reload();
    } catch (e) { toast(errMsg(e), 'bad'); }
  };

  const agregarMarca = async (e: FormEvent) => {
    e.preventDefault();
    try { await api.post('/catalogos/marcas', { nombre: marca }); toast('Marca agregada'); setMarca(''); cat.reload(); }
    catch (ex) { toast(errMsg(ex), 'bad'); }
  };

  return (
    <div>
      <div className="cabecera">
        <div>
          <h1>Administración</h1>
          <p className="sub">Usuarios, roles y parámetros del sistema.</p>
        </div>
        {esBio && <button className="btn btn-primario" onClick={() => setEditar('nuevo')}>+ Nuevo usuario</button>}
      </div>

      <section className="panel panel-tabla">
        <h2>Usuarios</h2>
        {usuarios.loading && !usuarios.data ? <Cargando /> : (
          <div className="tabla-scroll">
            <table className="tabla">
              <thead><tr><th>Usuario</th><th>Nombre</th><th>Legajo</th><th>Rol</th><th>Servicio (QR)</th><th>Estado</th><th>Alta</th><th /></tr></thead>
              <tbody>{usuarios.data?.map(u => {
                const editable = esBio || u.rol !== 'Bioingeniero';
                return (
                  <tr key={u.id} className={u.activo ? '' : 'inactivo'}>
                    <td className="mono">{u.nombreUsuario}</td><td>{u.nombreCompleto}</td><td>{u.legajo ?? '—'}</td>
                    <td>{u.rol}</td><td>{u.servicio ?? '—'}</td>
                    <td>{u.activo ? <Badge tono="ok">Activo</Badge> : <Badge tono="muted">Inactivo</Badge>}</td>
                    <td>{fDate(u.createdAt)}</td>
                    <td className="alinear-der">{editable && <button className="btn btn-sm btn-ghost" onClick={() => setEditar(u)}>Editar</button>}</td>
                  </tr>
                );
              })}</tbody>
            </table>
          </div>
        )}
      </section>

      <div className="grid-2">
        <section className="panel">
          <h2>Parámetros del sistema</h2>
          {!esBio && <p className="sub">Solo un Bioingeniero puede modificarlos.</p>}
          {params.data?.map(p => (
            <div key={p.clave} className="param">
              <div>
                <div className="mono param-clave">{p.clave}</div>
                <div className="sub">{DESCRIPCION_PARAM[p.clave] ?? ''}</div>
              </div>
              <div className="param-edit">
                <input className={p.clave === 'FAMILIAS_RESPIRADOR' ? 'param-ancho' : ''} value={valores[p.clave] ?? p.valor} disabled={!esBio} onChange={e => setValores(v => ({ ...v, [p.clave]: e.target.value }))} />
                {esBio && valores[p.clave] !== undefined && valores[p.clave] !== p.valor &&
                  <button className="btn btn-sm btn-primario" onClick={() => guardarParam(p.clave)}>Guardar</button>}
              </div>
            </div>
          ))}
        </section>
        <section className="panel">
          <h2>Marcas</h2>
          <div className="chips">{cat.data?.marcas.map(m => <span key={m.id} className="chip">{m.nombre}</span>)}</div>
          {esBio && (
            <form className="fila-acciones" onSubmit={agregarMarca}>
              <input required maxLength={100} placeholder="Nueva marca" value={marca} onChange={e => setMarca(e.target.value)} />
              <button className="btn">Agregar</button>
            </form>
          )}
        </section>
      </div>

      {editar && roles.data && cat.data && (
        <FormUsuario usuario={editar === 'nuevo' ? null : editar} roles={roles.data.filter(r => esBio || r.nombre !== 'Bioingeniero')}
          servicios={cat.data.servicios} propio={editar !== 'nuevo' && editar.id === yo?.id}
          onClose={() => setEditar(null)} onOk={() => { setEditar(null); usuarios.reload(); toast('Usuario guardado'); }} />
      )}
    </div>
  );
}

function FormUsuario({ usuario, roles, servicios, propio, onClose, onOk }: {
  usuario: Usuario | null; roles: Item[]; servicios: Item[]; propio: boolean; onClose: () => void; onOk: () => void;
}) {
  const [f, setF] = useState({
    nombreUsuario: usuario?.nombreUsuario ?? '', legajo: usuario?.legajo ?? '', nombreCompleto: usuario?.nombreCompleto ?? '',
    password: '', idRol: usuario ? String(usuario.idRol) : '', idServicio: usuario?.idServicio ? String(usuario.idServicio) : '',
    activo: usuario?.activo ?? true,
  });
  const [error, setError] = useState<string | null>(null);
  const rolSel = roles.find(r => String(r.id) === f.idRol);
  const asistencial = rolSel?.nombre === 'Personal Asistencial';

  const enviar = async (e: FormEvent) => {
    e.preventDefault();
    const base = {
      legajo: f.legajo || null, nombreCompleto: f.nombreCompleto, idRol: Number(f.idRol),
      idServicio: asistencial && f.idServicio ? Number(f.idServicio) : null,
    };
    try {
      if (usuario) await api.put(`/admin/usuarios/${usuario.id}`, { ...base, activo: f.activo, nuevaPassword: f.password || null });
      else await api.post('/admin/usuarios', { ...base, nombreUsuario: f.nombreUsuario, password: f.password });
      onOk();
    } catch (ex) { setError(errMsg(ex)); }
  };

  return (
    <Modal titulo={usuario ? `Editar ${usuario.nombreUsuario}` : 'Nuevo usuario'} onClose={onClose} ancho={600}>
      <form onSubmit={enviar} className="form-grid">
        <Campo label="Usuario (login)"><input required disabled={!!usuario} maxLength={50} value={f.nombreUsuario} onChange={e => setF({ ...f, nombreUsuario: e.target.value })} autoCapitalize="none" /></Campo>
        <Campo label="Legajo"><input maxLength={20} value={f.legajo} onChange={e => setF({ ...f, legajo: e.target.value })} /></Campo>
        <Campo label="Nombre completo" ancho="full"><input required maxLength={100} value={f.nombreCompleto} onChange={e => setF({ ...f, nombreCompleto: e.target.value })} /></Campo>
        <Campo label="Rol">
          <select required disabled={propio} value={f.idRol} onChange={e => setF({ ...f, idRol: e.target.value })}>
            <option value="">Seleccionar…</option>
            {roles.map(r => <option key={r.id} value={r.id}>{r.nombre}</option>)}
          </select>
        </Campo>
        {asistencial && (
          <Campo label="Servicio vinculado">
            <select required value={f.idServicio} onChange={e => setF({ ...f, idServicio: e.target.value })}>
              <option value="">Seleccionar…</option>
              {servicios.map(s => <option key={s.id} value={s.id}>{s.nombre}</option>)}
            </select>
          </Campo>
        )}
        <Campo label={usuario ? 'Nueva contraseña (opcional)' : 'Contraseña'} ayuda="Mínimo 8 caracteres.">
          <input type="password" minLength={8} required={!usuario} value={f.password} onChange={e => setF({ ...f, password: e.target.value })} autoComplete="new-password" />
        </Campo>
        {usuario && !propio && (
          <label className="check campo-full"><input type="checkbox" checked={f.activo} onChange={e => setF({ ...f, activo: e.target.checked })} /> Usuario activo</label>
        )}
        <div className="campo-full"><Alerta>{error}</Alerta></div>
        <div className="campo-full modal-pie-inline">
          <button type="button" className="btn" onClick={onClose}>Cancelar</button>
          <button className="btn btn-primario">Guardar</button>
        </div>
      </form>
    </Modal>
  );
}
