import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, ApiError } from '../api/client';
import type { Catalogos, Insumo, Ot, ResultadoSeguridad } from '../api/types';
import { Alerta, Campo, Cargando, EstadoBadge, Modal, Vacio, useToast } from '../components/ui';
import { errMsg, fDate, fDateTime, hoyISO, label, tonoEquipo, tonoOt, tonoSeg, tonoStock, useLoad } from '../util';

export default function OtDetallePage() {
  const { id } = useParams();
  const toast = useToast();
  const { data: ot, setData, error, loading } = useLoad(() => api.get<Ot>(`/ots/${id}`), [id]);
  const cat = useLoad(() => api.get<Catalogos>('/catalogos'));
  const insumos = useLoad(() => api.get<Insumo[]>('/insumos'));

  const [ocupado, setOcupado] = useState(false);
  const [modal, setModal] = useState<null | 'seguridad' | 'finalizar' | 'tercerizar' | 'retorno'>(null);
  const [err, setErr] = useState<string | null>(null);

  const [tarea, setTarea] = useState<string | null>(null);
  const [tecnico, setTecnico] = useState<string | null>(null);
  const [seg, setSeg] = useState<{ resultado: ResultadoSeguridad; observaciones: string }>({ resultado: 'APROBADA', observaciones: '' });
  const [consumo, setConsumo] = useState({ idInsumo: '', cantidad: 1 });
  const [terc, setTerc] = useState({ proveedor: '', enGarantia: false, fechaEnvio: hoyISO() });
  const [ret, setRet] = useState({ fechaVueltaReal: hoyISO(), remito: '' });

  if (loading && !ot) return <Cargando />;
  if (error && !ot) return <Alerta>{error}</Alerta>;
  if (!ot) return null;

  const abierta = ot.estado !== 'FINALIZADA';
  const tareaVal = tarea ?? ot.descripcionTareaTecnica ?? '';
  const tecnicoVal = tecnico ?? (ot.idTecnicoAsignado ? String(ot.idTecnicoAsignado) : '');

  /** Ejecuta una acción que devuelve la OT actualizada; muestra errores de la base (NS005, NS007, 23514…) */
  const ejecutar = async (fn: () => Promise<Ot>, ok: string, cerrarModal = true) => {
    setOcupado(true); setErr(null);
    try {
      const nueva = await fn();
      setData(nueva);
      setTarea(null); setTecnico(null);
      if (cerrarModal) setModal(null);
      toast(ok);
      insumos.reload();
      return true;
    } catch (e) {
      const msg = errMsg(e);
      if (modal) setErr(msg);
      else toast(e instanceof ApiError && e.status === 422 ? `⛔ ${msg}` : msg, 'bad');
      return false;
    } finally {
      setOcupado(false);
    }
  };

  const insumoSel = insumos.data?.find(i => String(i.id) === consumo.idInsumo);
  const marcarEspera = (idInsumo: number | null) =>
    ejecutar(() => api.put<Ot>(`/ots/${ot.id}/espera-insumo`, { idInsumo }),
      idInsumo ? 'OT marcada en espera de insumo' : 'Bloqueo por insumo quitado', false);

  return (
    <div>
      <div className="cabecera">
        <div>
          <Link to="/mantenimiento" className="volver">← Mantenimiento</Link>
          <h1>OT #{ot.id} <span className="sub">· {label(ot.tipo)}</span></h1>
          <p className="sub"><Link to={`/equipos/${ot.idEquipo}`} className="codigo">{ot.codigoPatrimonial}</Link> · {ot.equipo} · {ot.servicio}</p>
        </div>
        <div className="acciones">
          <EstadoBadge valor={ot.estado} tonos={tonoOt} />
          <button className="btn btn-sm" onClick={() => api.download(`/ots/${ot.id}/pdf`, `OT-${ot.id}.pdf`).catch(e => toast(errMsg(e), 'bad'))}>PDF</button>
          {ot.tercerizado && <button className="btn btn-sm" onClick={() => api.download(`/ots/${ot.id}/remito-tercerizado`, `Remito-OT-${ot.id}.pdf`).catch(e => toast(errMsg(e), 'bad'))}>Remito de salida</button>}
        </div>
      </div>

      {ot.idInsumoFaltante && abierta && (
        <div className="aviso aviso-warn aviso-bloqueo">
          <i className="fa-solid fa-box-open" />
          <div>
            <strong>OT detenida por falta de stock:</strong> {ot.insumoFaltante} (stock actual {ot.stockInsumoFaltante ?? 0}).
            <div className="sub">Se libera sola al registrar el consumo de ese insumo. {(ot.stockInsumoFaltante ?? 0) > 0 && 'Ya hay stock: podés registrar el consumo.'}</div>
          </div>
          <button className="btn btn-sm" disabled={ocupado} onClick={() => marcarEspera(null)}>Quitar bloqueo</button>
        </div>
      )}

      <ol className="pasos">
        <li className="hecho"><strong>Apertura</strong><span>{fDateTime(ot.fechaApertura)}</span></li>
        <li className={ot.fechaInicioTaller ? 'hecho' : 'actual'}><strong>En taller</strong><span>{fDateTime(ot.fechaInicioTaller)}</span></li>
        <li className={ot.seguridadElectrica === 'APROBADA' ? 'hecho' : ot.estado === 'EN_CURSO' ? 'actual' : ''}><strong>Seguridad eléctrica</strong><span>{label(ot.seguridadElectrica)}</span></li>
        <li className={ot.fechaCierre ? 'hecho' : ''}><strong>Finalizada</strong><span>{fDateTime(ot.fechaCierre)}</span></li>
      </ol>

      <div className="grid-detalle">
        <section className="panel">
          <h2>Trabajo técnico</h2>
          {ot.descripcionFalla && <><div className="sub">Falla reportada</div><p className="cita">“{ot.descripcionFalla}”</p></>}
          <dl className="datos">
            <div><dt>Estado del equipo</dt><dd><EstadoBadge valor={ot.estadoEquipo} tonos={tonoEquipo} /></dd></div>
            <div><dt>Creada por</dt><dd>{ot.creador}</dd></div>
          </dl>
          {abierta ? (
            <>
              <Campo label="Técnico asignado">
                <select value={tecnicoVal} onChange={e => setTecnico(e.target.value)}>
                  <option value="">Sin asignar</option>
                  {cat.data?.tecnicos.map(t => <option key={t.id} value={t.id}>{t.nombre}</option>)}
                </select>
              </Campo>
              <Campo label="Descripción de la tarea técnica">
                <textarea rows={4} value={tareaVal} onChange={e => setTarea(e.target.value)} placeholder="Diagnóstico, reparación realizada, repuestos…" />
              </Campo>
              <div className="fila-acciones">
                <button className="btn btn-sm" disabled={ocupado || (tarea === null && tecnico === null)}
                  onClick={() => ejecutar(() => api.put<Ot>(`/ots/${ot.id}`, { descripcionTareaTecnica: tareaVal || null, idTecnicoAsignado: tecnicoVal ? Number(tecnicoVal) : null }), 'Cambios guardados')}>
                  Guardar cambios
                </button>
              </div>
            </>
          ) : (
            <dl className="datos">
              <div><dt>Técnico</dt><dd>{ot.tecnicoAsignado ?? '—'}</dd></div>
              <div><dt>Tarea realizada</dt><dd className="texto-largo">{ot.descripcionTareaTecnica ?? '—'}</dd></div>
            </dl>
          )}
        </section>

        <section className="panel">
          <h2>Acciones</h2>
          {!abierta && <Alerta tono="ok">OT finalizada el {fDateTime(ot.fechaCierre)}. No admite modificaciones.</Alerta>}
          {ot.estado === 'PENDIENTE' && (
            <button className="btn btn-primario btn-bloque" disabled={ocupado}
              onClick={() => ejecutar(() => api.put<Ot>(`/ots/${ot.id}/iniciar`, { idTecnicoAsignado: tecnicoVal ? Number(tecnicoVal) : null }), 'OT en curso. El equipo pasó a mantenimiento.')}>
              Ingresar a taller (iniciar OT)
            </button>
          )}
          {ot.estado === 'EN_CURSO' && <>
            <div className="seg-estado">
              <span>Seguridad eléctrica:</span> <EstadoBadge valor={ot.seguridadElectrica} tonos={tonoSeg} />
              {ot.observacionesSeguridadElectrica && <div className="sub">{ot.observacionesSeguridadElectrica}</div>}
            </div>
            <button className="btn btn-bloque" onClick={() => { setErr(null); setModal('seguridad'); }}>Registrar prueba de seguridad eléctrica</button>
            <button className="btn btn-primario btn-bloque" disabled={ot.seguridadElectrica !== 'APROBADA'}
              title={ot.seguridadElectrica !== 'APROBADA' ? 'Requisito bloqueante: la prueba de seguridad eléctrica debe estar APROBADA' : undefined}
              onClick={() => { setErr(null); setModal('finalizar'); }}>
              Finalizar OT
            </button>
            {ot.seguridadElectrica !== 'APROBADA' && <p className="sub">⛔ No se puede finalizar sin la prueba de seguridad eléctrica aprobada.</p>}
          </>}
          {abierta && !ot.idInsumoFaltante && (
            <div className="espera-insumo">
              <select defaultValue="" disabled={ocupado} onChange={e => { if (e.target.value) { marcarEspera(Number(e.target.value)); e.target.value = ''; } }}>
                <option value="">Marcar en espera de insumo…</option>
                {insumos.data?.map(i => <option key={i.id} value={i.id}>{i.nombre} (stock {i.stockActual})</option>)}
              </select>
            </div>
          )}
          {abierta && !ot.tercerizado && (
            <button className="btn btn-ghost btn-bloque" onClick={() => { setErr(null); setModal('tercerizar'); }}>Derivar a reparación externa…</button>
          )}

          {ot.tercerizado && (
            <div className="caja-terc">
              <h3>Reparación externa</h3>
              <dl className="datos">
                <div><dt>Proveedor</dt><dd>{ot.tercerizado.proveedor} {ot.tercerizado.enGarantia && <span className="chip">Garantía</span>}</dd></div>
                <div><dt>Enviado</dt><dd>{fDate(ot.tercerizado.fechaEnvio)}</dd></div>
                <div><dt>Retorno probable</dt><dd className={ot.tercerizado.vencido ? 'txt-bad' : ''}>{fDate(ot.tercerizado.fechaProbableVuelta)} {ot.tercerizado.vencido && <span className="chip chip-bad">Vencido</span>}</dd></div>
                <div><dt>Retorno real</dt><dd>{fDate(ot.tercerizado.fechaVueltaReal)}</dd></div>
                {ot.tercerizado.remitoEntrada && <div><dt>Remito de entrada</dt><dd>{ot.tercerizado.remitoEntrada}</dd></div>}
              </dl>
              {abierta && !ot.tercerizado.fechaVueltaReal && (
                <button className="btn btn-sm" onClick={() => { setErr(null); setModal('retorno'); }}>Registrar retorno del equipo</button>
              )}
            </div>
          )}
        </section>
      </div>

      <section className="panel">
        <h2>Insumos consumidos</h2>
        {ot.consumos.length === 0 ? <Vacio>Sin consumos registrados.</Vacio> : (
          <table className="tabla">
            <thead><tr><th>Insumo</th><th className="num">Cantidad</th><th>Fecha</th><th>Registró</th>{abierta && <th />}</tr></thead>
            <tbody>{ot.consumos.map(c => (
              <tr key={c.id}>
                <td>{c.insumo}</td><td className="num">{c.cantidad}</td><td>{fDateTime(c.fecha)}</td><td>{c.usuario}</td>
                {abierta && <td className="alinear-der">
                  <button className="btn btn-sm btn-ghost" disabled={ocupado} title="Elimina el consumo y devuelve el stock"
                    onClick={() => ejecutar(() => api.del<Ot>(`/ots/${ot.id}/consumos/${c.id}`), 'Consumo eliminado; stock devuelto')}>Quitar</button>
                </td>}
              </tr>
            ))}</tbody>
          </table>
        )}
        {abierta && (
          <form className="fila-acciones consumo-form" onSubmit={e => {
            e.preventDefault();
            ejecutar(() => api.post<Ot>(`/ots/${ot.id}/consumos`, { idInsumo: Number(consumo.idInsumo), cantidad: consumo.cantidad }),
              'Consumo registrado').then(ok => { if (ok) setConsumo({ idInsumo: '', cantidad: 1 }); });
          }}>
            <select required value={consumo.idInsumo} onChange={e => setConsumo({ ...consumo, idInsumo: e.target.value })}>
              <option value="">Insumo…</option>
              {insumos.data?.map(i => <option key={i.id} value={i.id}>{i.nombre} (stock {i.stockActual})</option>)}
            </select>
            <input type="number" min={1} required value={consumo.cantidad} onChange={e => setConsumo({ ...consumo, cantidad: Number(e.target.value) })} style={{ width: 90 }} />
            <button className="btn btn-primario" disabled={ocupado}>Registrar consumo</button>
            {insumoSel && <EstadoBadge valor={insumoSel.estadoStock} tonos={tonoStock} />}
            {insumoSel && consumo.cantidad > insumoSel.stockActual && <span className="txt-bad sub">Stock insuficiente: la base rechazará el consumo.</span>}
          </form>
        )}
      </section>

      {modal === 'seguridad' && (
        <Modal titulo="Prueba de seguridad eléctrica" onClose={() => setModal(null)} pie={<>
          <button className="btn" onClick={() => setModal(null)}>Cancelar</button>
          <button className="btn btn-primario" disabled={ocupado}
            onClick={() => ejecutar(() => api.put<Ot>(`/ots/${ot.id}/seguridad-electrica`, { resultado: seg.resultado, observaciones: seg.observaciones || null }), 'Prueba registrada')}>Guardar</button>
        </>}>
          <div className="radios">
            {(['APROBADA', 'NO_APROBADA'] as ResultadoSeguridad[]).map(r => (
              <label key={r} className={`radio radio-${r === 'APROBADA' ? 'ok' : 'bad'} ${seg.resultado === r ? 'sel' : ''}`}>
                <input type="radio" name="seg" checked={seg.resultado === r} onChange={() => setSeg({ ...seg, resultado: r })} /> {label(r)}
              </label>
            ))}
          </div>
          <Campo label="Observaciones" ayuda={seg.resultado === 'NO_APROBADA' ? 'Obligatorio si no fue aprobada.' : 'Mediciones, instrumento utilizado, etc.'}>
            <textarea rows={3} value={seg.observaciones} onChange={e => setSeg({ ...seg, observaciones: e.target.value })} />
          </Campo>
          <Alerta>{err}</Alerta>
        </Modal>
      )}

      {modal === 'finalizar' && (
        <Modal titulo={`Finalizar OT #${ot.id}`} onClose={() => setModal(null)} pie={<>
          <button className="btn" onClick={() => setModal(null)}>Cancelar</button>
          <button className="btn btn-primario" disabled={ocupado || !tareaVal.trim()}
            onClick={() => ejecutar(() => api.put<Ot>(`/ots/${ot.id}/finalizar`, { descripcionTareaTecnica: tareaVal }), 'OT finalizada. El equipo volvió a OPERATIVO.')}>Finalizar</button>
        </>}>
          <p>Al finalizar, la OT queda cerrada (no se puede reabrir) y el equipo vuelve a <strong>operativo</strong>.
            {ot.tipo === 'PREVENTIVO' && ' El próximo preventivo se reprograma a 365 días.'}</p>
          <Campo label="Tarea técnica realizada (obligatorio)">
            <textarea rows={4} value={tareaVal} onChange={e => setTarea(e.target.value)} />
          </Campo>
          <Alerta>{err}</Alerta>
        </Modal>
      )}

      {modal === 'tercerizar' && (
        <Modal titulo="Derivar a reparación externa" onClose={() => setModal(null)} pie={<>
          <button className="btn" onClick={() => setModal(null)}>Cancelar</button>
          <button className="btn btn-primario" disabled={ocupado || !terc.proveedor.trim()}
            onClick={() => ejecutar(() => api.post<Ot>(`/ots/${ot.id}/tercerizar`, { proveedorFabricaNombre: terc.proveedor, enGarantia: terc.enGarantia, fechaEnvio: terc.fechaEnvio }), 'OT tercerizada')}>Derivar</button>
        </>}>
          <Campo label="Proveedor / fábrica"><input value={terc.proveedor} onChange={e => setTerc({ ...terc, proveedor: e.target.value })} maxLength={150} /></Campo>
          <Campo label="Fecha de envío"><input type="date" value={terc.fechaEnvio} onChange={e => setTerc({ ...terc, fechaEnvio: e.target.value })} /></Campo>
          <label className="check"><input type="checkbox" checked={terc.enGarantia} onChange={e => setTerc({ ...terc, enGarantia: e.target.checked })} /> El equipo está en garantía</label>
          <Alerta>{err}</Alerta>
        </Modal>
      )}

      {modal === 'retorno' && (
        <Modal titulo="Retorno desde el proveedor" onClose={() => setModal(null)} pie={<>
          <button className="btn" onClick={() => setModal(null)}>Cancelar</button>
          <button className="btn btn-primario" disabled={ocupado}
            onClick={() => ejecutar(() => api.put<Ot>(`/ots/${ot.id}/tercerizado/retorno`, { fechaVueltaReal: ret.fechaVueltaReal, remitoEntradaDescripcion: ret.remito || null }), 'Retorno registrado')}>Guardar</button>
        </>}>
          <Campo label="Fecha de retorno"><input type="date" max={hoyISO()} value={ret.fechaVueltaReal} onChange={e => setRet({ ...ret, fechaVueltaReal: e.target.value })} /></Campo>
          <Campo label="Remito de entrada / informe del proveedor"><textarea rows={3} value={ret.remito} onChange={e => setRet({ ...ret, remito: e.target.value })} /></Campo>
          <p className="sub">Después del retorno, realizá la prueba de seguridad eléctrica antes de finalizar la OT.</p>
          <Alerta>{err}</Alerta>
        </Modal>
      )}
    </div>
  );
}
