import { useState } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import { ROLES_ADMIN, ROLES_MANT, ROLES_SECTOR, useAuth } from '../auth/AuthContext';
import type { Rol } from '../api/types';
import AlarmaFallas, { useNovedadesFallas } from './AlarmaFallas';
import Logo from './Logo';

type ItemMenu = { to: string; texto: string; icono: string; roles: Rol[]; insignia?: 'fallas' };

const MENU: ItemMenu[] = [
  { to: '/equipos', texto: 'Activos', icono: 'fa-boxes-stacked', roles: ROLES_SECTOR },
  { to: '/kanban', texto: 'Ubicación', icono: 'fa-table-columns', roles: ROLES_SECTOR },
  { to: '/mantenimiento', texto: 'Mantenimiento', icono: 'fa-screwdriver-wrench', roles: ROLES_MANT, insignia: 'fallas' },
  { to: '/alertas', texto: 'Alertas', icono: 'fa-bell', roles: ROLES_SECTOR, insignia: 'fallas' },
  { to: '/prestamos', texto: 'Préstamos', icono: 'fa-right-left', roles: ROLES_SECTOR },
  { to: '/insumos', texto: 'Insumos', icono: 'fa-box-open', roles: ROLES_SECTOR },
  { to: '/admin', texto: 'Administración', icono: 'fa-user-gear', roles: ROLES_ADMIN },
  { to: '/', texto: 'Dashboard', icono: 'fa-chart-pie', roles: ROLES_SECTOR },
];

export default function Layout() {
  const { usuario, logout } = useAuth();
  const [abierto, setAbierto] = useState(false);
  const gestionaFallas = !!usuario && ROLES_MANT.includes(usuario.rol);
  const { pendientes, nuevos, reconocer } = useNovedadesFallas(usuario?.id ?? 0, gestionaFallas);
  if (!usuario) return null;

  return (
    <div className="app">
      <aside className={`lateral ${abierto ? 'abierto' : ''}`}>
        <div className="marca">
          <Logo tamano={32} placa />
          <div>
            <div className="marca-nombre">NODUS</div>
            <div className="marca-sub">Bioingeniería · HMN</div>
          </div>
        </div>
        <nav onClick={() => setAbierto(false)}>
          {MENU.filter(n => n.roles.includes(usuario.rol)).map(n => (
            <NavLink key={n.to} to={n.to} end={n.to === '/'} className={({ isActive }) => `nav-item ${isActive ? 'activo' : ''}`}>
              <i className={`fa-solid ${n.icono} nav-icono`} aria-hidden />
              <span className="nav-texto">{n.texto}</span>
              {n.insignia === 'fallas' && gestionaFallas && pendientes > 0 && (
                <span className={`nav-insignia ${nuevos.length ? 'nueva' : ''}`} title={`${pendientes} reportes de falla pendientes`}>{pendientes}</span>
              )}
            </NavLink>
          ))}
        </nav>
        <div className="sesion">
          <div className="sesion-nombre">{usuario.nombreCompleto}</div>
          <div className="sesion-rol">{usuario.rol}</div>
          <button className="btn btn-ghost btn-sm" onClick={logout}><i className="fa-solid fa-arrow-right-from-bracket" /> Cerrar sesión</button>
        </div>
      </aside>
      <div className="principal">
        <header className="barra-movil">
          <button className="icono" onClick={() => setAbierto(a => !a)} aria-label="Menú"><i className="fa-solid fa-bars" /></button>
          <Logo tamano={22} placa />
          <span className="marca-nombre">NODUS</span>
          {gestionaFallas && pendientes > 0 && <span className="nav-insignia nueva barra-insignia">{pendientes}</span>}
        </header>
        <main className="contenido">
          <Outlet />
        </main>
      </div>
      {abierto && <div className="velo" onClick={() => setAbierto(false)} />}
      <AlarmaFallas nuevos={nuevos} onReconocer={reconocer} />
    </div>
  );
}
