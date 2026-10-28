import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import type { ReactNode } from 'react';
import { ROLES_ADMIN, ROLES_MANT, ROLES_SECTOR, useAuth } from './auth/AuthContext';
import type { Rol } from './api/types';
import Layout from './components/Layout';
import { Cargando } from './components/ui';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import KanbanPage from './pages/Kanban';
import Equipos from './pages/Equipos';
import EquipoDetallePage from './pages/EquipoDetalle';
import QrEquipoPage, { QrInicio } from './pages/QrEquipo';
import Mantenimiento from './pages/Mantenimiento';
import OtDetallePage from './pages/OtDetalle';
import Insumos from './pages/Insumos';
import Prestamos from './pages/Prestamos';
import Alertas from './pages/Alertas';
import Admin from './pages/Admin';

function Protegida({ roles, children }: { roles?: Rol[]; children: ReactNode }) {
  const { usuario, cargando } = useAuth();
  const loc = useLocation();
  if (cargando) return <Cargando texto="Verificando sesión…" />;
  if (!usuario) return <Navigate to={`/login?volver=${encodeURIComponent(loc.pathname)}`} replace />;
  if (roles && !roles.includes(usuario.rol))
    return <Navigate to={usuario.rol === 'Personal Asistencial' ? '/q' : '/'} replace />;
  return <>{children}</>;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />

      <Route path="/q" element={<Protegida><QrInicio /></Protegida>} />
      <Route path="/q/:codigo" element={<Protegida><QrEquipoPage /></Protegida>} />

      <Route element={<Protegida roles={ROLES_SECTOR}><Layout /></Protegida>}>
        <Route index element={<Dashboard />} />
        <Route path="kanban" element={<KanbanPage />} />
        <Route path="equipos" element={<Equipos />} />
        <Route path="equipos/:id" element={<EquipoDetallePage />} />
        <Route path="mantenimiento" element={<Protegida roles={ROLES_MANT}><Mantenimiento /></Protegida>} />
        <Route path="ots/:id" element={<Protegida roles={ROLES_MANT}><OtDetallePage /></Protegida>} />
        <Route path="insumos" element={<Insumos />} />
        <Route path="prestamos" element={<Prestamos />} />
        <Route path="alertas" element={<Alertas />} />
        <Route path="admin" element={<Protegida roles={ROLES_ADMIN}><Admin /></Protegida>} />
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
