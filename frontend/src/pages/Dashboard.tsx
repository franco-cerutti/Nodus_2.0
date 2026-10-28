import { useEffect, useState } from 'react';
import { api, qs } from '../api/client';
import type { Catalogos, Dashboard as D } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { Alerta, Cargando, Modal, useToast } from '../components/ui';
import { errMsg, fDate, hoyISO, sumarDias, useLoad } from '../util';
import {
  Bloque, ConsumoRecursos, DisponibilidadPorTipo, DonaAreas, FallasPorMarca, GrillaServicios, MapaDeCalor, MttrChart,
  OtsBloqueadas, ParqueChart, Tarjeta, TareasDelDia,
} from './dashboard/Bloques';
import './dashboard/charts';
import Logo from '../components/Logo';

const MESES = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];

type Preset = 'hoy' | '7d' | '30d' | 'mes' | 'anio' | null;
const PRESETS: { id: Exclude<Preset, null>; texto: string }[] = [
  { id: 'hoy', texto: 'Hoy' }, { id: '7d', texto: 'Últimos 7 días' }, { id: '30d', texto: 'Últimos 30 días' },
  { id: 'mes', texto: 'Este mes' }, { id: 'anio', texto: 'Este año' },
];
function rango(p: Exclude<Preset, null>): [string, string] {
  const hoy = hoyISO();
  switch (p) {
    case 'hoy': return [hoy, hoy];
    case '7d': return [sumarDias(hoy, -6), hoy];
    case '30d': return [sumarDias(hoy, -29), hoy];
    case 'mes': return [`${hoy.slice(0, 8)}01`, hoy];
    case 'anio': return [`${hoy.slice(0, 4)}-01-01`, hoy];
  }
}

const BLOQUES = [
  { id: 'kpis', texto: 'Alertas y KPIs críticos' },
  { id: 'operativo', texto: 'Bloque operativo' },
  { id: 'tactico', texto: 'Bloque táctico (MTTR)' },
  { id: 'estrategico', texto: 'Bloque estratégico' },
  { id: 'analitico', texto: 'Bloque analítico (mapa de calor)' },
];

