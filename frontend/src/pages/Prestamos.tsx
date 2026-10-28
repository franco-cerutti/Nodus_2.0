import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import L from 'leaflet';
import { CircleMarker, MapContainer, Marker, Popup, TileLayer, Tooltip, useMap, useMapEvents } from 'react-leaflet';
import { distanciaMetros, fDistancia, geocodificar, type ResultadoGeo } from '../geo';
import { api, qs } from '../api/client';
import type { Equipo, HospitalExterno, Prestamo, TipoDireccion } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { Alerta, Campo, Cargando, EstadoBadge, Modal, Vacio, useToast } from '../components/ui';
import { errMsg, fDate, hoyISO, label, sumarDias, tonoPrestamo, useLoad } from '../util';

const CENTRO: [number, number] = [-31.4135, -64.1811];

type Modo = { tipo: 'nuevo'; direccion: TipoDireccion } | { tipo: 'prorroga' | 'finalizar'; p: Prestamo }
  | { tipo: 'hospital'; hospital?: HospitalExterno; lat?: number; lng?: number } | null;

export default function Prestamos() {
  const { tiene } = useAuth();
  const esBio = tiene('Bioingeniero');
  const toast = useToast();
  const [f, setF] = useState({ estado: '', tipo: '' });
  const prestamos = useLoad(() => api.get<Prestamo[]>(`/prestamos${qs(f)}`), [f.estado, f.tipo]);
  const hospitales = useLoad(() => api.get<HospitalExterno[]>('/hospitales-externos'));
  const [modo, setModo] = useState<Modo>(null);
  const [mapa, setMapa] = useState<L.Map | null>(null);
  const marcadores = useRef<Record<number, L.CircleMarker | null>>({});
  const [buscarHosp, setBuscarHosp] = useState('');

  /** Centra el mapa en un hospital y abre su ficha */
  const verEnMapa = (h: HospitalExterno) => {
    mapa?.flyTo([h.latitud, h.longitud], 16, { duration: 0.8 });
    setTimeout(() => marcadores.current[h.id]?.openPopup(), 850);
    mapa?.getContainer().scrollIntoView({ behavior: 'smooth', block: 'center' });
  };
  const listaHosp = (hospitales.data ?? []).filter(h =>
    !buscarHosp || `${h.nombre} ${h.direccion ?? ''}`.toLowerCase().includes(buscarHosp.toLowerCase()));

  const recargar = () => { prestamos.reload(); hospitales.reload(); };
  const pdf = (p: Prestamo, doc: 'remito' | 'nota') =>
    api.download(`/prestamos/${p.id}/${doc}`, `${doc}-prestamo-${p.id}.pdf`).catch(e => toast(errMsg(e), 'bad'));

  return (
    <div>
      <div className="cabecera">
        <div>
          <h1>Préstamos interinstitucionales</h1>
          <p className="sub">Equipos otorgados a otras instituciones y equipos recibidos en préstamo.</p>
        </div>
        {esBio && (
          <div className="acciones">
            <button className="btn btn-primario" onClick={() => setModo({ tipo: 'nuevo', direccion: 'OTORGADO' })}>+ Prestar equipo</button>
            <button className="btn" onClick={() => setModo({ tipo: 'nuevo', direccion: 'RECIBIDO' })}>+ Registrar recibido</button>
          </div>
        )}
      </div>

      <section className="panel panel-mapa">
        <div className="panel-cab">
          <h2>Mapa de nosocomios</h2>
          {esBio && <span className="sub">Clic en el mapa para agregar un hospital en ese punto.</span>}
        </div>
        <div className="mapa-layout">
          <MapContainer center={CENTRO} zoom={12} className="mapa" scrollWheelZoom={false} ref={setMapa}>
            <TileLayer attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>' url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
            <CircleMarker center={CENTRO} radius={10} pathOptions={{ color: '#14324f', fillColor: '#40c4aa', fillOpacity: 1, weight: 3 }}>
              <Tooltip permanent direction="top" offset={[0, -10]}>HMN</Tooltip>
            </CircleMarker>
            {hospitales.data?.map(h => (
              <CircleMarker key={h.id} center={[h.latitud, h.longitud]} radius={h.prestamosActivos ? 9 : 6}
                ref={el => { marcadores.current[h.id] = el; }}
                pathOptions={{ color: h.prestamosActivos ? '#b45309' : '#475569', fillColor: h.prestamosActivos ? '#f59e0b' : '#94a3b8', fillOpacity: 0.9, weight: 2 }}>
                <Popup>
                  <strong>{h.nombre}</strong><br />
                  {h.direccion && <>{h.direccion}<br /></>}
                  {h.prestamosActivos} préstamo(s) activo(s)
                  {esBio && <><br /><button type="button" className="link" onClick={() => setModo({ tipo: 'hospital', hospital: h })}>Ajustar ubicación</button></>}
                </Popup>
              </CircleMarker>
            ))}
            {esBio && <ClickParaAgregar onClick={(lat, lng) => setModo({ tipo: 'hospital', lat, lng })} />}
          </MapContainer>

          <aside className="hosp-lista">
            <div className="hosp-lista-cab">
              <input placeholder="Buscar hospital…" value={buscarHosp} onChange={e => setBuscarHosp(e.target.value)} />
              {esBio && <button className="btn btn-sm" onClick={() => setModo({ tipo: 'hospital' })} title="Agregar hospital">+</button>}
            </div>
            {hospitales.loading && !hospitales.data ? <Cargando /> : listaHosp.length === 0 ? <div className="sub hosp-vacio">Sin hospitales cargados.</div> : (
              <ul>
                {listaHosp.map(h => (
                  <li key={h.id}>
                    <button className="hosp-item" onClick={() => verEnMapa(h)} title="Ver en el mapa">
                      <span className={`hosp-punto ${h.prestamosActivos ? 'activo' : ''}`} />
                      <span className="hosp-txt">
                        <strong>{h.nombre}</strong>
                        <span className="sub">{h.direccion || 'Sin dirección'}{h.prestamosActivos ? ` · ${h.prestamosActivos} activo(s)` : ''}</span>
                      </span>
                    </button>
                    {esBio && <button className="btn btn-sm btn-ghost" onClick={() => setModo({ tipo: 'hospital', hospital: h })} title="Editar y ajustar ubicación">Editar</button>}
                  </li>
                ))}
              </ul>
            )}
          </aside>
        </div>
      </section>

      <div className="filtros">
        <select value={f.tipo} onChange={e => setF({ ...f, tipo: e.target.value })}>
          <option value="">Otorgados y recibidos</option>
          <option value="OTORGADO">Otorgados</option>
          <option value="RECIBIDO">Recibidos</option>
        </select>
        <select value={f.estado} onChange={e => setF({ ...f, estado: e.target.value })}>
          <option value="">Todos los estados</option>
          {['EN_PRESTAMO', 'PRORROGA', 'VENCIDO', 'FINALIZADO'].map(s => <option key={s} value={s}>{label(s)}</option>)}
        </select>
      </div>

      <Alerta>{prestamos.error}</Alerta>
      {prestamos.loading && !prestamos.data ? <Cargando /> : prestamos.data?.length === 0 ? <Vacio>Sin préstamos.</Vacio> : (
        <div className="panel panel-tabla"><div className="tabla-scroll">
          <table className="tabla">
            <thead><tr><th>N°</th><th>Tipo</th><th>Equipo</th><th>Institución</th><th>Salida</th><th>Devolución</th><th>Estado</th><th /></tr></thead>
            <tbody>{prestamos.data?.map(p => (
              <tr key={p.id}>
                <td>P-{String(p.id).padStart(5, '0')}</td>
                <td>{p.tipo === 'OTORGADO' ? '↗ Otorgado' : '↙ Recibido'}</td>
                <td>{p.codigoPatrimonial && <span className="codigo">{p.codigoPatrimonial}</span>}<div className="sub">{p.descripcionEquipo}</div></td>
                <td>{p.hospital}<div className="sub">Resp.: {p.responsable}</div></td>
                <td>{fDate(p.fechaSalida)}</td>
                <td>{p.fechaDevolucionReal ? fDate(p.fechaDevolucionReal) : <span className={p.estado === 'VENCIDO' ? 'txt-bad' : ''}>{fDate(p.fechaDevolucionEstimada)} <span className="sub">(est.)</span></span>}</td>
                <td><EstadoBadge valor={p.estado} tonos={tonoPrestamo} /></td>
                <td className="alinear-der acciones-fila">
                  <button className="btn btn-sm btn-ghost" onClick={() => pdf(p, 'remito')}>Remito</button>
                  <button className="btn btn-sm btn-ghost" onClick={() => pdf(p, 'nota')}>Nota</button>
                  {esBio && p.estado !== 'FINALIZADO' && <>
                    <button className="btn btn-sm" onClick={() => setModo({ tipo: 'prorroga', p })}>Prórroga</button>
                    <button className="btn btn-sm btn-primario" onClick={() => setModo({ tipo: 'finalizar', p })}>Finalizar</button>
                  </>}
                </td>
              </tr>
            ))}</tbody>
          </table>
        </div></div>
      )}

      {modo?.tipo === 'nuevo' && <NuevoPrestamo direccion={modo.direccion} hospitales={hospitales.data ?? []}
        onClose={() => setModo(null)} onOk={() => { setModo(null); recargar(); toast('Préstamo registrado'); }}
        onNuevoHospital={() => setModo({ tipo: 'hospital' })} />}
      {modo?.tipo === 'prorroga' && <Prorroga p={modo.p} onClose={() => setModo(null)} onOk={() => { setModo(null); recargar(); toast('Prórroga registrada'); }} />}
      {modo?.tipo === 'finalizar' && <Finalizar p={modo.p} onClose={() => setModo(null)} onOk={() => { setModo(null); recargar(); toast('Préstamo finalizado'); }} />}
      {modo?.tipo === 'hospital' && <HospitalModal hospital={modo.hospital} lat={modo.lat} lng={modo.lng} onClose={() => setModo(null)}
        onOk={h => {
          setModo(null); hospitales.reload();
          toast(modo.hospital ? 'Hospital actualizado' : 'Hospital agregado');
          mapa?.flyTo([h.latitud, h.longitud], 15, { duration: 0.8 });
        }} />}
    </div>
  );
}

