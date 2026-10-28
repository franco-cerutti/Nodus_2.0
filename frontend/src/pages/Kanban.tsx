import { useMemo, useState, type DragEvent } from 'react';
import { Link } from 'react-router-dom';
import { api, ApiError } from '../api/client';
import type { Equipo, Item, Kanban } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { Alerta, Cargando, EstadoBadge, Modal, useToast } from '../components/ui';
import { errMsg, label, tonoEquipo, useLoad } from '../util';

const NO_TRASLADABLES = new Set(['EN_MANTENIMIENTO', 'EN_PRESTAMO', 'BAJA_PATRIMONIAL']);

interface Movimiento { equipo: Equipo; destino: Item }

export default function KanbanPage() {
  const { tiene } = useAuth();
  const puedeMover = tiene('Bioingeniero');
  const toast = useToast();
  const { data, setData, error, loading, reload } = useLoad(() => api.get<Kanban>('/equipos/kanban'));

  const [filtro, setFiltro] = useState('');
  const [arrastrando, setArrastrando] = useState<number | null>(null);
  const [sobre, setSobre] = useState<number | null>(null);
  const [mov, setMov] = useState<Movimiento | null>(null);
  const [moviendo, setMoviendo] = useState(false);
  const [errorMov, setErrorMov] = useState<string | null>(null);
  const [resaltado, setResaltado] = useState<number | null>(null);
  const [elegirDestino, setElegirDestino] = useState<Equipo | null>(null);

  const columnas = useMemo(() => {
    if (!data) return [];
    const f = filtro.trim().toLowerCase();
    return data.servicios.map(s => ({
      servicio: s,
      equipos: data.equipos.filter(e => e.idServicioActual === s.id &&
        (!f || `${e.codigoPatrimonial} ${e.familia} ${e.marca} ${e.modelo} ${e.nroSerie}`.toLowerCase().includes(f))),
    }));
  }, [data, filtro]);

  if (loading && !data) return <Cargando />;
  if (error && !data) return <Alerta>{error}</Alerta>;
  if (!data) return null;

  const trasladable = (e: Equipo) => puedeMover && !NO_TRASLADABLES.has(e.estado);

  const onDragStart = (ev: DragEvent, e: Equipo) => {
    ev.dataTransfer.setData('text/plain', String(e.id));
    ev.dataTransfer.effectAllowed = 'move';
    setArrastrando(e.id);
  };

  const onDrop = (ev: DragEvent, destino: Item) => {
    ev.preventDefault();
    setSobre(null);
    setArrastrando(null);
    const id = Number(ev.dataTransfer.getData('text/plain'));
    const equipo = data.equipos.find(x => x.id === id);
    if (!equipo || equipo.idServicioActual === destino.id) return;
    setErrorMov(null);
    setMov({ equipo, destino });
  };

  const confirmar = async () => {
    if (!mov) return;
    setMoviendo(true);
    setErrorMov(null);
    try {
      const actualizado = await api.put<Equipo>(`/equipos/${mov.equipo.id}/ubicacion`, {
        idServicioDestino: mov.destino.id,
        version: mov.equipo.version,
      });
      setData(d => d && { ...d, equipos: d.equipos.map(e => e.id === actualizado.id ? actualizado : e) });
      toast(`${actualizado.codigoPatrimonial} trasladado a ${mov.destino.nombre}`);
      setResaltado(actualizado.id);
      setMov(null);
    } catch (e) {
      if (e instanceof ApiError && e.esConflicto) {
        const id = mov.equipo.id;
        try {
          const fresco = await api.get<Equipo[]>(`/equipos?incluirBajas=true&q=${encodeURIComponent(mov.equipo.codigoPatrimonial)}`);
          const eq = fresco.find(x => x.id === id);
          setData(d => d && {
            ...d,
            equipos: eq && eq.estado !== 'BAJA_PATRIMONIAL'
              ? d.equipos.map(x => x.id === id ? eq : x)
              : d.equipos.filter(x => x.id !== id),
          });
        } catch { await reload(); }
        setResaltado(id);
        setMov(null);
        toast(e.esConcurrencia
          ? 'Otro usuario modificó este equipo. Se recargó la tarjeta con los datos actuales.'
          : e.message, 'bad');
      } else {
        setErrorMov(errMsg(e));
      }
    } finally {
      setMoviendo(false);
    }
  };

  return (
    <div>
      <div className="cabecera">
        <div>
          <h1>Tablero de ubicaciones</h1>
          <p className="sub">
            {puedeMover
              ? 'Arrastrá un equipo a otro servicio para trasladarlo. Cada traslado queda en el historial.'
              : 'Vista de solo lectura de la ubicación actual de los equipos.'}
          </p>
        </div>
        <div className="acciones">
          <input className="buscador" placeholder="Buscar código, modelo, serie…" value={filtro} onChange={e => setFiltro(e.target.value)} />
          <button className="btn" onClick={reload} disabled={loading}>{loading ? 'Actualizando…' : 'Actualizar'}</button>
        </div>
      </div>

      <div className="kanban">
        {columnas.map(({ servicio, equipos }) => (
          <section
            key={servicio.id}
            className={`kanban-col ${sobre === servicio.id ? 'sobre' : ''}`}
            onDragOver={ev => { if (arrastrando != null) { ev.preventDefault(); setSobre(servicio.id); } }}
            onDragLeave={ev => { if (!ev.currentTarget.contains(ev.relatedTarget as Node)) setSobre(null); }}
            onDrop={ev => onDrop(ev, servicio)}
          >
            <header className="kanban-col-cab">
              <span>{servicio.nombre}</span>
              <span className="contador">{equipos.length}</span>
            </header>
            <div className="kanban-tarjetas">
              {equipos.length === 0 && <div className="kanban-vacio">Sin equipos</div>}
              {equipos.map(e => (
                <article
                  key={e.id}
                  className={`tarjeta tarjeta-${tonoEquipo[e.estado]} ${arrastrando === e.id ? 'arrastrando' : ''} ${resaltado === e.id ? 'resaltada' : ''} ${trasladable(e) ? 'movible' : ''}`}
                  draggable={trasladable(e)}
                  onDragStart={ev => onDragStart(ev, e)}
                  onDragEnd={() => { setArrastrando(null); setSobre(null); }}
                  onAnimationEnd={() => setResaltado(null)}
                  title={!trasladable(e) && puedeMover ? `No se puede trasladar: ${label(e.estado)}` : undefined}
                >
                  <div className="tarjeta-sup">
                    <Link to={`/equipos/${e.id}`} className="codigo">{e.codigoPatrimonial}</Link>
                    <EstadoBadge valor={e.estado} tonos={tonoEquipo} />
                  </div>
                  <div className="tarjeta-titulo">{e.familia}</div>
                  <div className="tarjeta-meta">{e.marca} · {e.modelo}</div>
                  <div className="tarjeta-pie">
                    {e.preventivoProximo && <span className="chip chip-warn">Preventivo</span>}
                    {e.enGarantia && <span className="chip">Garantía</span>}
                    {trasladable(e) && (
                      <button className="btn btn-sm btn-ghost mover-movil" onClick={() => setElegirDestino(e)}>Mover…</button>
                    )}
                  </div>
                </article>
              ))}
            </div>
          </section>
        ))}
      </div>

      {elegirDestino && (
        <Modal titulo={`Mover ${elegirDestino.codigoPatrimonial}`} onClose={() => setElegirDestino(null)}>
          <div className="lista-opciones">
            {data.servicios.filter(s => s.id !== elegirDestino.idServicioActual).map(s => (
              <button key={s.id} className="btn btn-bloque" onClick={() => { setMov({ equipo: elegirDestino, destino: s }); setElegirDestino(null); setErrorMov(null); }}>
                {s.nombre}
              </button>
            ))}
          </div>
        </Modal>
      )}

      {mov && (
        <Modal
          titulo="Confirmar traslado"
          onClose={() => !moviendo && setMov(null)}
          pie={<>
            <button className="btn" onClick={() => setMov(null)} disabled={moviendo}>Cancelar</button>
            <button className="btn btn-primario" onClick={confirmar} disabled={moviendo}>{moviendo ? 'Trasladando…' : 'Confirmar traslado'}</button>
          </>}
        >
          <p>¿Trasladar el equipo <strong>{mov.equipo.codigoPatrimonial}</strong> ({mov.equipo.familia} {mov.equipo.marca} {mov.equipo.modelo})?</p>
          <div className="traslado">
            <div className="traslado-caja">{mov.equipo.servicioActual}</div>
            <span className="traslado-flecha">→</span>
            <div className="traslado-caja traslado-destino">{mov.destino.nombre}</div>
          </div>
          <p className="sub">El código patrimonial no cambia: queda congelado con el servicio de alta.</p>
          <Alerta>{errorMov}</Alerta>
        </Modal>
      )}
    </div>
  );
}
