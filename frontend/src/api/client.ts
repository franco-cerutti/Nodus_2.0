const TOKEN_KEY = 'nodus.token';

export class ApiError extends Error {
  constructor(public status: number, message: string, public codigo?: string, public titulo?: string) {
    super(message);
  }
  /** Conflicto de concurrencia optimista (xmin) u otra regla de negocio que devuelve 409 */
  get esConflicto() { return this.status === 409; }
  get esConcurrencia() { return this.codigo === 'CONCURRENCIA'; }
}

export const tokenStore = {
  get: () => { try { return localStorage.getItem(TOKEN_KEY); } catch { return null; } },
  set: (t: string) => { try { localStorage.setItem(TOKEN_KEY, t); } catch { } },
  clear: () => { try { localStorage.removeItem(TOKEN_KEY); } catch { } },
};

let onUnauthorized: () => void = () => {};
export const setOnUnauthorized = (fn: () => void) => { onUnauthorized = fn; };

async function request(method: string, url: string, body?: unknown): Promise<Response> {
  const headers: Record<string, string> = {};
  const token = tokenStore.get();
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  let res: Response;
  try {
    res = await fetch(`/api${url}`, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  } catch {
    throw new ApiError(0, 'No se pudo conectar con el servidor. Revisá la red.');
  }

  if (res.status === 401 && token && url !== '/auth/login') {
    onUnauthorized();
    throw new ApiError(401, 'La sesión expiró. Volvé a iniciar sesión.');
  }
  if (!res.ok) {
    let msg = `Error ${res.status}`;
    let codigo: string | undefined;
    let titulo: string | undefined;
    try {
      const p = await res.json();
      msg = p.detail || p.title || msg;
      codigo = p.codigo;
      titulo = p.title;
    } catch { }
    if (res.status === 403) msg = 'No tenés permisos para esta acción.';
    throw new ApiError(res.status, msg, codigo, titulo);
  }
  return res;
}

async function json<T>(method: string, url: string, body?: unknown): Promise<T> {
  const res = await request(method, url, body);
  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

export const api = {
  get: <T>(url: string) => json<T>('GET', url),
  post: <T>(url: string, body?: unknown) => json<T>('POST', url, body ?? {}),
  put: <T>(url: string, body?: unknown) => json<T>('PUT', url, body ?? {}),
  del: <T>(url: string) => json<T>('DELETE', url),

  /** Descarga un archivo generado al vuelo (PDF / Excel) respetando el nombre que manda la API. */
  async download(url: string, fallbackName: string) {
    const res = await request('GET', url);
    const blob = await res.blob();
    const cd = res.headers.get('Content-Disposition') ?? '';
    const m = /filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i.exec(cd);
    const name = m ? decodeURIComponent(m[1] ?? m[2]) : fallbackName;
    const href = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = href;
    a.download = name;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(href), 10_000);
  },
};

export const qs = (params: Record<string, string | number | boolean | null | undefined>) => {
  const s = new URLSearchParams();
  Object.entries(params).forEach(([k, v]) => { if (v !== undefined && v !== null && v !== '') s.set(k, String(v)); });
  const str = s.toString();
  return str ? `?${str}` : '';
};
