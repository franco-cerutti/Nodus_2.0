import { useState, type FormEvent, type ReactNode } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api } from '../api/client';
import type { QrEquipo, ReporteFalla } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { Alerta, Cargando, EstadoBadge } from '../components/ui';
import { errMsg, fDateTime, tonoEquipo, useLoad } from '../util';
import Logo from '../components/Logo';

/** Contenedor mobile-first para el personal asistencial */
function MarcoQr({ children }: { children: ReactNode }) {
  const { usuario, logout } = useAuth();
  const esSector = usuario && usuario.rol !== 'Personal Asistencial';
  return (
    <div className="qr">
      <header className="qr-cab">
        <div className="marca">
          <Logo tamano={28} placa />
          <div>
            <div className="marca-nombre">NODUS</div>
            <div className="marca-sub">{usuario?.nombreServicio ?? usuario?.nombreCompleto}</div>
          </div>
        </div>
        {esSector
          ? <Link to="/" className="btn btn-sm btn-ghost-claro">Ir al sistema</Link>
          : <button className="btn btn-sm btn-ghost-claro" onClick={logout}>Salir</button>}
      </header>
      <main className="qr-main">{children}</main>
    </div>
  );
}

/** /q — sin código: ingreso manual (por si la cámara no abre el enlace) */
export function QrInicio() {
  const [codigo, setCodigo] = useState('');
  const nav = useNavigate();
  return (
    <MarcoQr>
      <div className="qr-card">
        <div className="qr-icono" aria-hidden>⌗</div>
        <h1>Escaneá el código QR del equipo</h1>
        <p className="sub">Usá la cámara del celular sobre la etiqueta del equipo. Si no funciona, ingresá el código patrimonial.</p>
        <form onSubmit={e => { e.preventDefault(); if (codigo.trim()) nav(`/q/${encodeURIComponent(codigo.trim().toUpperCase())}`); }}>
          <label className="campo">
            <span className="campo-label">Código patrimonial</span>
            <input value={codigo} onChange={e => setCodigo(e.target.value)} placeholder="HMN-UTIN-EMED-0001" autoCapitalize="characters" />
          </label>
          <button className="btn btn-primario btn-bloque">Buscar equipo</button>
        </form>
      </div>
    </MarcoQr>
  );
}

/** /q/:codigo — ubicación, estado y reporte de falla */
export default function QrEquipoPage() {
  const { codigo = '' } = useParams();
  const { data: eq, error, loading, reload } = useLoad(() => api.get<QrEquipo>(`/qr/${encodeURIComponent(codigo)}`), [codigo]);

  const [form, setForm] = useState({ nombre: '', apellido: '', matricula: '', descripcionFalla: '' });
  const [enviando, setEnviando] = useState(false);
  const [errorForm, setErrorForm] = useState<string | null>(null);
  const [enviado, setEnviado] = useState<ReporteFalla | null>(null);

  const set = (k: keyof typeof form) => (e: { target: { value: string } }) => setForm(f => ({ ...f, [k]: e.target.value }));

  const enviar = async (e: FormEvent) => {
    e.preventDefault();
    setEnviando(true);
    setErrorForm(null);
    try {
      setEnviado(await api.post<ReporteFalla>(`/qr/${encodeURIComponent(codigo)}/reportes`, form));
    } catch (ex) {
      setErrorForm(errMsg(ex));
      reload();
    } finally {
      setEnviando(false);
    }
  };

  if (loading && !eq) return <MarcoQr><Cargando /></MarcoQr>;
  if (error || !eq) return (
    <MarcoQr>
      <div className="qr-card">
        <Alerta>{error ?? 'Equipo no encontrado'}</Alerta>
        <Link className="btn btn-bloque" to="/q">Ingresar otro código</Link>
      </div>
    </MarcoQr>
  );

  return (
    <MarcoQr>
      <div className="qr-card">
        <div className="qr-codigo">{eq.codigoPatrimonial}</div>
        <h1 className="qr-equipo">{eq.familia}</h1>
        <p className="sub">{eq.marca} · {eq.modelo}</p>
        <dl className="qr-datos">
          <div><dt>Ubicación actual</dt><dd>{eq.servicioActual}</dd></div>
          <div><dt>Estado</dt><dd><EstadoBadge valor={eq.estado} tonos={tonoEquipo} /></dd></div>
        </dl>
      </div>

      {enviado ? (
        <div className="qr-card qr-ok">
          <div className="qr-icono" aria-hidden>✓</div>
          <h2>Falla reportada</h2>
          <p>Bioingeniería recibió el reporte N° <strong>{enviado.id}</strong> ({fDateTime(enviado.fechaHora)}).</p>
          <p className="sub">Un técnico revisará el equipo. No hace falta volver a reportarlo.</p>
          <Link className="btn btn-bloque" to="/q">Escanear otro equipo</Link>
        </div>
      ) : eq.puedeReportar ? (
        <form className="qr-card" onSubmit={enviar}>
          <h2>Reportar una falla</h2>
          <p className="sub">Todos los campos son obligatorios.</p>
          <div className="fila-2">
            <label className="campo"><span className="campo-label">Nombre</span>
              <input value={form.nombre} onChange={set('nombre')} required maxLength={80} autoComplete="given-name" /></label>
            <label className="campo"><span className="campo-label">Apellido</span>
              <input value={form.apellido} onChange={set('apellido')} required maxLength={80} autoComplete="family-name" /></label>
          </div>
          <label className="campo"><span className="campo-label">Matrícula</span>
            <input value={form.matricula} onChange={set('matricula')} required maxLength={30} inputMode="text" /></label>
          <label className="campo"><span className="campo-label">¿Qué falla?</span>
            <textarea value={form.descripcionFalla} onChange={set('descripcionFalla')} required minLength={5} maxLength={2000} rows={4}
              placeholder="Ej.: alarma de presión alta permanente, no enciende, pantalla sin respuesta…" /></label>
          <Alerta>{errorForm}</Alerta>
          <button className="btn btn-peligro btn-bloque btn-grande" disabled={enviando}>{enviando ? 'Enviando…' : 'Enviar reporte de falla'}</button>
        </form>
      ) : (
        <div className="qr-card">
          <Alerta tono="info">{eq.motivoNoReportable}</Alerta>
        </div>
      )}
    </MarcoQr>
  );
}
