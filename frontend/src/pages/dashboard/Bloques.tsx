import { useState, type MouseEvent, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Bar, Doughnut, Line } from 'react-chartjs-2';
import type { Dashboard, GrillaServicio, TareaDia } from '../../api/types';
import { fDate } from '../../util';
import { C, COLOR_ESTADO, SERIE } from './charts';

const MESES_CORTOS = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];
const DIAS = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom'];
const DIAS_LARGOS = ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'];

export function Bloque({ id, titulo, icono, descripcion, children, oculto }: {
  id: string; titulo: string; icono: string; descripcion: string; children: ReactNode; oculto?: boolean;
}) {
  return (
    <section className={`dash-bloque ${oculto ? 'print-oculto' : ''}`} id={`bloque-${id}`}>
      <header className="dash-bloque-cab">
        <span className="dash-bloque-icono"><i className={`fa-solid ${icono}`} /></span>
        <div>
          <h2>{titulo}</h2>
          <p>{descripcion}</p>
        </div>
      </header>
      {children}
    </section>
  );
}

export function Tarjeta({ titulo, icono, alcance, children, pie, className = '' }: {
  titulo: string; icono: string; alcance: 'vivo' | 'periodo' | '12m'; children: ReactNode; pie?: ReactNode; className?: string;
}) {
  const etiquetas = { vivo: 'Tiempo real', periodo: 'Período', '12m': 'Últimos 12 meses' };
  return (
    <article className={`dash-card ${className}`}>
      <header className="dash-card-cab">
        <h3><i className={`fa-solid ${icono}`} /> {titulo}</h3>
        <span className={`alcance alcance-${alcance}`}>
          {alcance === 'vivo' && <span className="pulso" />}{etiquetas[alcance]}
        </span>
      </header>
      <div className="dash-card-cuerpo">{children}</div>
      {pie && <footer className="dash-card-pie">{pie}</footer>}
    </article>
  );
}

function SinDatos({ texto = 'Sin datos para los filtros seleccionados.' }: { texto?: string }) {
  return <div className="dash-vacio"><i className="fa-regular fa-folder-open" /> {texto}</div>;
}

export function DisponibilidadPorTipo({ d }: { d: Dashboard }) {
  const filas = d.porTipo;
  if (filas.every(f => f.total === 0)) return <SinDatos />;
  return (
    <div className="disp-lista">
      <div className="disp-escala" aria-hidden>
        <span style={{ left: 0 }}>0%</span>
        <span className="disp-escala-umbral" style={{ left: `${d.umbral}%` }}>Umbral {d.umbral}%</span>
        <span style={{ right: 0 }}>100%</span>
      </div>
      {filas.map(t => {
        const critico = t.semaforo === 'CRITICO';
        return (
          <div key={t.idFamilia} className={`disp-fila ${t.total === 0 ? 'sin-equipos' : ''}`}>
            <div className="disp-nombre">
              {t.familia}
              <span className="disp-frac">{t.total === 0 ? 'sin equipos' : `${t.operativos}/${t.total}`}</span>
            </div>
            <div className="disp-barra" title={`${t.operativos} operativos de ${t.total}`}>
              <div className={`disp-relleno ${critico ? 'rojo' : 'verde'}`} style={{ width: `${t.total ? t.porcentaje : 0}%` }} />
              <div className="disp-umbral" style={{ left: `${d.umbral}%` }} />
            </div>
            <div className={`disp-pct ${t.total === 0 ? '' : critico ? 'txt-rojo' : 'txt-verde'}`}>
              {t.total === 0 ? '—' : `${t.porcentaje.toFixed(1)}%`}
              {critico && <i className="fa-solid fa-triangle-exclamation" title="Por debajo del umbral crítico" />}
            </div>
          </div>
        );
      })}
    </div>
  );
}