function ClickParaAgregar({ onClick }: { onClick: (lat: number, lng: number) => void }) {
  useMapEvents({ click: e => onClick(Number(e.latlng.lat.toFixed(6)), Number(e.latlng.lng.toFixed(6))) });
  return null;
}

function useEnvio(fn: () => Promise<unknown>, onOk: () => void) {
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  const enviar = async (e: FormEvent) => {
    e.preventDefault();
    setEnviando(true); setError(null);
    try { await fn(); onOk(); } catch (ex) { setError(errMsg(ex)); } finally { setEnviando(false); }
  };
  return { error, enviando, enviar };
}

function Pie({ onClose, enviando, texto }: { onClose: () => void; enviando: boolean; texto: string }) {
  return (
    <div className="campo-full modal-pie-inline">
      <button type="button" className="btn" onClick={onClose}>Cancelar</button>
      <button className="btn btn-primario" disabled={enviando}>{enviando ? 'Guardando…' : texto}</button>
    </div>
  );
}

function NuevoPrestamo({ direccion, hospitales, onClose, onOk, onNuevoHospital }: {
  direccion: TipoDireccion; hospitales: HospitalExterno[]; onClose: () => void; onOk: () => void; onNuevoHospital: () => void;
}) {
  const otorgado = direccion === 'OTORGADO';
  const equipos = useLoad(() => otorgado ? api.get<Equipo[]>('/equipos?estado=OPERATIVO') : Promise.resolve([] as Equipo[]));
  const [f, setF] = useState({
    idEquipo: '', marcaExterna: '', modeloExterno: '', nroSerieExterno: '', idHospitalExterno: '',
    responsable: '', fechaSalida: hoyISO(), fechaDevolucionEstimada: sumarDias(hoyISO(), 15), estadoTecnicoEntrega: '', accesorios: '',
  });
  const set = (k: keyof typeof f) => (e: { target: { value: string } }) => setF({ ...f, [k]: e.target.value });
  const { error, enviando, enviar } = useEnvio(() => otorgado
    ? api.post('/prestamos/otorgados', { ...f, idEquipo: Number(f.idEquipo), idHospitalExterno: Number(f.idHospitalExterno), accesorios: f.accesorios || null })
    : api.post('/prestamos/recibidos', { ...f, idHospitalExterno: Number(f.idHospitalExterno), nroSerieExterno: f.nroSerieExterno || null, accesorios: f.accesorios || null }),
    onOk);

  return (
    <Modal titulo={otorgado ? 'Prestar equipo a otra institución' : 'Registrar equipo recibido en préstamo'} onClose={onClose} ancho={680}>
      <form onSubmit={enviar} className="form-grid">
        {otorgado ? (
          <Campo label="Equipo (solo operativos)" ancho="full">
            <select required value={f.idEquipo} onChange={set('idEquipo')}>
              <option value="">{equipos.loading ? 'Cargando…' : 'Seleccionar…'}</option>
              {equipos.data?.map(e => <option key={e.id} value={e.id}>{e.codigoPatrimonial} — {e.familia} {e.marca} {e.modelo} ({e.servicioActual})</option>)}
            </select>
          </Campo>
        ) : <>
          <Campo label="Marca"><input required maxLength={100} value={f.marcaExterna} onChange={set('marcaExterna')} /></Campo>
          <Campo label="Modelo"><input required maxLength={80} value={f.modeloExterno} onChange={set('modeloExterno')} /></Campo>
          <Campo label="N° de serie"><input maxLength={80} value={f.nroSerieExterno} onChange={set('nroSerieExterno')} /></Campo>
          <p className="sub">El equipo externo no ingresa al inventario patrimonial.</p>
        </>}
        <Campo label={otorgado ? 'Institución receptora' : 'Institución prestadora'} ancho="full"
          ayuda={<button type="button" className="link" onClick={onNuevoHospital}>¿No está en la lista? Agregar hospital</button>}>
          <select required value={f.idHospitalExterno} onChange={set('idHospitalExterno')}>
            <option value="">Seleccionar…</option>
            {hospitales.map(h => <option key={h.id} value={h.id}>{h.nombre}</option>)}
          </select>
        </Campo>
        <Campo label="Responsable de retiro / entrega"><input required maxLength={100} value={f.responsable} onChange={set('responsable')} /></Campo>
        <Campo label="Fecha de salida"><input type="date" required value={f.fechaSalida} onChange={set('fechaSalida')} /></Campo>
        <Campo label="Devolución estimada"><input type="date" required min={f.fechaSalida} value={f.fechaDevolucionEstimada} onChange={set('fechaDevolucionEstimada')} /></Campo>
        <Campo label="Accesorios"><input value={f.accesorios} onChange={set('accesorios')} placeholder="Circuito, humidificador, sensor de flujo…" /></Campo>
        <Campo label="Estado técnico a la entrega" ancho="full"><textarea required rows={2} value={f.estadoTecnicoEntrega} onChange={set('estadoTecnicoEntrega')} /></Campo>
        <div className="campo-full"><Alerta>{error}</Alerta></div>
        <Pie onClose={onClose} enviando={enviando} texto="Registrar préstamo" />
      </form>
    </Modal>
  );
}

