/** Geocodificación con Nominatim (OpenStreetMap). Máximo una consulta por segundo, solo a pedido del usuario. */
export interface ResultadoGeo { lat: number; lng: number; nombre: string; tipo: string }

const VIEWBOX = '-65.9,-29.4,-61.7,-35.1';
let ultimaConsulta = 0;

export async function geocodificar(direccion: string): Promise<ResultadoGeo[]> {
  const q = direccion.trim();
  if (q.length < 4) throw new Error('Escribí una dirección más completa (calle, número y ciudad).');

  const espera = 1100 - (Date.now() - ultimaConsulta);
  if (espera > 0) await new Promise(r => setTimeout(r, espera));
  ultimaConsulta = Date.now();

  const url = new URL('https://nominatim.openstreetmap.org/search');
  url.search = new URLSearchParams({
    q: /córdoba|cordoba|argentina/i.test(q) ? q : `${q}, Córdoba, Argentina`,
    format: 'jsonv2', limit: '5', countrycodes: 'ar', 'accept-language': 'es', viewbox: VIEWBOX, addressdetails: '0',
  }).toString();

  let res: Response;
  try {
    res = await fetch(url, { headers: { Accept: 'application/json' } });
  } catch {
    throw new Error('No se pudo consultar el servicio de mapas. Revisá la conexión a internet o ubicá el punto a mano en el mapa.');
  }
  if (!res.ok) throw new Error(`El servicio de mapas respondió ${res.status}. Probá de nuevo en unos segundos.`);

  const datos: { lat: string; lon: string; display_name: string; type: string }[] = await res.json();
  return datos.map(d => ({ lat: Number(Number(d.lat).toFixed(6)), lng: Number(Number(d.lon).toFixed(6)), nombre: d.display_name, tipo: d.type }));
}

/** Distancia en metros entre dos coordenadas (fórmula de haversine) */
export function distanciaMetros(a: [number, number], b: [number, number]) {
  const R = 6371000, rad = Math.PI / 180;
  const dLat = (b[0] - a[0]) * rad, dLng = (b[1] - a[1]) * rad;
  const h = Math.sin(dLat / 2) ** 2 + Math.cos(a[0] * rad) * Math.cos(b[0] * rad) * Math.sin(dLng / 2) ** 2;
  return 2 * R * Math.asin(Math.sqrt(h));
}

export const fDistancia = (m: number) => m < 1000 ? `${Math.round(m)} m` : `${(m / 1000).toFixed(1)} km`;