export function GrillaServicios({ d }: { d: Dashboard }) {
  const celda = (n: number, clase: string) => <td className={`num ${n ? clase : 'cero'}`}>{n}</td>;
  const fila = (g: GrillaServicio, total = false) => (
    <tr key={g.idServicio} className={total ? 'fila-total' : ''}>
      <th scope="row">{g.servicio}</th>
      {celda(g.operativos, 'c-ok')}
      {celda(g.fueraDeServicio, 'c-bad')}
      {celda(g.enMantenimiento, 'c-warn')}
      {celda(g.enPrestamo, 'c-info')}
      <td className="num"><strong>{g.total}</strong></td>
      <td className="num">
        {g.total ? <span className={100 * g.operativos / g.total < d.umbral ? 'txt-rojo' : 'txt-verde'}>{Math.round(100 * g.operativos / g.total)}%</span> : '—'}
      </td>
    </tr>
  );
  return (
    <div className="tabla-scroll">
      <table className="dash-tabla">
        <thead>
          <tr>
            <th>Servicio / Área</th>
            <th className="num"><span className="dot" style={{ background: COLOR_ESTADO.operativos }} />Operativos</th>
            <th className="num"><span className="dot" style={{ background: COLOR_ESTADO.fueraDeServicio }} />Fuera de serv.</th>
            <th className="num"><span className="dot" style={{ background: COLOR_ESTADO.enMantenimiento }} />En mant.</th>
            <th className="num"><span className="dot" style={{ background: COLOR_ESTADO.enPrestamo }} />Prestados</th>
            <th className="num">Total</th>
            <th className="num">Disp.</th>
          </tr>
        </thead>
        <tbody>{d.grilla.map(g => fila(g))}</tbody>
        <tfoot>{fila(d.grillaTotal, true)}</tfoot>
      </table>
    </div>
  );
}

