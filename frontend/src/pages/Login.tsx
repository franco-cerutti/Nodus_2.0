import { useState, type FormEvent } from 'react';
import { Navigate, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { Alerta } from '../components/ui';
import { errMsg } from '../util';
import Logo from '../components/Logo';

export default function Login() {
  const { usuario, login } = useAuth();
  const [params] = useSearchParams();
  const nav = useNavigate();
  const volver = params.get('volver') || '';
  const desdeQr = volver.startsWith('/q');

  const [u, setU] = useState('');
  const [p, setP] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  const destino = (rol: string) =>
    rol === 'Personal Asistencial' ? (desdeQr ? volver : '/q') : (volver || '/');

  if (usuario) return <Navigate to={destino(usuario.rol)} replace />;

  const enviar = async (e: FormEvent) => {
    e.preventDefault();
    setEnviando(true);
    setError(null);
    try {
      const us = await login(u, p);
      nav(destino(us.rol), { replace: true });
    } catch (ex) {
      setError(errMsg(ex));
    } finally {
      setEnviando(false);
    }
  };

  return (
    <div className="login">
      <form className="login-card" onSubmit={enviar}>
        <div className="login-marca">
          <Logo variante="completo" tamano={72} />
          <div className="marca-sub">Bioingeniería · Hospital Materno Neonatal</div>
        </div>
        {desdeQr && (
          <Alerta tono="info">
            Para ver el equipo y reportar una falla, ingresá con el usuario de tu servicio.
          </Alerta>
        )}
        <label className="campo">
          <span className="campo-label">Usuario</span>
          <input autoFocus autoCapitalize="none" autoComplete="username" value={u} onChange={e => setU(e.target.value)} required />
        </label>
        <label className="campo">
          <span className="campo-label">Contraseña</span>
          <input type="password" autoComplete="current-password" value={p} onChange={e => setP(e.target.value)} required />
        </label>
        <Alerta>{error}</Alerta>
        <button className="btn btn-primario btn-bloque" disabled={enviando}>{enviando ? 'Ingresando…' : 'Ingresar'}</button>
        <p className="login-pie">Gestión patrimonial y mantenimiento de equipos de soporte ventilatorio</p>
      </form>
    </div>
  );
}