export default function Dashboard() {
  const { usuario } = useAuth();
  const [preset, setPreset] = useState<Preset>('anio');
  const [[desde, hasta], setRango] = useState<[string, string]>(rango('anio'));
  const [servicio, setServicio] = useState('');
  const [familia, setFamilia] = useState('');
  const [exportar, setExportar] = useState(false);
  const [ocultosImpresion, setOcultosImpresion] = useState<Set<string>>(new Set());

  const cat = useLoad(() => api.get<Catalogos>('/catalogos'));
  const { data: d, error, loading, reload } = useLoad(
    () => api.get<D>(`/reportes/dashboard${qs({ desde, hasta, servicio, familia })}`), [desde, hasta, servicio, familia]);

  useEffect(() => {
    const t = setInterval(() => { if (document.visibilityState === 'visible') reload(); }, 120_000);
    return () => clearInterval(t);
  }, [reload]);

  const nombreServicio = cat.data?.servicios.find(s => String(s.id) === servicio)?.nombre;
  const nombreFamilia = cat.data?.familias.find(s => String(s.id) === familia)?.nombre;
  const filtrosTexto = [`${fDate(desde)} al ${fDate(hasta)}`, nombreServicio ?? 'Todas las áreas', nombreFamilia ?? 'Todos los tipos'].join(' · ');

  const elegirPreset = (p: Exclude<Preset, null>) => { setPreset(p); setRango(rango(p)); };
  const cambiarFecha = (cual: 0 | 1, v: string) => {
    if (!v) return;
    setPreset(null);
    setRango(r => {
      const n: [string, string] = cual === 0 ? [v, r[1]] : [r[0], v];
      return n[0] > n[1] ? [v, v] : n;
    });
  };

  const critico = !!d && d.kpis.disponibilidad < d.umbral;
  const tiposCriticos = d?.porTipo.filter(t => t.semaforo === 'CRITICO') ?? [];

  return (
    <div className="dash">
      <Membrete filtros={filtrosTexto} usuario={usuario?.nombreCompleto ?? ''} />

      <header className="dash-cab no-print">
        <div>
          <h1>Tablero de control</h1>
          <p className="sub">
            Soporte ventilatorio · Bioingeniería HMN
            {d && <> · actualizado {new Date(d.generadoUtc).toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit' })}</>}
          </p>
        </div>
        <div className="acciones">
          <button className="btn" onClick={reload} disabled={loading}><i className={`fa-solid fa-rotate ${loading ? 'fa-spin' : ''}`} /> Actualizar</button>
          <button className="btn btn-primario" onClick={() => setExportar(true)} disabled={!d}><i className="fa-solid fa-file-export" /> Exportar reportes</button>
        </div>
      </header>

      <div className="dash-filtros no-print">
        <div className="filtro-grupo">
          <label><span>Fecha inicio</span><input type="date" value={desde} max={hasta} onChange={e => cambiarFecha(0, e.target.value)} /></label>
          <label><span>Fecha fin</span><input type="date" value={hasta} min={desde} max={hoyISO()} onChange={e => cambiarFecha(1, e.target.value)} /></label>
        </div>
        <div className="presets" role="group" aria-label="Rangos rápidos">
          {PRESETS.map(p => (
            <button key={p.id} className={preset === p.id ? 'activo' : ''} onClick={() => elegirPreset(p.id)}>{p.texto}</button>
          ))}
        </div>
        <div className="filtro-grupo">
          <label><span>Servicio / Área</span>
            <select value={servicio} onChange={e => setServicio(e.target.value)}>
              <option value="">Todas las áreas</option>
              {cat.data?.servicios.map(s => <option key={s.id} value={s.id}>{s.nombre}</option>)}
            </select>
          </label>
          <label><span>Tipo de activo</span>
            <select value={familia} onChange={e => setFamilia(e.target.value)}>
              <option value="">Todos los tipos</option>
              {cat.data?.familias.map(s => <option key={s.id} value={s.id}>{s.nombre}</option>)}
            </select>
          </label>
          {(servicio || familia) && (
            <button className="btn btn-sm btn-ghost limpiar" onClick={() => { setServicio(''); setFamilia(''); }}>
              <i className="fa-solid fa-xmark" /> Limpiar
            </button>
          )}
        </div>
      </div>

      {error && <Alerta>{error}</Alerta>}
      {!d ? <Cargando /> : (
        <>
          <section className={`dash-kpis-bloque ${ocultosImpresion.has('kpis') ? 'print-oculto' : ''}`}>
            {critico && (
              <div className="banner-critico" role="alert">
                <div className="banner-icono"><i className="fa-solid fa-triangle-exclamation" /></div>
                <div className="banner-txt">
                  <strong>ALERTA DE SEGURIDAD CRÍTICA</strong>
                  <span>
                    Disponibilidad operativa de la flota en <b>{d.kpis.disponibilidad.toFixed(1)}%</b>, por debajo del umbral de seguridad del <b>{d.umbral}%</b>.
                    {' '}Respiradores libres para urgencias: <b>{d.kpis.respiradoresLibres}</b>.
                    {tiposCriticos.length > 0 && <> Tipos en estado crítico: {tiposCriticos.map(t => t.familia).join(', ')}.</>}
                  </span>
                </div>
                <div className="banner-accion">Activar protocolo de contingencia y priorizar reparaciones</div>
              </div>
            )}

            <div className="kpi-principales">
              <div className="kpi-p">
                <div className="kpi-p-icono"><i className="fa-solid fa-hospital" /></div>
                <div>
                  <span className="kpi-p-titulo">Total de equipos</span>
                  <strong className="kpi-p-valor">{d.kpis.totalEquipos}</strong>
                  <span className="kpi-p-sub">{d.kpis.operativos} operativos · sin contar bajas</span>
                </div>
              </div>
              <div className={`kpi-p ${d.kpis.semaforo === 'CRITICO' ? 'kpi-rojo' : 'kpi-verde'}`}>
                <div className="kpi-p-icono"><i className="fa-solid fa-heart-pulse" /></div>
                <div className="kpi-p-cuerpo">
                  <span className="kpi-p-titulo">Disponibilidad general</span>
                  <strong className="kpi-p-valor">{d.kpis.disponibilidad.toFixed(1)}%</strong>
                  <div className="kpi-barra">
                    <div className="kpi-barra-relleno" style={{ width: `${d.kpis.disponibilidad}%` }} />
                    <div className="kpi-barra-umbral" style={{ left: `${d.umbral}%` }} title={`Umbral ${d.umbral}%`} />
                  </div>
                  <span className="kpi-p-sub">Umbral crítico {d.umbral}%</span>
                </div>
              </div>
              <div className={`kpi-p ${d.kpis.respiradoresLibres === 0 ? 'kpi-rojo' : ''}`}>
                <div className="kpi-p-icono"><i className="fa-solid fa-lungs" /></div>
                <div>
                  <span className="kpi-p-titulo">Respiradores libres para urgencias <span className="en-vivo"><span className="pulso" />en vivo</span></span>
                  <strong className="kpi-p-valor">{d.kpis.respiradoresLibres}<small> / {d.kpis.respiradoresTotal}</small></strong>
                  <span className="kpi-p-sub">Operativos y disponibles en el área filtrada</span>
                </div>
              </div>
            </div>
            <div className="kpi-secundarios">
              <span><i className="fa-solid fa-clipboard-list" /> OT abiertas <b>{d.kpis.otsAbiertas}</b></span>
              <span className={d.kpis.reportesPendientes ? 'atencion' : ''}><i className="fa-solid fa-bell" /> Reportes de falla pendientes <b>{d.kpis.reportesPendientes}</b></span>
              <span><i className="fa-solid fa-circle-exclamation" /> Alertas abiertas <b>{d.kpis.alertasAbiertas}</b></span>
            </div>
          </section>

          <Bloque id="operativo" titulo="Bloque operativo" icono="fa-stethoscope" oculto={ocultosImpresion.has('operativo')}
            descripcion="Estado de la flota, bloqueos y tareas inmediatas.">
            <div className="dash-grid g-2-1">
              <Tarjeta titulo="Disponibilidad por tipo de equipo" icono="fa-chart-simple" alcance="vivo"
                pie={<><span className="ley ley-verde" /> ≥ {d.umbral}% <span className="ley ley-rojo" /> &lt; {d.umbral}% <span className="ley-linea" /> umbral crítico</>}>
                <DisponibilidadPorTipo d={d} />
              </Tarjeta>
              <Tarjeta titulo="Equipos por área" icono="fa-chart-pie" alcance="vivo">
                <DonaAreas d={d} />
              </Tarjeta>
            </div>
            <Tarjeta titulo="Grilla operativa por servicio" icono="fa-table-cells" alcance="vivo">
              <GrillaServicios d={d} />
            </Tarjeta>
            <div className="dash-grid g-1-1">
              <Tarjeta titulo="OT bloqueadas" icono="fa-ban" alcance="vivo"
                pie="Stock: la base rechazó un consumo (Hard Stop) o el técnico marcó la espera. Seguridad: prueba registrada como no aprobada.">
                <OtsBloqueadas d={d} />
              </Tarjeta>
              <Tarjeta titulo="Fallas por marca" icono="fa-industry" alcance="periodo" pie="Falla = OT correctiva abierta en el período.">
                <FallasPorMarca d={d} />
              </Tarjeta>
            </div>
            <Tarjeta titulo="Grilla operativa de tareas del día" icono="fa-list-check" alcance="vivo">
              <TareasDelDia d={d} />
            </Tarjeta>
          </Bloque>

          <Bloque id="tactico" titulo="Bloque táctico" icono="fa-gauge-high" oculto={ocultosImpresion.has('tactico')}
            descripcion="Desempeño del servicio técnico de Bioingeniería.">
            <Tarjeta titulo="MTTR — Tiempo medio de reparación (días)" icono="fa-stopwatch" alcance="periodo"
              pie="Correctivo: desde el reporte de falla (o la apertura de la OT) hasta el cierre con seguridad eléctrica aprobada. OT cerradas en el período.">
              <MttrChart d={d} />
            </Tarjeta>
          </Bloque>

          <Bloque id="estrategico" titulo="Bloque estratégico" icono="fa-chess-knight" oculto={ocultosImpresion.has('estrategico')}
            descripcion="Envejecimiento del parque tecnológico y consumo de recursos.">
            <div className="dash-grid g-1-1">
              <Tarjeta titulo="Evolución del parque tecnológico" icono="fa-layer-group" alcance="vivo"
                pie={`Equipos en servicio al cierre de cada año. Vida útil recomendada: ${d.parque.vidaUtilAnios} años (parámetro del sistema).`}>
                <ParqueChart d={d} />
              </Tarjeta>
              <Tarjeta titulo="Consumo de recursos (no monetario)" icono="fa-boxes-stacked" alcance="periodo">
                <ConsumoRecursos d={d} />
              </Tarjeta>
            </div>
          </Bloque>

          <Bloque id="analitico" titulo="Bloque analítico" icono="fa-fire" oculto={ocultosImpresion.has('analitico')}
            descripcion="Patrones temporales de fallas: estacionalidad y horarios de mayor demanda.">
            <Tarjeta titulo="Mapa de calor de fallas" icono="fa-calendar-days" alcance="12m">
              <MapaDeCalor d={d} />
            </Tarjeta>
          </Bloque>

          <FirmaImpresion usuario={usuario?.nombreCompleto ?? ''} />
        </>
      )}

      {exportar && d && (
        <Exportar d={d} filtros={filtrosTexto} consulta={qs({ desde, hasta, servicio, familia })} usuario={usuario?.nombreCompleto ?? ''}
          ocultos={ocultosImpresion} setOcultos={setOcultosImpresion} onClose={() => setExportar(false)} />
      )}
    </div>
  );
}

const nroDocumento = () => {
  const n = new Date();
  const p = (x: number) => String(x).padStart(2, '0');
  return `NODUS-TC-${n.getFullYear()}${p(n.getMonth() + 1)}${p(n.getDate())}-${p(n.getHours())}${p(n.getMinutes())}`;
};

function Membrete({ filtros, usuario }: { filtros: string; usuario: string }) {
  return (
    <div className="solo-impresion membrete">
      <div className="membrete-sup">
        <Logo variante="completo" tamano={48} className="membrete-logo" />
        <div className="membrete-inst">
          <strong>HOSPITAL MATERNO NEONATAL</strong>
          <span>Córdoba · Sector Bioingeniería</span>
          <span>Sistema de Gestión Patrimonial y Mantenimiento NODUS</span>
        </div>
        <div className="membrete-doc">
          <span>Documento N° <b>{nroDocumento()}</b></span>
          <span>Emitido: {new Date().toLocaleString('es-AR')}</span>
          <span>Emitió: {usuario}</span>
        </div>
      </div>
      <h1 className="membrete-titulo">Informe del Tablero de Control de Activos de Soporte Ventilatorio</h1>
      <div className="membrete-filtros">{filtros}</div>
    </div>
  );
}

function FirmaImpresion({ usuario }: { usuario: string }) {
  return (
    <div className="solo-impresion firmas">
      <div><span className="linea" />{usuario}<small>Emisor — Bioingeniería</small></div>
      <div><span className="linea" />Jefatura de Bioingeniería<small>Firma y sello</small></div>
      <div><span className="linea" />Dirección del Hospital<small>Firma y sello</small></div>
    </div>
  );
}

const PASOS = ['Consolidando indicadores', 'Renderizando gráficos', 'Aplicando membrete institucional', 'Preparando para impresión'];

type Descarga = 'tablero-xlsx' | 'consolidado-pdf' | 'consolidado-xlsx';

function Exportar({ d, filtros, consulta, usuario, ocultos, setOcultos, onClose }: {
  d: D; filtros: string; consulta: string; usuario: string;
  ocultos: Set<string>; setOcultos: (s: Set<string>) => void; onClose: () => void;
}) {
  const toast = useToast();
  const [paso, setPaso] = useState(-1);
  const [bajando, setBajando] = useState<Descarga | null>(null);
  const generando = paso >= 0;
  const [anio, mes] = d.hasta.split('-').map(Number);
  const periodo = `${MESES[mes - 1]} ${anio}`;

  const alternar = (id: string) => {
    const n = new Set(ocultos);
    if (n.has(id)) n.delete(id); else n.add(id);
    setOcultos(n);
  };

  const imprimir = async () => {
    for (let i = 0; i < PASOS.length; i++) {
      setPaso(i);
      await new Promise(r => setTimeout(r, 450));
    }
    setPaso(PASOS.length);
    await new Promise(r => setTimeout(r, 250));
    onClose();
    setTimeout(() => window.print(), 150);
  };

  const descargar = async (tipo: Descarga) => {
    const rutas: Record<Descarga, [string, string]> = {
      'tablero-xlsx': [`/reportes/dashboard/excel${consulta}`, `Tablero-${d.desde}-${d.hasta}.xlsx`],
      'consolidado-pdf': [`/reportes/consolidado?anio=${anio}&mes=${mes}&formato=pdf`, `Consolidado-${anio}-${mes}.pdf`],
      'consolidado-xlsx': [`/reportes/consolidado?anio=${anio}&mes=${mes}&formato=xlsx`, `Consolidado-${anio}-${mes}.xlsx`],
    };
    setBajando(tipo);
    try { await api.download(...rutas[tipo]); }
    catch (e) { toast(errMsg(e), 'bad'); }
    finally { setBajando(null); }
  };

  const BotonDescarga = ({ tipo, icono, texto }: { tipo: Descarga; icono: string; texto: string }) => (
    <button className="btn btn-sm" onClick={() => descargar(tipo)} disabled={!!bajando}>
      <i className={`fa-solid ${bajando === tipo ? 'fa-spinner fa-spin' : icono}`} /> {texto}
    </button>
  );

  return (
    <Modal titulo="Exportar reportes" onClose={() => !generando && onClose()} ancho={700}
      pie={!generando && <button className="btn" onClick={onClose}>Cerrar</button>}>
      {generando ? (
        <div className="pdf-progreso">
          <div className="pdf-hoja"><i className="fa-solid fa-file-pdf" /></div>
          <ol>
            {PASOS.map((p, i) => (
              <li key={p} className={i < paso ? 'hecho' : i === paso ? 'actual' : ''}>
                <i className={`fa-solid ${i < paso ? 'fa-circle-check' : i === paso ? 'fa-spinner fa-spin' : 'fa-circle'}`} /> {p}
              </li>
            ))}
          </ol>
          <div className="pdf-barra"><div style={{ width: `${Math.min(100, (paso / PASOS.length) * 100)}%` }} /></div>
          <p className="sub">En el diálogo de impresión elegí <b>“Guardar como PDF”</b> como destino.</p>
        </div>
      ) : (
        <>
          <section className="export-seccion">
            <div className="export-cab">
              <div>
                <h4><i className="fa-solid fa-file-pdf txt-rojo" /> Documento oficial del tablero (PDF)</h4>
                <div className="sub">A4 con membrete institucional, número de documento, filtros aplicados y firmas.</div>
              </div>
              <button className="btn btn-primario btn-sm" onClick={imprimir} disabled={ocultos.size === BLOQUES.length}>
                <i className="fa-solid fa-print" /> Generar PDF
              </button>
            </div>
            <div className="pdf-preview">
              <div className="pdf-preview-membrete">
                <Logo variante="completo" tamano={34} />
                <div>
                  <strong>HOSPITAL MATERNO NEONATAL</strong>
                  <span>Córdoba · Sector Bioingeniería</span>
                </div>
                <span className="pdf-preview-nro">{nroDocumento()}</span>
              </div>
              <div className="pdf-preview-titulo">Informe del Tablero de Control de Activos de Soporte Ventilatorio</div>
              <div className="pdf-preview-meta">
                <span><i className="fa-regular fa-calendar" /> {filtros}</span>
                <span><i className="fa-regular fa-user" /> {usuario}</span>
              </div>
            </div>
            <div className="pdf-secciones">
              <div className="campo-label">Secciones a incluir</div>
              {BLOQUES.map(b => (
                <label key={b.id} className="check">
                  <input type="checkbox" checked={!ocultos.has(b.id)} onChange={() => alternar(b.id)} /> {b.texto}
                </label>
              ))}
            </div>
          </section>

          <section className="export-seccion">
            <div className="export-cab">
              <div>
                <h4><i className="fa-solid fa-file-excel txt-verde" /> Tablero de control (Excel)</h4>
                <div className="sub">Una hoja por indicador con los mismos filtros: {filtros}.</div>
              </div>
              <BotonDescarga tipo="tablero-xlsx" icono="fa-download" texto="Descargar .xlsx" />
            </div>
          </section>

          <section className="export-seccion">
            <div className="export-cab">
              <div>
                <h4><i className="fa-solid fa-building-columns" /> Consolidado General Mensual — {periodo}</h4>
                <div className="sub">Inventario, órdenes de trabajo, altas, bajas, traslados, préstamos e insumos del mes.</div>
              </div>
              <div className="export-botones">
                <BotonDescarga tipo="consolidado-pdf" icono="fa-file-pdf" texto="PDF" />
                <BotonDescarga tipo="consolidado-xlsx" icono="fa-file-excel" texto="Excel" />
              </div>
            </div>
          </section>
        </>
      )}
    </Modal>
  );
}