export function DonaAreas({ d }: { d: Dashboard }) {
  const areas = d.grilla.filter(g => g.total > 0);
  if (areas.length === 0) return <SinDatos />;
  return (
    <div className="dona-wrap">
      <div className="dona-canvas">
        <Doughnut
          data={{
            labels: areas.map(a => a.servicio),
            datasets: [{ data: areas.map(a => a.total), backgroundColor: areas.map((_, i) => SERIE[i % SERIE.length]), borderColor: '#fff', borderWidth: 3, hoverOffset: 6 }],
          }}
          options={{
            cutout: '68%',
            plugins: {
              legend: { display: false },
              tooltip: {
                callbacks: {
                  label: ctx => ` ${ctx.label}: ${ctx.parsed} equipos`,
                  afterLabel: ctx => {
                    const a = areas[ctx.dataIndex];
                    return [`Operativos: ${a.operativos}`, `Fuera de servicio: ${a.fueraDeServicio}`, `En mantenimiento: ${a.enMantenimiento}`, `Prestados: ${a.enPrestamo}`];
                  },
                },
              },
            },
          }}
        />
        <div className="dona-centro"><strong>{d.grillaTotal.total}</strong><span>equipos</span></div>
      </div>
      <ul className="dona-leyenda">
        {areas.map((a, i) => (
          <li key={a.idServicio}>
            <span className="dot" style={{ background: SERIE[i % SERIE.length] }} />
            <span className="dona-area">{a.servicio}</span>
            <span className="dona-mini">
              <span style={{ flex: a.operativos, background: COLOR_ESTADO.operativos }} />
              <span style={{ flex: a.fueraDeServicio, background: COLOR_ESTADO.fueraDeServicio }} />
              <span style={{ flex: a.enMantenimiento, background: COLOR_ESTADO.enMantenimiento }} />
              <span style={{ flex: a.enPrestamo, background: COLOR_ESTADO.enPrestamo }} />
            </span>
            <span className="num"><strong>{a.operativos}</strong>/{a.total}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

export function OtsBloqueadas({ d }: { d: Dashboard }) {
  const b = d.otsBloqueadas;
  return (
    <div className="bloq-wrap">
      <div className="bloq-chart">
        <Bar
          data={{
            labels: ['Falta de stock de insumos', 'Seguridad eléctrica no aprobada'],
            datasets: [{ data: [b.porStock, b.porSeguridadElectrica], backgroundColor: [C.ambar, C.rojo], borderRadius: 6, maxBarThickness: 64 }],
          }}
          options={{
            plugins: { legend: { display: false }, tooltip: { callbacks: { label: ctx => ` ${ctx.parsed.y} OT detenidas` } } },
            scales: { y: { beginAtZero: true, ticks: { precision: 0 } }, x: { grid: { display: false }, ticks: { font: { size: 11 } } } },
          }}
        />
      </div>
      {b.detalle.length === 0 ? (
        <div className="dash-ok"><i className="fa-solid fa-circle-check" /> Ninguna OT detenida por stock o seguridad eléctrica.</div>
      ) : (
        <ul className="bloq-lista">
          {b.detalle.map(o => (
            <li key={`${o.motivo}-${o.idOt}`}>
              <i className={`fa-solid ${o.motivo === 'STOCK' ? 'fa-box-open txt-ambar' : 'fa-bolt txt-rojo'}`} />
              <div>
                <Link to={`/ots/${o.idOt}`}><strong>OT #{o.idOt}</strong> · <span className="mono">{o.codigoPatrimonial}</span></Link>
                <div className="sub">{o.detalle}</div>
              </div>
              <span className="sub">{o.servicio}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function ListaTareas({ items, vacio, enlace, unidad }: {
  items: TareaDia[]; vacio: string; enlace: (t: TareaDia) => string; unidad: (t: TareaDia) => string;
}) {
  if (items.length === 0) return <div className="tarea-vacia"><i className="fa-solid fa-check" /> {vacio}</div>;
  return (
    <ul className="tareas">
      {items.map(t => {
        const tono = t.diasRestantes < 0 ? 'vencida' : t.diasRestantes === 0 ? 'hoy' : 'proxima';
        return (
          <li key={t.idReferencia} className={`tarea tarea-${tono}`}>
            <Link to={enlace(t)}>
              <div className="tarea-sup">
                <span className="mono tarea-titulo">{t.titulo}</span>
                <span className={`tarea-plazo plazo-${tono}`}>{unidad(t)}</span>
              </div>
              <div className="sub">{t.detalle} · {fDate(t.fecha)}</div>
            </Link>
          </li>
        );
      })}
    </ul>
  );
}

const plazo = (t: TareaDia) =>
  t.diasRestantes < 0 ? `Vencido hace ${-t.diasRestantes} d` : t.diasRestantes === 0 ? 'Hoy' : t.diasRestantes === 1 ? 'Mañana' : `En ${t.diasRestantes} d`;

export function TareasDelDia({ d }: { d: Dashboard }) {
  const t = d.tareas;
  return (
    <div className="tareas-grid">
      <div>
        <h4><i className="fa-solid fa-screwdriver-wrench" /> Preventivos hoy / 24 h <span className="contador">{t.preventivos.length}</span></h4>
        <ListaTareas items={t.preventivos} vacio="Sin preventivos para hoy." enlace={x => `/equipos/${x.idReferencia}`} unidad={plazo} />
      </div>
      <div>
        <h4><i className="fa-solid fa-right-left" /> Préstamos que vencen esta semana <span className="contador">{t.prestamos.length}</span></h4>
        <ListaTareas items={t.prestamos} vacio="Ningún préstamo vence esta semana." enlace={() => '/prestamos'} unidad={plazo} />
      </div>
      <div>
        <h4><i className="fa-solid fa-truck-medical" /> Tercerizados próximos a retornar <span className="contador">{t.tercerizados.length}</span></h4>
        <ListaTareas items={t.tercerizados} vacio="Sin retornos previstos esta semana." enlace={x => `/ots/${x.idReferencia}`} unidad={plazo} />
      </div>
    </div>
  );
}

export function FallasPorMarca({ d }: { d: Dashboard }) {
  const datos = d.fallasPorMarca.slice(0, 10);
  if (datos.length === 0) return <SinDatos texto="No se registraron fallas en el período." />;
  const total = d.fallasPorMarca.reduce((a, b) => a + b.cantidad, 0);
  return (
    <div style={{ height: Math.max(180, datos.length * 34 + 40) }}>
      <Bar
        data={{
          labels: datos.map(x => x.nombre),
          datasets: [{ data: datos.map(x => x.cantidad), backgroundColor: datos.map((_, i) => i === 0 ? C.rojo : C.navy2), borderRadius: 4, maxBarThickness: 22 }],
        }}
        options={{
          indexAxis: 'y',
          plugins: {
            legend: { display: false },
            tooltip: { callbacks: { label: ctx => { const n = ctx.parsed.x ?? 0; return ` ${n} fallas (${Math.round(100 * n / total)}% del total)`; } } },
          },
          scales: { x: { beginAtZero: true, ticks: { precision: 0 } }, y: { grid: { display: false } } },
        }}
      />
    </div>
  );
}

export function MttrChart({ d }: { d: Dashboard }) {
  const filas = d.mttr.porFamilia.filter(f => f.nCorrectivo + f.nPreventivo > 0);
  return (
    <div className="mttr-wrap">
      <div className="mttr-resumen">
        <div><span>MTTR correctivo</span><strong>{d.mttr.globalCorrectivo != null ? `${d.mttr.globalCorrectivo.toFixed(1)} d` : '—'}</strong><small>del reporte de falla al alta</small></div>
        <div><span>MTTR preventivo</span><strong>{d.mttr.globalPreventivo != null ? `${d.mttr.globalPreventivo.toFixed(1)} d` : '—'}</strong><small>de la apertura al cierre</small></div>
      </div>
      {filas.length === 0 ? <SinDatos texto="No hay OT correctivas ni preventivas cerradas en el período." /> : (
        <div className="mttr-chart">
          <Bar
            data={{
              labels: filas.map(f => f.familia),
              datasets: [
                { label: 'Correctivo', data: filas.map(f => f.correctivo), backgroundColor: C.navy, borderRadius: 4, maxBarThickness: 28 },
                { label: 'Preventivo', data: filas.map(f => f.preventivo), backgroundColor: C.gris, borderRadius: 4, maxBarThickness: 28 },
              ],
            }}
            options={{
              plugins: {
                legend: { position: 'top', align: 'end' },
                tooltip: {
                  callbacks: {
                    label: ctx => {
                      const f = filas[ctx.dataIndex];
                      const n = ctx.datasetIndex === 0 ? f.nCorrectivo : f.nPreventivo;
                      return ctx.parsed.y == null ? ` ${ctx.dataset.label}: sin OT` : ` ${ctx.dataset.label}: ${ctx.parsed.y.toFixed(1)} días (${n} OT)`;
                    },
                  },
                },
              },
              scales: {
                y: { beginAtZero: true, title: { display: true, text: 'Días promedio' } },
                x: { grid: { display: false }, ticks: { autoSkip: false, maxRotation: 30, font: { size: 11 } } },
              },
            }}
          />
        </div>
      )}
    </div>
  );
}

export function ParqueChart({ d }: { d: Dashboard }) {
  const p = d.parque;
  if (p.serie.length === 0) return <SinDatos />;
  return (
    <div className="parque-wrap">
      <div className="parque-kpis">
        <div className={p.porcentajeExcedido >= 30 ? 'alto' : ''}>
          <span>Supera vida útil (&gt; {p.vidaUtilAnios} años)</span>
          <strong>{p.porcentajeExcedido.toFixed(1)}%</strong>
          <small>{p.excedidosActual} de {p.totalActual} equipos en servicio</small>
        </div>
        <div>
          <span>Antigüedad promedio</span>
          <strong>{p.edadPromedio.toFixed(1)} años</strong>
          <small>del parque en servicio</small>
        </div>
      </div>
      <div className="parque-chart">
        <Line
          data={{
            labels: p.serie.map(s => String(s.anio)),
            datasets: [
              { label: `Dentro de vida útil (≤ ${p.vidaUtilAnios} años)`, data: p.serie.map(s => s.vigentes), fill: 'origin', backgroundColor: 'rgba(43, 84, 128, .28)', borderColor: C.navy2, tension: .3, pointRadius: 3, stack: 'parque' },
              { label: `Supera vida útil (> ${p.vidaUtilAnios} años)`, data: p.serie.map(s => s.excedidos), fill: '-1', backgroundColor: 'rgba(198, 40, 40, .30)', borderColor: C.rojo, tension: .3, pointRadius: 3, stack: 'parque' },
            ],
          }}
          options={{
            interaction: { mode: 'index', intersect: false },
            plugins: {
              legend: { position: 'top', align: 'end' },
              tooltip: {
                callbacks: {
                  footer: items => {
                    const tot = items.reduce((a, i) => a + (i.parsed.y ?? 0), 0);
                    const exc = items.find(i => i.datasetIndex === 1)?.parsed.y ?? 0;
                    return `Total: ${tot} · ${tot ? Math.round(100 * exc / tot) : 0}% supera vida útil`;
                  },
                },
              },
            },
            scales: { y: { stacked: true, beginAtZero: true, ticks: { precision: 0 }, title: { display: true, text: 'Equipos en servicio' } }, x: { grid: { display: false } } },
          }}
        />
      </div>
    </div>
  );
}

export function ConsumoRecursos({ d }: { d: Dashboard }) {
  const c = d.consumo;
  const filas = c.porFamilia.filter(f => f.unidadesInsumos + f.serviciosExternos > 0);
  return (
    <div className="consumo-wrap">
      <div className="consumo-kpis">
        <div><i className="fa-solid fa-boxes-stacked" /><strong>{c.totalUnidades}</strong><span>unidades de repuestos e insumos</span></div>
        <div><i className="fa-solid fa-industry" /><strong>{c.totalServiciosExternos}</strong><span>servicios técnicos externos</span></div>
      </div>
      {filas.length === 0 ? <SinDatos texto="Sin consumos ni servicios externos en el período." /> : (
        <div className="consumo-chart">
          <Bar
            data={{
              labels: filas.map(f => f.familia),
              datasets: [
                { label: 'Insumos / repuestos (u.)', data: filas.map(f => f.unidadesInsumos), backgroundColor: C.navy2, borderRadius: 4, yAxisID: 'y', maxBarThickness: 26 },
                { label: 'Servicios externos', data: filas.map(f => f.serviciosExternos), backgroundColor: C.ambar, borderRadius: 4, yAxisID: 'y1', maxBarThickness: 26 },
              ],
            }}
            options={{
              plugins: { legend: { position: 'top', align: 'end' } },
              scales: {
                y: { beginAtZero: true, ticks: { precision: 0 }, title: { display: true, text: 'Unidades' } },
                y1: { beginAtZero: true, position: 'right', grid: { drawOnChartArea: false }, ticks: { precision: 0 }, title: { display: true, text: 'Servicios' } },
                x: { grid: { display: false }, ticks: { autoSkip: false, maxRotation: 30, font: { size: 11 } } },
              },
            }}
          />
        </div>
      )}
      {c.topInsumos.length > 0 && (
        <table className="dash-tabla compacta">
          <thead><tr><th>Insumos más consumidos</th><th className="num">Unidades</th></tr></thead>
          <tbody>{c.topInsumos.map(i => <tr key={i.nombre}><td>{i.nombre}</td><td className="num">{i.cantidad}</td></tr>)}</tbody>
        </table>
      )}
    </div>
  );
}

const NIVELES = ['#f1f5f9', '#fde2e1', '#f8b4b0', '#ee7d77', '#d64541', '#a32622'];
const nivel = (n: number, max: number) => n === 0 ? 0 : Math.min(5, Math.ceil((n / max) * 5));
const hora = (h: number) => `${String(h).padStart(2, '0')}:00`;

export function MapaDeCalor({ d }: { d: Dashboard }) {
  const h = d.heatmap;
  const [hover, setHover] = useState<{ x: number; y: number; texto: string } | null>(null);
  const maxMes = Math.max(1, ...h.porMes.map(c => c.fallas));
  const maxDH = Math.max(1, ...h.diaHora.flat());
  const totalesMes = h.meses.map(m => h.porMes.filter(c => c.mes === m).reduce((a, c) => a + c.fallas, 0));
  const etiquetaMes = (m: string) => `${MESES_CORTOS[Number(m.slice(5)) - 1]} ${m.slice(2, 4)}`;
  const esInvierno = (m: string) => ['06', '07', '08'].includes(m.slice(5));

  const mostrar = (e: MouseEvent<HTMLElement>, texto: string) => {
    const caja = (e.currentTarget.closest('.calor') as HTMLElement).getBoundingClientRect();
    const r = (e.currentTarget as HTMLElement).getBoundingClientRect();
    setHover({ x: r.left - caja.left + r.width / 2, y: r.top - caja.top, texto });
  };

  return (
    <div className="calor" onMouseLeave={() => setHover(null)}>
      <div className="calor-metricas">
        <div><i className="fa-regular fa-clock" /><span>Hora pico</span><strong>{h.horaPico != null ? `${hora(h.horaPico)}–${hora((h.horaPico + 1) % 24)}` : '—'}</strong><small>{h.totalFallasPeriodo} fallas en el período</small></div>
        <div><i className="fa-regular fa-calendar" /><span>Día con más fallas</span><strong>{h.diaPico != null ? DIAS_LARGOS[h.diaPico] : '—'}</strong><small>período seleccionado</small></div>
        <div><i className="fa-solid fa-chart-line" /><span>Mes de mayor demanda</span><strong>{h.mesPico ?? '—'}</strong><small>{h.mesPico ? `${h.mesPicoFallas} fallas` : 'últimos 12 meses'}</small></div>
        <div className={h.porcentajeInvierno >= 35 ? 'destacado' : ''}><i className="fa-regular fa-snowflake" /><span>Pico estacional (invierno)</span><strong>{h.porcentajeInvierno.toFixed(0)}%</strong><small>de las fallas en jun–ago (esperable 25%)</small></div>
      </div>

      <div className="calor-titulo">Fallas por tipo de equipo y mes <span className="sub">· {h.totalFallas12Meses} fallas en los últimos 12 meses</span></div>
      <div className="calor-scroll">
        <div className="calor-grid" style={{ gridTemplateColumns: `minmax(130px, 170px) repeat(${h.meses.length}, minmax(28px, 1fr)) 44px` }}>
          <div />
          {h.meses.map(m => <div key={m} className={`calor-col ${esInvierno(m) ? 'invierno' : ''}`}>{etiquetaMes(m)}</div>)}
          <div className="calor-col">Total</div>
          {h.familias.map(f => {
            const tot = h.porMes.filter(c => c.familia === f).reduce((a, c) => a + c.fallas, 0);
            return [
              <div key={`${f}-n`} className="calor-fila" title={f}>{f}</div>,
              ...h.meses.map(m => {
                const n = h.porMes.find(c => c.familia === f && c.mes === m)?.fallas ?? 0;
                return (
                  <div key={`${f}-${m}`} className={`calor-celda n${nivel(n, maxMes)}`}
                    onMouseEnter={e => mostrar(e, `${f} · ${etiquetaMes(m)}: ${n} ${n === 1 ? 'falla' : 'fallas'}`)}>
                    {n > 0 ? n : ''}
                  </div>
                );
              }),
              <div key={`${f}-t`} className="calor-tot">{tot}</div>,
            ];
          })}
          <div className="calor-fila calor-pie">Total mensual</div>
          {totalesMes.map((t, i) => <div key={i} className="calor-tot calor-pie">{t}</div>)}
          <div className="calor-tot calor-pie">{h.totalFallas12Meses}</div>
        </div>
      </div>

      <div className="calor-titulo">Distribución horaria <span className="sub">· día de la semana × hora del reporte (período)</span></div>
      <div className="calor-scroll">
        <div className="calor-grid horas" style={{ gridTemplateColumns: `44px repeat(24, minmax(16px, 1fr))` }}>
          <div />
          {Array.from({ length: 24 }, (_, i) => <div key={i} className="calor-col">{i % 3 === 0 ? String(i).padStart(2, '0') : ''}</div>)}
          {h.diaHora.map((fila, di) => [
            <div key={`d${di}`} className="calor-fila">{DIAS[di]}</div>,
            ...fila.map((n, hi) => (
              <div key={`${di}-${hi}`} className={`calor-celda mini n${nivel(n, maxDH)} ${h.horaPico === hi ? 'pico' : ''}`}
                onMouseEnter={e => mostrar(e, `${DIAS_LARGOS[di]} ${hora(hi)}–${hora((hi + 1) % 24)}: ${n} ${n === 1 ? 'falla' : 'fallas'}`)} />
            )),
          ])}
        </div>
      </div>

      <div className="calor-leyenda">
        <span>Menos</span>
        {NIVELES.map((c, i) => <span key={i} className="calor-muestra" style={{ background: c }} />)}
        <span>Más fallas</span>
        <span className="sub calor-nota"><span className="calor-muestra invierno-muestra" /> meses de invierno</span>
      </div>

      {hover && <div className="calor-tooltip" style={{ left: hover.x, top: hover.y }}>{hover.texto}</div>}
    </div>
  );
}