function Prorroga({ p, onClose, onOk }: { p: Prestamo; onClose: () => void; onOk: () => void }) {
  const [fecha, setFecha] = useState(sumarDias(p.fechaDevolucionEstimada > hoyISO() ? p.fechaDevolucionEstimada : hoyISO(), 15));
  const { error, enviando, enviar } = useEnvio(() => api.put(`/prestamos/${p.id}/prorroga`, { nuevaFechaDevolucion: fecha }), onOk);
  return (
    <Modal titulo={`Prórroga — P-${String(p.id).padStart(5, '0')}`} onClose={onClose}>
      <form onSubmit={enviar} className="form-grid">
        <p className="campo-full">Devolución estimada actual: <strong>{fDate(p.fechaDevolucionEstimada)}</strong></p>
        <Campo label="Nueva fecha de devolución"><input type="date" required value={fecha} min={sumarDias(p.fechaDevolucionEstimada, 1)} onChange={e => setFecha(e.target.value)} /></Campo>
        <div className="campo-full"><Alerta>{error}</Alerta></div>
        <Pie onClose={onClose} enviando={enviando} texto="Registrar prórroga" />
      </form>
    </Modal>
  );
}

function Finalizar({ p, onClose, onOk }: { p: Prestamo; onClose: () => void; onOk: () => void }) {
  const [f, setF] = useState({ fechaDevolucionReal: hoyISO(), estadoTecnicoRecepcion: '' });
  const { error, enviando, enviar } = useEnvio(() => api.put(`/prestamos/${p.id}/finalizar`, f), onOk);
  return (
    <Modal titulo={`Finalizar préstamo — P-${String(p.id).padStart(5, '0')}`} onClose={onClose}>
      <form onSubmit={enviar} className="form-grid">
        <p className="campo-full sub">{p.descripcionEquipo} · {p.hospital}</p>
        <Campo label="Fecha de devolución real"><input type="date" required min={p.fechaSalida} max={hoyISO()} value={f.fechaDevolucionReal} onChange={e => setF({ ...f, fechaDevolucionReal: e.target.value })} /></Campo>
        <Campo label="Estado técnico a la recepción" ancho="full"><textarea required rows={3} value={f.estadoTecnicoRecepcion} onChange={e => setF({ ...f, estadoTecnicoRecepcion: e.target.value })} /></Campo>
        {p.tipo === 'OTORGADO' && <p className="campo-full sub">El equipo vuelve a estado operativo.</p>}
        <div className="campo-full"><Alerta>{error}</Alerta></div>
        <Pie onClose={onClose} enviando={enviando} texto="Finalizar" />
      </form>
    </Modal>
  );
}

