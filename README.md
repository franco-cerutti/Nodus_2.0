# NODUS — Gestión Patrimonial y Mantenimiento de Activos de Soporte Ventilatorio

Sector de Bioingeniería · Hospital Materno Neonatal de Córdoba.

```
nodus/
├── database/
│   ├── 001_nodus_schema.sql
│   ├── 002_dashboard_bloqueos.sql
│   ├── 003_siglas_servicios_alerta_fallas.sql
│   └── 900_datos_demo.sql
├── backend/Nodus.Api/          .NET 8 Web API · EF Core (Npgsql) · JWT · QuestPDF · ClosedXML
└── frontend/                   React 18 · TypeScript · Vite · Chart.js · Leaflet
```

## Instalación

### Base de datos (PostgreSQL)

```bash
createdb nodus
psql -d nodus -f database/001_nodus_schema.sql
psql -d nodus -f database/002_dashboard_bloqueos.sql
psql -d nodus -f database/003_siglas_servicios_alerta_fallas.sql
psql -d nodus -f database/900_datos_demo.sql        # opcional
```

Las migraciones 002 y 003 son idempotentes.

### API

Configurar en `backend/Nodus.Api/appsettings.json` (o variables de entorno):

| Clave | Descripción |
|---|---|
| `ConnectionStrings:Nodus` | Cadena de conexión a PostgreSQL. Contraseñas con `;` o comillas van entre comillas simples. |
| `Jwt:Key` | Clave de firma, mínimo 32 caracteres. |
| `Jwt:ExpirationHours` | Vigencia del token. |
| `Seed:AdminPassword` / `Seed:PasswordServicios` | Contraseñas iniciales. |
| `Alertas:HoraEjecucion` | Hora local de la corrida diaria de alertas. |
| `Cors:Origins` | Orígenes permitidos del frontend. |

```bash
cd backend/Nodus.Api
dotnet run
```

Usuarios creados al iniciar: `bioingenieria` (Bioingeniero) y `uti_adultos`, `uti_neonatal`, `quirofano`, `guardia` (Personal Asistencial).

### Frontend

```bash
cd frontend
npm install
npm run dev
```

`VITE_APP_PUBLIC_URL` (archivo `.env`) define la URL codificada en los QR de los equipos.

Para publicar en un solo servidor: `npm run build` y copiar `frontend/dist/*` a `backend/Nodus.Api/wwwroot/`.

### Logo institucional

| Archivo | Uso |
|---|---|
| `frontend/public/logo.png` | Isotipo: menú lateral, barra móvil, vista QR, favicon |
| `frontend/public/logo-completo.png` | Isotipo + NODUS: login, membrete del PDF del tablero |
| `backend/Nodus.Api/Recursos/logo.png` | Isotipo + NODUS: encabezado de PDF y Excel generados por la API |

## Siglas patrimoniales

Formato `[HOSPITAL]-[SERVICIO]-[FAMILIA]-[CORRELATIVO]`, por ejemplo `HMN-UTIA-EMED-0042`.

| Servicio | Sigla |
|---|---|
| UTI Adultos | `UTIA` |
| UTI Neonatal | `UTIN` |
| Quirófano Central | `QUIR` |
| Guardia Central | `GUARD` |

## API

| Módulo | Endpoints |
|---|---|
| Auth | `POST /api/auth/login`, `GET /api/auth/me` |
| Activos | `GET/POST /api/equipos`, `GET /api/equipos/{id}`, `GET /api/equipos/kanban`, `PUT /api/equipos/{id}/ubicacion`, `POST /api/equipos/{id}/baja` |
| QR | `GET /api/qr/{codigo}`, `POST /api/qr/{codigo}/reportes` |
| Mantenimiento | `/api/reportes-falla` (`novedades`, `convertir`, `descartar`), `/api/ots` (`iniciar`, `seguridad-electrica`, `finalizar`, `tercerizar`, `tercerizado/retorno`, `consumos`, `espera-insumo`, `pdf`, `remito-tercerizado`) |
| Insumos | `/api/insumos` (`reposiciones`, `solicitudes-compra`, `movimientos`) |
| Préstamos | `/api/prestamos` (`otorgados`, `recibidos`, `prorroga`, `finalizar`, `remito`, `nota`), `/api/hospitales-externos` |
| Reportes | `GET /api/reportes/dashboard`, `GET /api/reportes/dashboard/excel`, `GET /api/reportes/consolidado?anio=&mes=&formato=pdf\|xlsx` |
| Alertas | `GET /api/alertas`, `PUT /api/alertas/{id}/leida` |
| Administración | `/api/admin/usuarios`, `/api/admin/roles`, `/api/admin/parametros` |

## Parámetros del sistema

| Clave | Valor inicial |
|---|---|
| `CODIGO_HOSPITAL` | `HMN` |
| `DIAS_ALERTA_PREVENTIVO` | `15` |
| `UMBRAL_DISPONIBILIDAD_FLOTA_CRITICA` | `70` |
| `DIAS_RETORNO_TERCERIZADO` | `45` |
| `VIDA_UTIL_ANIOS` | `7` |
| `FAMILIAS_RESPIRADOR` | `EMED,VAFO,VNIV` |

## Indicadores del tablero

| Indicador | Alcance | Cálculo |
|---|---|---|
| Disponibilidad | Tiempo real | Equipos `OPERATIVO` / equipos no dados de baja |
| Respiradores libres | Tiempo real | Equipos `OPERATIVO` de las familias de `FAMILIAS_RESPIRADOR` |
| OT bloqueadas | Tiempo real | Stock: `id_insumo_faltante` informado · Seguridad: OT en curso con prueba `NO_APROBADA` |
| Tareas del día | Tiempo real | Preventivos con vencimiento ≤ 24 h, préstamos que vencen en ≤ 7 días, tercerizados con retorno ≤ 7 días |
| Fallas por marca | Período | OT correctivas fechadas por su reporte de falla o apertura |
| MTTR (días) | Período | Correctivo: reporte → cierre · Preventivo: apertura → cierre |
| Parque tecnológico | Histórico | Equipos en servicio al cierre de cada año; excedidos: antigüedad > `VIDA_UTIL_ANIOS` |
| Consumo de recursos | Período | Unidades de insumos y OT tercerizadas por tipo de equipo |
| Mapa de calor | 12 meses / período | Fallas por tipo y mes; fallas por día de semana y hora |

## Exportación

- **Tablero (PDF):** documento A4 con membrete, número de documento, filtros y firmas, generado desde el navegador.
- **Tablero (Excel):** una hoja por indicador con los filtros aplicados.
- **Consolidado General Mensual:** PDF y Excel generados por la API.
