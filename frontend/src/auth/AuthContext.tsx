import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api, setOnUnauthorized, tokenStore } from '../api/client';
import type { LoginResponse, Rol, UsuarioSesion } from '../api/types';

interface AuthState {
  usuario: UsuarioSesion | null;
  cargando: boolean;
  login: (nombreUsuario: string, password: string) => Promise<UsuarioSesion>;
  logout: () => void;
  tiene: (...roles: Rol[]) => boolean;
}

const Ctx = createContext<AuthState>(null!);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<UsuarioSesion | null>(null);
  const [cargando, setCargando] = useState(true);

  const logout = useCallback(() => {
    tokenStore.clear();
    setUsuario(null);
  }, []);

  useEffect(() => {
    setOnUnauthorized(logout);
    if (!tokenStore.get()) { setCargando(false); return; }
    api.get<UsuarioSesion>('/auth/me')
      .then(setUsuario)
      .catch(() => tokenStore.clear())
      .finally(() => setCargando(false));
  }, [logout]);

  const login = useCallback(async (nombreUsuario: string, password: string) => {
    const r = await api.post<LoginResponse>('/auth/login', { nombreUsuario, password });
    tokenStore.set(r.token);
    setUsuario(r.usuario);
    return r.usuario;
  }, []);

  const value = useMemo<AuthState>(() => ({
    usuario, cargando, login, logout,
    tiene: (...roles) => !!usuario && roles.includes(usuario.rol),
  }), [usuario, cargando, login, logout]);

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export const useAuth = () => useContext(Ctx);

export const ROLES_SECTOR: Rol[] = ['Bioingeniero', 'Técnico', 'Administrativo'];
export const ROLES_MANT: Rol[] = ['Bioingeniero', 'Técnico'];
export const ROLES_INSUMOS: Rol[] = ['Bioingeniero', 'Administrativo'];
export const ROLES_ADMIN: Rol[] = ['Bioingeniero', 'Administrativo'];