const iconoPin = L.divIcon({
  className: 'pin-ajuste',
  html: '<span class="pin-ajuste-cabeza"></span>',
  iconSize: [26, 34],
  iconAnchor: [13, 34],
});

function Recentrar({ pos }: { pos: [number, number] }) {
  const map = useMap();
  useEffect(() => { map.setView(pos, Math.max(map.getZoom(), 16), { animate: true }); }, [map, pos]);
  return null;
}

function ClickMueve({ onClick }: { onClick: (lat: number, lng: number) => void }) {
  useMapEvents({ click: e => onClick(Number(e.latlng.lat.toFixed(6)), Number(e.latlng.lng.toFixed(6))) });
  return null;
}

/** Alta y edición de hospital externo con geocodificación y ajuste manual de la ubicación. */
function HospitalModal({ hospital, lat, lng, onClose, onOk }: {
  hospital?: HospitalExterno; lat?: number; lng?: number; onClose: () => void; onOk: (h: HospitalExterno) => void;
}) {
  const [f, setF] = useState({
    nombre: hospital?.nombre ?? '',
    direccion: hospital?.direccion ?? '',
    latitud: hospital?.latitud ?? lat ?? CENTRO[0],
    longitud: hospital?.longitud ?? lng ?? CENTRO[1],
  });
  const original = useMemo<[number, number]>(() => [f.latitud, f.longitud], []);
  const pos = useMemo<[number, number]>(() => [f.latitud, f.longitud], [f.latitud, f.longitud]);
  const [buscando, setBuscando] = useState(false);
  const [resultados, setResultados] = useState<ResultadoGeo[] | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [errorGeo, setErrorGeo] = useState<string | null>(null);

  const { error, enviando, enviar } = useEnvio(async () => {
    const body = { ...f, direccion: f.direccion.trim() || null };
    const h = hospital
      ? await api.put<HospitalExterno>(`/hospitales-externos/${hospital.id}`, body)
      : await api.post<HospitalExterno>('/hospitales-externos', body);
    onOk(h);
  }, () => {});

  const mover = (la: number, ln: number, motivo?: string) => {
    const d = distanciaMetros([f.latitud, f.longitud], [la, ln]);
    setF(x => ({ ...x, latitud: la, longitud: ln }));
    if (motivo) setAviso(`${motivo} — el marcador se movió ${fDistancia(d)}.`);
  };

  const buscar = async () => {
    setBuscando(true); setErrorGeo(null); setResultados(null); setAviso(null);
    try {
      const r = await geocodificar(f.direccion);
      if (r.length === 0) setErrorGeo('No se encontró la dirección. Probá agregando la localidad o ubicá el punto a mano en el mapa.');
      else if (r.length === 1) mover(r[0].lat, r[0].lng, 'Ubicado según la dirección');
      else setResultados(r);
    } catch (e) {
      setErrorGeo(errMsg(e));
    } finally {
      setBuscando(false);
    }
  };

  const desplazamiento = distanciaMetros(original, pos);

  return (
    <Modal titulo={hospital ? `Editar — ${hospital.nombre}` : 'Nuevo hospital externo'} onClose={onClose} ancho={720}>
      <form onSubmit={enviar} className="form-grid">
        <Campo label="Nombre" ancho="full"><input required maxLength={150} value={f.nombre} onChange={e => setF({ ...f, nombre: e.target.value })} /></Campo>
        <Campo label="Dirección" ancho="full" ayuda="Calle y número, ciudad. Ej.: Ferroviarios 1250, Córdoba">
          <div className="input-con-boton">
            <input maxLength={200} value={f.direccion} onChange={e => setF({ ...f, direccion: e.target.value })}
              onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); if (f.direccion.trim()) buscar(); } }} />
            <button type="button" className="btn btn-primario" onClick={buscar} disabled={buscando || f.direccion.trim().length < 4}>
              <i className="fa-solid fa-location-crosshairs" /> {buscando ? 'Buscando…' : 'Ubicar dirección'}
            </button>
          </div>
        </Campo>

        {resultados && (
          <div className="campo-full geo-resultados">
            <div className="sub">Se encontraron varias coincidencias. Elegí la correcta:</div>
            {resultados.map((r, i) => (
              <button type="button" key={i} className="geo-opcion" onClick={() => { mover(r.lat, r.lng, 'Ubicado según la dirección'); setResultados(null); }}>
                <i className="fa-solid fa-location-dot" /> {r.nombre}
              </button>
            ))}
          </div>
        )}
        {errorGeo && <div className="campo-full"><Alerta tono="warn">{errorGeo}</Alerta></div>}

        <div className="campo-full">
          <MapContainer center={pos} zoom={hospital || lat ? 16 : 12} className="mapa-mini" scrollWheelZoom>
            <TileLayer attribution='&copy; OpenStreetMap' url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
            <Marker position={pos} draggable icon={iconoPin}
              eventHandlers={{ dragend: e => { const p = (e.target as L.Marker).getLatLng(); mover(Number(p.lat.toFixed(6)), Number(p.lng.toFixed(6)), 'Ajuste manual'); } }} />
            <ClickMueve onClick={(la, ln) => mover(la, ln, 'Ajuste manual')} />
            <Recentrar pos={pos} />
          </MapContainer>
          <div className="mapa-mini-pie">
            <span className="sub"><i className="fa-solid fa-hand-pointer" /> Arrastrá el marcador o hacé clic en el mapa para afinar la ubicación (ej.: la puerta de ingreso).</span>
            <span className="mono sub">{f.latitud.toFixed(6)}, {f.longitud.toFixed(6)}</span>
          </div>
          {aviso && <Alerta tono="info">{aviso}</Alerta>}
          {hospital && desplazamiento > 1 && (
            <div className="sub">Cambio respecto de la ubicación guardada: <strong>{fDistancia(desplazamiento)}</strong>.</div>
          )}
        </div>

        <div className="campo-full"><Alerta>{error}</Alerta></div>
        <Pie onClose={onClose} enviando={enviando} texto={hospital ? 'Guardar cambios' : 'Agregar'} />
      </form>
    </Modal>
  );
}
