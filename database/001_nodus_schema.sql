-- =========================================================================
-- SISTEMA NODUS - SCRIPT DE BASE DE DATOS (POSTGRESQL) - v2 revisada
-- Hospital Materno Neonatal - Córdoba
--
-- Decisiones de diseño:
--  * Código patrimonial CONGELADO con el servicio de alta (nunca cambia).
--    El servicio actual vive en equipo.id_servicio_actual.
--  * OT termina en FINALIZADA y solo cierra con seguridad eléctrica APROBADA.
--  * Concurrencia optimista con la columna de sistema xmin (sin trigger).
--    En EF Core:  [Timestamp] public uint Version { get; set; }
--  * Préstamos RECIBIDOS no entran al inventario: se describen en el préstamo.
--  * Estados en MAYÚSCULAS ASCII con guion bajo (mapean 1:1 a enums de C#).
--  * Los PDF se generan al vuelo: no se guardan URLs de PDF.
--  * Errores de negocio con SQLSTATE propio (clase NS) para mapear a HTTP.
-- =========================================================================

DROP TABLE IF EXISTS alerta CASCADE;
DROP TABLE IF EXISTS parametro_sistema CASCADE;
DROP TABLE IF EXISTS prestamo_equipo CASCADE;
DROP TABLE IF EXISTS hospital_externo CASCADE;
DROP TABLE IF EXISTS solicitud_compra_insumo CASCADE;
DROP TABLE IF EXISTS reposicion_insumo CASCADE;
DROP TABLE IF EXISTS uso_insumo_ot CASCADE;
DROP TABLE IF EXISTS insumo CASCADE;
DROP TABLE IF EXISTS detalle_mantenimiento_tercerizado CASCADE;
DROP TABLE IF EXISTS orden_trabajo CASCADE;
DROP TABLE IF EXISTS reporte_falla CASCADE;
DROP TABLE IF EXISTS baja_patrimonial CASCADE;
DROP TABLE IF EXISTS historial_ubicacion CASCADE;
DROP TABLE IF EXISTS contador_patrimonial CASCADE;
DROP TABLE IF EXISTS equipo CASCADE;
DROP TABLE IF EXISTS marca CASCADE;
DROP TABLE IF EXISTS tipo_equipo CASCADE;
DROP TABLE IF EXISTS usuario CASCADE;
DROP TABLE IF EXISTS servicio CASCADE;
DROP TABLE IF EXISTS rol CASCADE;
DROP FUNCTION IF EXISTS update_updated_at_column() CASCADE;


-- =========================================================================
-- 1. USUARIOS, SEGURIDAD E INFRAESTRUCTURA (RBAC)
-- =========================================================================
CREATE TABLE rol (
    id_rol      SERIAL PRIMARY KEY,
    nombre_rol  VARCHAR(50) NOT NULL UNIQUE
);

INSERT INTO rol (nombre_rol) VALUES
('Bioingeniero'),
('Técnico'),
('Administrativo'),
('Personal Asistencial');

CREATE TABLE servicio (
    id_servicio      SERIAL PRIMARY KEY,
    nombre_servicio  VARCHAR(100) NOT NULL UNIQUE,
    codigo_servicio  VARCHAR(10)  NOT NULL UNIQUE
);

INSERT INTO servicio (nombre_servicio, codigo_servicio) VALUES
('UTI Adultos', 'UCI'),
('UTI Neonatal', 'UTIN'),
('Quirófano Central', 'QUIR'),
('Guardia Central', 'GUARD');

CREATE TABLE usuario (
    id_usuario       SERIAL PRIMARY KEY,
    nombre_usuario   VARCHAR(50)  NOT NULL UNIQUE,           -- login (ej: 'utin', 'fperez')
    legajo           VARCHAR(20)  NULL UNIQUE,               -- solo personal del sector
    nombre_completo  VARCHAR(100) NOT NULL,
    password_hash    VARCHAR(255) NOT NULL,                  -- BCrypt
    id_rol           INT NOT NULL REFERENCES rol(id_rol),
    id_servicio      INT NULL REFERENCES servicio(id_servicio), -- usuarios de servicio (QR)
    activo           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now()
);
-- Los usuarios de servicio se siembran desde la app (necesitan hash BCrypt).


-- =========================================================================
-- 2. GESTIÓN DE ACTIVOS
-- =========================================================================
CREATE TABLE tipo_equipo (
    id_familia      SERIAL PRIMARY KEY,
    nombre_familia  VARCHAR(100) NOT NULL UNIQUE,
    codigo_familia  VARCHAR(10)  NOT NULL UNIQUE
);

INSERT INTO tipo_equipo (nombre_familia, codigo_familia) VALUES
('Ventilador Mecánico', 'EMED'),
('VAFO', 'VAFO'),
('ECMO', 'ECMO'),
('Oxigenoterapia de Alto Flujo', 'OAF'),
('VNI', 'VNIV'),
('Base Humidificadora', 'BHUM'),
('Óxido Nítrico', 'ONIT');

CREATE TABLE marca (
    id_marca      SERIAL PRIMARY KEY,
    nombre_marca  VARCHAR(100) NOT NULL UNIQUE
);

-- Cardiohelp y Rotaflow son modelos de Maquet (Getinge), no marcas.
INSERT INTO marca (nombre_marca) VALUES
('TECME'), ('Leistung'), ('Mindray'), ('Philips Respironics'),
('Maquet'), ('Hamilton Medical'), ('ResMed'), ('BMC Medical'),
('Yuwell'), ('Fisher & Paykel'), ('SLE'), ('Dräger');

CREATE TABLE equipo (
    id_equipo                       SERIAL PRIMARY KEY,
    codigo_patrimonial              VARCHAR(50) NOT NULL UNIQUE,  -- congelado al alta
    id_familia                      INT NOT NULL REFERENCES tipo_equipo(id_familia),
    id_marca                        INT NOT NULL REFERENCES marca(id_marca),
    modelo                          VARCHAR(80) NOT NULL,
    nro_serie                       VARCHAR(80) NOT NULL UNIQUE,
    fecha_adquisicion               DATE NOT NULL,
    fecha_garantia_vencimiento      DATE NOT NULL,   -- adquisición + 1 año
    fecha_mantenimiento_preventivo  DATE NOT NULL,   -- garantía + 365 días; se recalcula al cerrar un preventivo
    id_servicio_actual              INT NOT NULL REFERENCES servicio(id_servicio),
    estado_operativo                VARCHAR(30) NOT NULL DEFAULT 'OPERATIVO',
    created_at                      TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_equipo_estado CHECK (estado_operativo IN
        ('OPERATIVO','FUERA_DE_SERVICIO','EN_MANTENIMIENTO','EN_PRESTAMO','BAJA_PATRIMONIAL')),
    CONSTRAINT chk_equipo_fechas CHECK (fecha_garantia_vencimiento >= fecha_adquisicion)
);
-- Sin qr_code_url: el QR se genera en el front a partir de la URL de la app + código patrimonial.
-- Sin updated_at: la concurrencia optimista usa xmin.

-- Correlativo por familia, generado de forma atómica (sin MAX()+1)
CREATE TABLE contador_patrimonial (
    id_familia  INT PRIMARY KEY REFERENCES tipo_equipo(id_familia),
    ultimo      INT NOT NULL
);

CREATE TABLE historial_ubicacion (
    id_historial_ubicacion  SERIAL PRIMARY KEY,
    id_equipo               INT NOT NULL REFERENCES equipo(id_equipo),
    id_servicio_origen      INT NOT NULL REFERENCES servicio(id_servicio),
    id_servicio_destino     INT NOT NULL REFERENCES servicio(id_servicio),
    fecha_hora_cambio       TIMESTAMPTZ NOT NULL DEFAULT now(),
    id_usuario              INT NOT NULL REFERENCES usuario(id_usuario),
    CONSTRAINT chk_historial_distinto CHECK (id_servicio_origen <> id_servicio_destino)
);

CREATE TABLE baja_patrimonial (
    id_baja                  SERIAL PRIMARY KEY,
    id_equipo                INT NOT NULL UNIQUE REFERENCES equipo(id_equipo),
    fecha_baja               DATE NOT NULL DEFAULT CURRENT_DATE,
    motivo_baja              VARCHAR(30) NOT NULL,
    observaciones            TEXT NULL,
    id_usuario_bioingeniero  INT NOT NULL REFERENCES usuario(id_usuario),
    CONSTRAINT chk_baja_motivo CHECK (motivo_baja IN
        ('OBSOLESCENCIA','IRREPARABILIDAD','COSTO_REPARACION_MAYOR_60'))
);


-- =========================================================================
-- 3. GESTIÓN DE MANTENIMIENTO
-- =========================================================================
CREATE TABLE reporte_falla (
    id_reporte             SERIAL PRIMARY KEY,
    id_equipo              INT NOT NULL REFERENCES equipo(id_equipo),
    id_usuario_sesion      INT NOT NULL REFERENCES usuario(id_usuario), -- usuario de servicio logueado
    nombre_solicitante     VARCHAR(80) NOT NULL,
    apellido_solicitante   VARCHAR(80) NOT NULL,
    matricula_solicitante  VARCHAR(30) NOT NULL,
    descripcion_falla      TEXT NOT NULL,
    fecha_hora_reporte     TIMESTAMPTZ NOT NULL DEFAULT now(),
    estado_reporte         VARCHAR(30) NOT NULL DEFAULT 'PENDIENTE',
    motivo_descarte        TEXT NULL,
    CONSTRAINT chk_reporte_estado CHECK (estado_reporte IN ('PENDIENTE','CONVERTIDO_EN_OT','DESCARTADO')),
    CONSTRAINT chk_reporte_descarte CHECK (estado_reporte <> 'DESCARTADO' OR motivo_descarte IS NOT NULL)
);

-- Regla: 1 solo reporte de falla activo por equipo
CREATE UNIQUE INDEX ux_reporte_activo_por_equipo
    ON reporte_falla(id_equipo) WHERE estado_reporte = 'PENDIENTE';

CREATE TABLE orden_trabajo (
    id_ot                              SERIAL PRIMARY KEY,
    id_equipo                          INT NOT NULL REFERENCES equipo(id_equipo),
    id_reporte_falla                   INT NULL UNIQUE REFERENCES reporte_falla(id_reporte),
    tipo_mantenimiento                 VARCHAR(30) NOT NULL,
    estado_ot                          VARCHAR(30) NOT NULL DEFAULT 'PENDIENTE',
    fecha_apertura                     TIMESTAMPTZ NOT NULL DEFAULT now(),
    fecha_inicio_taller                TIMESTAMPTZ NULL,   -- pasa a EN_CURSO
    fecha_cierre                       TIMESTAMPTZ NULL,   -- pasa a FINALIZADA
    descripcion_tarea_tecnica          TEXT NULL,
    resultado_seguridad_electrica      VARCHAR(30) NOT NULL DEFAULT 'PENDIENTE',
    observaciones_seguridad_electrica  TEXT NULL,
    id_tecnico_asignado                INT NULL REFERENCES usuario(id_usuario),
    id_usuario_creador                 INT NOT NULL REFERENCES usuario(id_usuario),
    CONSTRAINT chk_ot_tipo CHECK (tipo_mantenimiento IN ('CORRECTIVO','PREVENTIVO','TERCERIZADO')),
    CONSTRAINT chk_ot_estado CHECK (estado_ot IN ('PENDIENTE','EN_CURSO','FINALIZADA')),
    CONSTRAINT chk_ot_seg_elec CHECK (resultado_seguridad_electrica IN ('PENDIENTE','APROBADA','NO_APROBADA')),
    -- Seguridad eléctrica BLOQUEANTE
    CONSTRAINT chk_ot_cierre CHECK (estado_ot <> 'FINALIZADA'
        OR (resultado_seguridad_electrica = 'APROBADA' AND fecha_cierre IS NOT NULL)),
    CONSTRAINT chk_ot_en_curso CHECK (estado_ot = 'PENDIENTE' OR fecha_inicio_taller IS NOT NULL),
    CONSTRAINT chk_ot_fechas CHECK (fecha_cierre IS NULL OR fecha_cierre >= fecha_apertura)
);

-- Regla: 1 sola OT abierta por equipo
CREATE UNIQUE INDEX ux_ot_abierta_por_equipo
    ON orden_trabajo(id_equipo) WHERE estado_ot <> 'FINALIZADA';

CREATE TABLE detalle_mantenimiento_tercerizado (
    id_tercerizado              SERIAL PRIMARY KEY,
    id_ot                       INT NOT NULL UNIQUE REFERENCES orden_trabajo(id_ot),
    proveedor_fabrica_nombre    VARCHAR(150) NOT NULL,
    en_garantia                 BOOLEAN NOT NULL DEFAULT FALSE,
    fecha_envio                 DATE NOT NULL,
    fecha_probable_vuelta       DATE NOT NULL,   -- envío + DIAS_RETORNO_TERCERIZADO
    fecha_vuelta_real           DATE NULL,
    remito_entrada_descripcion  TEXT NULL,
    CONSTRAINT chk_terc_fechas CHECK (fecha_probable_vuelta >= fecha_envio
        AND (fecha_vuelta_real IS NULL OR fecha_vuelta_real >= fecha_envio))
);


-- =========================================================================
-- 4. GESTIÓN DE INSUMOS
-- =========================================================================
CREATE TABLE insumo (
    id_insumo      SERIAL PRIMARY KEY,
    nombre_insumo  VARCHAR(100) NOT NULL UNIQUE,
    categoria      VARCHAR(50)  NOT NULL,
    stock_actual   INT NOT NULL DEFAULT 0,
    stock_minimo   INT NOT NULL,
    estado_stock   VARCHAR(30) GENERATED ALWAYS AS (
        CASE WHEN stock_actual = 0            THEN 'AGOTADO'
             WHEN stock_actual < stock_minimo THEN 'STOCK_BAJO'
             ELSE 'STOCK_NORMAL' END) STORED,
    -- Hard stop a nivel base: el stock jamás queda negativo
    CONSTRAINT chk_insumo_stock CHECK (stock_actual >= 0 AND stock_minimo >= 0)
);

CREATE TABLE uso_insumo_ot (
    id_uso              SERIAL PRIMARY KEY,
    id_ot               INT NOT NULL REFERENCES orden_trabajo(id_ot),
    id_insumo           INT NOT NULL REFERENCES insumo(id_insumo),
    cantidad_consumida  INT NOT NULL,
    fecha_uso           TIMESTAMPTZ NOT NULL DEFAULT now(),
    id_usuario          INT NOT NULL REFERENCES usuario(id_usuario),
    CONSTRAINT chk_uso_cantidad CHECK (cantidad_consumida > 0)
);

CREATE TABLE reposicion_insumo (
    id_reposicion       SERIAL PRIMARY KEY,
    id_insumo           INT NOT NULL REFERENCES insumo(id_insumo),
    cantidad_ingresada  INT NOT NULL,
    fecha_reposicion    TIMESTAMPTZ NOT NULL DEFAULT now(),
    id_usuario          INT NOT NULL REFERENCES usuario(id_usuario),
    CONSTRAINT chk_repo_cantidad CHECK (cantidad_ingresada > 0)
);

CREATE TABLE solicitud_compra_insumo (
    id_solicitud        SERIAL PRIMARY KEY,
    id_insumo           INT NOT NULL REFERENCES insumo(id_insumo),
    cantidad_a_comprar  INT NOT NULL,
    fecha_solicitud     TIMESTAMPTZ NOT NULL DEFAULT now(),
    id_usuario          INT NOT NULL REFERENCES usuario(id_usuario),
    CONSTRAINT chk_compra_cantidad CHECK (cantidad_a_comprar > 0)
);


-- =========================================================================
-- 5. PRÉSTAMOS INTERINSTITUCIONALES
-- =========================================================================
CREATE TABLE hospital_externo (
    id_hospital_externo  SERIAL PRIMARY KEY,
    nombre_hospital      VARCHAR(150) NOT NULL UNIQUE,
    direccion            VARCHAR(200) NULL,
    latitud              NUMERIC(10,8) NOT NULL,
    longitud             NUMERIC(11,8) NOT NULL,
    CONSTRAINT chk_hosp_coords CHECK (latitud BETWEEN -90 AND 90 AND longitud BETWEEN -180 AND 180)
);

CREATE TABLE prestamo_equipo (
    id_prestamo                 SERIAL PRIMARY KEY,
    tipo_direccion              VARCHAR(20) NOT NULL,                       -- OTORGADO / RECIBIDO
    id_equipo                   INT NULL REFERENCES equipo(id_equipo),      -- solo OTORGADO
    marca_externa               VARCHAR(100) NULL,                          -- solo RECIBIDO
    modelo_externo              VARCHAR(80)  NULL,
    nro_serie_externo           VARCHAR(80)  NULL,
    id_hospital_externo         INT NOT NULL REFERENCES hospital_externo(id_hospital_externo),
    responsable_retiro_entrega  VARCHAR(100) NOT NULL,
    fecha_salida                DATE NOT NULL,
    fecha_devolucion_estimada   DATE NOT NULL,   -- una prórroga la actualiza
    fecha_devolucion_real       DATE NULL,
    estado_prestamo             VARCHAR(20) NOT NULL DEFAULT 'EN_PRESTAMO',
    estado_tecnico_entrega      TEXT NOT NULL,
    estado_tecnico_recepcion    TEXT NULL,
    accesorios_prestados        TEXT NULL,
    id_usuario                  INT NOT NULL REFERENCES usuario(id_usuario),
    CONSTRAINT chk_prest_tipo CHECK (tipo_direccion IN ('OTORGADO','RECIBIDO')),
    CONSTRAINT chk_prest_estado CHECK (estado_prestamo IN ('EN_PRESTAMO','VENCIDO','PRORROGA','FINALIZADO')),
    CONSTRAINT chk_prest_equipo CHECK (
        (tipo_direccion = 'OTORGADO' AND id_equipo IS NOT NULL
            AND marca_externa IS NULL AND modelo_externo IS NULL AND nro_serie_externo IS NULL)
     OR (tipo_direccion = 'RECIBIDO' AND id_equipo IS NULL
            AND marca_externa IS NOT NULL AND modelo_externo IS NOT NULL)),
    CONSTRAINT chk_prest_fechas CHECK (fecha_devolucion_estimada >= fecha_salida
        AND (fecha_devolucion_real IS NULL OR fecha_devolucion_real >= fecha_salida)),
    CONSTRAINT chk_prest_finalizado CHECK (estado_prestamo <> 'FINALIZADO'
        OR (fecha_devolucion_real IS NOT NULL AND estado_tecnico_recepcion IS NOT NULL))
);

-- Regla: un equipo propio no puede estar en dos préstamos activos
CREATE UNIQUE INDEX ux_prestamo_activo_por_equipo
    ON prestamo_equipo(id_equipo) WHERE estado_prestamo <> 'FINALIZADO' AND id_equipo IS NOT NULL;


-- =========================================================================
-- 6. PARÁMETROS Y ALERTAS
-- =========================================================================
CREATE TABLE parametro_sistema (
    clave  VARCHAR(50) PRIMARY KEY,
    valor  VARCHAR(100) NOT NULL
);

INSERT INTO parametro_sistema (clave, valor) VALUES
('CODIGO_HOSPITAL', 'HMN'),
('DIAS_ALERTA_PREVENTIVO', '15'),
('UMBRAL_DISPONIBILIDAD_FLOTA_CRITICA', '70'),
('DIAS_RETORNO_TERCERIZADO', '45');

-- Alertas generadas por el BackgroundService diario
CREATE TABLE alerta (
    id_alerta         SERIAL PRIMARY KEY,
    tipo_alerta       VARCHAR(30) NOT NULL,
    id_referencia     INT NOT NULL,      -- id_equipo / id_prestamo / id_tercerizado / id_insumo según tipo
    mensaje           TEXT NOT NULL,
    fecha_generacion  TIMESTAMPTZ NOT NULL DEFAULT now(),
    leida             BOOLEAN NOT NULL DEFAULT FALSE,
    resuelta          BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT chk_alerta_tipo CHECK (tipo_alerta IN
        ('PREVENTIVO_PROXIMO','PRESTAMO_VENCIDO','TERCERIZADO_VENCIDO','STOCK_BAJO'))
);

-- Evita duplicar la misma alerta en cada corrida diaria (usar INSERT ... ON CONFLICT DO NOTHING)
CREATE UNIQUE INDEX ux_alerta_abierta
    ON alerta(tipo_alerta, id_referencia) WHERE resuelta = FALSE;


-- =========================================================================
-- 7. ÍNDICES SOBRE FKs (Postgres no los crea solo)
-- =========================================================================
CREATE INDEX ix_usuario_rol            ON usuario(id_rol);
CREATE INDEX ix_equipo_servicio        ON equipo(id_servicio_actual);
CREATE INDEX ix_equipo_familia         ON equipo(id_familia);
CREATE INDEX ix_equipo_estado          ON equipo(estado_operativo);
CREATE INDEX ix_historial_equipo       ON historial_ubicacion(id_equipo, fecha_hora_cambio DESC);
CREATE INDEX ix_reporte_equipo         ON reporte_falla(id_equipo);
CREATE INDEX ix_ot_equipo              ON orden_trabajo(id_equipo);
CREATE INDEX ix_ot_tecnico             ON orden_trabajo(id_tecnico_asignado);
CREATE INDEX ix_ot_apertura            ON orden_trabajo(fecha_apertura);   -- mapa de calor / consolidado mensual
CREATE INDEX ix_uso_ot                 ON uso_insumo_ot(id_ot);
CREATE INDEX ix_uso_insumo             ON uso_insumo_ot(id_insumo);
CREATE INDEX ix_reposicion_insumo      ON reposicion_insumo(id_insumo);
CREATE INDEX ix_prestamo_hospital      ON prestamo_equipo(id_hospital_externo);


-- =========================================================================
-- 8. FUNCIONES Y TRIGGERS DE NEGOCIO
-- =========================================================================

-- 8.1 Generación atómica del código patrimonial: HMN-UCI-EMED-0042
CREATE OR REPLACE FUNCTION fn_generar_codigo_patrimonial(p_id_servicio INT, p_id_familia INT)
RETURNS VARCHAR AS $$
DECLARE
    v_hosp  VARCHAR;
    v_serv  VARCHAR;
    v_fam   VARCHAR;
    v_nro   INT;
BEGIN
    SELECT valor INTO v_hosp FROM parametro_sistema WHERE clave = 'CODIGO_HOSPITAL';
    SELECT codigo_servicio INTO v_serv FROM servicio WHERE id_servicio = p_id_servicio;
    SELECT codigo_familia INTO v_fam FROM tipo_equipo WHERE id_familia = p_id_familia;

    IF v_serv IS NULL OR v_fam IS NULL THEN
        RAISE EXCEPTION 'Servicio o familia inexistente' USING ERRCODE = 'NS000';
    END IF;

    -- El upsert bloquea la fila del contador: dos altas simultáneas nunca obtienen el mismo número
    INSERT INTO contador_patrimonial (id_familia, ultimo) VALUES (p_id_familia, 1)
    ON CONFLICT (id_familia) DO UPDATE SET ultimo = contador_patrimonial.ultimo + 1
    RETURNING ultimo INTO v_nro;

    RETURN v_hosp || '-' || v_serv || '-' || v_fam || '-' || LPAD(v_nro::TEXT, 4, '0');
END;
$$ LANGUAGE plpgsql;


-- 8.2 Guardas sobre equipo: baja inmutable y bloqueo de traslados
CREATE OR REPLACE FUNCTION fn_guarda_equipo()
RETURNS TRIGGER AS $$
BEGIN
    IF OLD.estado_operativo = 'BAJA_PATRIMONIAL' THEN
        RAISE EXCEPTION 'El equipo % está dado de baja y no admite modificaciones', OLD.codigo_patrimonial
            USING ERRCODE = 'NS001';
    END IF;

    IF NEW.codigo_patrimonial <> OLD.codigo_patrimonial THEN
        RAISE EXCEPTION 'El código patrimonial no puede modificarse' USING ERRCODE = 'NS002';
    END IF;

    IF NEW.id_servicio_actual <> OLD.id_servicio_actual
       AND OLD.estado_operativo IN ('EN_MANTENIMIENTO','EN_PRESTAMO') THEN
        RAISE EXCEPTION 'El equipo % está % y no puede trasladarse', OLD.codigo_patrimonial, OLD.estado_operativo
            USING ERRCODE = 'NS003';
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_guarda_equipo
    BEFORE UPDATE ON equipo
    FOR EACH ROW EXECUTE FUNCTION fn_guarda_equipo();

CREATE OR REPLACE FUNCTION fn_bloquear_delete_equipo()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'Los equipos no se eliminan: se dan de baja patrimonial' USING ERRCODE = 'NS001';
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_bloquear_delete_equipo
    BEFORE DELETE ON equipo
    FOR EACH ROW EXECUTE FUNCTION fn_bloquear_delete_equipo();


-- 8.3 Ningún movimiento nuevo sobre un equipo dado de baja
CREATE OR REPLACE FUNCTION fn_validar_equipo_no_baja()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.id_equipo IS NOT NULL AND EXISTS (
        SELECT 1 FROM equipo WHERE id_equipo = NEW.id_equipo AND estado_operativo = 'BAJA_PATRIMONIAL'
    ) THEN
        RAISE EXCEPTION 'El equipo está dado de baja patrimonial' USING ERRCODE = 'NS001';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_reporte_no_baja  BEFORE INSERT ON reporte_falla
    FOR EACH ROW EXECUTE FUNCTION fn_validar_equipo_no_baja();
CREATE TRIGGER trg_ot_no_baja       BEFORE INSERT ON orden_trabajo
    FOR EACH ROW EXECUTE FUNCTION fn_validar_equipo_no_baja();
CREATE TRIGGER trg_prestamo_no_baja BEFORE INSERT ON prestamo_equipo
    FOR EACH ROW EXECUTE FUNCTION fn_validar_equipo_no_baja();
CREATE TRIGGER trg_historial_no_baja BEFORE INSERT ON historial_ubicacion
    FOR EACH ROW EXECUTE FUNCTION fn_validar_equipo_no_baja();


-- 8.3b Reporte de falla: no se acepta si el equipo ya tiene una falla en curso (OT abierta)
CREATE OR REPLACE FUNCTION fn_reporte_validar()
RETURNS TRIGGER AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM orden_trabajo WHERE id_equipo = NEW.id_equipo AND estado_ot <> 'FINALIZADA') THEN
        RAISE EXCEPTION 'El equipo ya tiene una falla en tratamiento (OT abierta)' USING ERRCODE = 'NS009';
    END IF;
    IF EXISTS (SELECT 1 FROM equipo WHERE id_equipo = NEW.id_equipo AND estado_operativo = 'EN_PRESTAMO') THEN
        RAISE EXCEPTION 'El equipo está prestado a otra institución' USING ERRCODE = 'NS009';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_reporte_validar BEFORE INSERT ON reporte_falla
    FOR EACH ROW EXECUTE FUNCTION fn_reporte_validar();


-- 8.4 Baja patrimonial: precondiciones, cierre del equipo e inmutabilidad
CREATE OR REPLACE FUNCTION fn_baja_antes()
RETURNS TRIGGER AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM orden_trabajo WHERE id_equipo = NEW.id_equipo AND estado_ot <> 'FINALIZADA') THEN
        RAISE EXCEPTION 'No se puede dar de baja: el equipo tiene una OT abierta' USING ERRCODE = 'NS004';
    END IF;
    IF EXISTS (SELECT 1 FROM prestamo_equipo WHERE id_equipo = NEW.id_equipo AND estado_prestamo <> 'FINALIZADO') THEN
        RAISE EXCEPTION 'No se puede dar de baja: el equipo está prestado' USING ERRCODE = 'NS004';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION fn_baja_despues()
RETURNS TRIGGER AS $$
BEGIN
    UPDATE equipo SET estado_operativo = 'BAJA_PATRIMONIAL' WHERE id_equipo = NEW.id_equipo;
    UPDATE alerta SET resuelta = TRUE
     WHERE tipo_alerta = 'PREVENTIVO_PROXIMO' AND id_referencia = NEW.id_equipo AND resuelta = FALSE;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION fn_baja_inmutable()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'La baja patrimonial es inmutable' USING ERRCODE = 'NS001';
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_baja_antes     BEFORE INSERT ON baja_patrimonial
    FOR EACH ROW EXECUTE FUNCTION fn_baja_antes();
CREATE TRIGGER trg_baja_despues   AFTER INSERT ON baja_patrimonial
    FOR EACH ROW EXECUTE FUNCTION fn_baja_despues();
CREATE TRIGGER trg_baja_inmutable BEFORE UPDATE OR DELETE ON baja_patrimonial
    FOR EACH ROW EXECUTE FUNCTION fn_baja_inmutable();


-- 8.5 Cascada de estados OT → equipo
--   OT CORRECTIVA creada      → equipo FUERA_DE_SERVICIO
--   OT pasa a EN_CURSO        → equipo EN_MANTENIMIENTO
--   OT pasa a FINALIZADA      → equipo OPERATIVO (+ recalcula preventivo si corresponde)
CREATE OR REPLACE FUNCTION fn_ot_validar_transicion()
RETURNS TRIGGER AS $$
BEGIN
    IF OLD.estado_ot = 'FINALIZADA' THEN
        RAISE EXCEPTION 'La OT % ya está finalizada', OLD.id_ot USING ERRCODE = 'NS005';
    END IF;
    IF OLD.estado_ot = 'EN_CURSO' AND NEW.estado_ot = 'PENDIENTE' THEN
        RAISE EXCEPTION 'Una OT en curso no puede volver a pendiente' USING ERRCODE = 'NS005';
    END IF;
    IF NEW.id_equipo <> OLD.id_equipo THEN
        RAISE EXCEPTION 'No se puede cambiar el equipo de una OT' USING ERRCODE = 'NS005';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_ot_validar_transicion
    BEFORE UPDATE ON orden_trabajo
    FOR EACH ROW EXECUTE FUNCTION fn_ot_validar_transicion();

CREATE OR REPLACE FUNCTION fn_ot_cascada_estado()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        IF NEW.tipo_mantenimiento = 'CORRECTIVO' THEN
            UPDATE equipo SET estado_operativo = 'FUERA_DE_SERVICIO' WHERE id_equipo = NEW.id_equipo;
        END IF;
        IF NEW.id_reporte_falla IS NOT NULL THEN
            UPDATE reporte_falla SET estado_reporte = 'CONVERTIDO_EN_OT' WHERE id_reporte = NEW.id_reporte_falla;
        END IF;
        IF NEW.estado_ot = 'EN_CURSO' THEN
            UPDATE equipo SET estado_operativo = 'EN_MANTENIMIENTO' WHERE id_equipo = NEW.id_equipo;
        END IF;
        RETURN NEW;
    END IF;

    IF NEW.estado_ot IS DISTINCT FROM OLD.estado_ot THEN
        IF NEW.estado_ot = 'EN_CURSO' THEN
            UPDATE equipo SET estado_operativo = 'EN_MANTENIMIENTO' WHERE id_equipo = NEW.id_equipo;
        ELSIF NEW.estado_ot = 'FINALIZADA' THEN
            UPDATE equipo
               SET estado_operativo = 'OPERATIVO',
                   fecha_mantenimiento_preventivo = CASE
                       WHEN NEW.tipo_mantenimiento = 'PREVENTIVO'
                       THEN (NEW.fecha_cierre AT TIME ZONE 'America/Argentina/Cordoba')::DATE + 365
                       ELSE fecha_mantenimiento_preventivo END
             WHERE id_equipo = NEW.id_equipo;
            IF NEW.tipo_mantenimiento = 'PREVENTIVO' THEN
                UPDATE alerta SET resuelta = TRUE
                 WHERE tipo_alerta = 'PREVENTIVO_PROXIMO' AND id_referencia = NEW.id_equipo AND resuelta = FALSE;
            END IF;
        END IF;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_ot_cascada_estado
    AFTER INSERT OR UPDATE ON orden_trabajo
    FOR EACH ROW EXECUTE FUNCTION fn_ot_cascada_estado();


-- 8.6 Insumos: descuento en tiempo real con HARD STOP
CREATE OR REPLACE FUNCTION fn_uso_insumo()
RETURNS TRIGGER AS $$
DECLARE
    v_estado_ot  VARCHAR;
    v_nombre     VARCHAR;
    v_stock      INT;
BEGIN
    IF TG_OP = 'UPDATE' THEN
        RAISE EXCEPTION 'Un consumo registrado no se modifica: eliminarlo y cargarlo de nuevo' USING ERRCODE = 'NS006';
    END IF;

    SELECT estado_ot INTO v_estado_ot FROM orden_trabajo
     WHERE id_ot = COALESCE(NEW.id_ot, OLD.id_ot);
    IF v_estado_ot = 'FINALIZADA' THEN
        RAISE EXCEPTION 'La OT está finalizada: no se pueden modificar sus consumos' USING ERRCODE = 'NS005';
    END IF;

    IF TG_OP = 'INSERT' THEN
        -- UPDATE condicional: atómico frente a consumos simultáneos
        UPDATE insumo SET stock_actual = stock_actual - NEW.cantidad_consumida
         WHERE id_insumo = NEW.id_insumo AND stock_actual >= NEW.cantidad_consumida;
        IF NOT FOUND THEN
            SELECT nombre_insumo, stock_actual INTO v_nombre, v_stock FROM insumo WHERE id_insumo = NEW.id_insumo;
            RAISE EXCEPTION 'Stock insuficiente de "%": disponible %, solicitado %',
                v_nombre, v_stock, NEW.cantidad_consumida USING ERRCODE = 'NS007';
        END IF;
        RETURN NEW;
    END IF;

    -- DELETE (OT todavía abierta): devuelve el stock
    UPDATE insumo SET stock_actual = stock_actual + OLD.cantidad_consumida WHERE id_insumo = OLD.id_insumo;
    RETURN OLD;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_uso_insumo
    BEFORE INSERT OR UPDATE OR DELETE ON uso_insumo_ot
    FOR EACH ROW EXECUTE FUNCTION fn_uso_insumo();

CREATE OR REPLACE FUNCTION fn_reposicion_insumo()
RETURNS TRIGGER AS $$
BEGIN
    UPDATE insumo SET stock_actual = stock_actual + NEW.cantidad_ingresada WHERE id_insumo = NEW.id_insumo;
    -- Si volvió a stock normal, la alerta se resuelve sola
    UPDATE alerta a SET resuelta = TRUE
      FROM insumo i
     WHERE a.tipo_alerta = 'STOCK_BAJO' AND a.id_referencia = i.id_insumo
       AND i.id_insumo = NEW.id_insumo AND i.estado_stock = 'STOCK_NORMAL' AND a.resuelta = FALSE;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_reposicion_insumo
    AFTER INSERT ON reposicion_insumo
    FOR EACH ROW EXECUTE FUNCTION fn_reposicion_insumo();

-- Alerta inmediata de stock bajo (no espera a la corrida diaria)
CREATE OR REPLACE FUNCTION fn_alerta_stock_bajo()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.estado_stock IN ('STOCK_BAJO','AGOTADO')
       AND (TG_OP = 'INSERT' OR OLD.estado_stock = 'STOCK_NORMAL') THEN
        INSERT INTO alerta (tipo_alerta, id_referencia, mensaje)
        VALUES ('STOCK_BAJO', NEW.id_insumo,
                format('Insumo "%s" con stock %s (mínimo %s)', NEW.nombre_insumo, NEW.stock_actual, NEW.stock_minimo))
        ON CONFLICT DO NOTHING;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_alerta_stock_bajo
    AFTER INSERT OR UPDATE OF stock_actual, stock_minimo ON insumo
    FOR EACH ROW EXECUTE FUNCTION fn_alerta_stock_bajo();


-- 8.7 Préstamos otorgados → estado del equipo
CREATE OR REPLACE FUNCTION fn_prestamo_validar()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.tipo_direccion = 'OTORGADO' AND NOT EXISTS (
        SELECT 1 FROM equipo WHERE id_equipo = NEW.id_equipo AND estado_operativo = 'OPERATIVO'
    ) THEN
        RAISE EXCEPTION 'Solo se pueden prestar equipos en estado OPERATIVO' USING ERRCODE = 'NS008';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_prestamo_validar
    BEFORE INSERT ON prestamo_equipo
    FOR EACH ROW EXECUTE FUNCTION fn_prestamo_validar();

CREATE OR REPLACE FUNCTION fn_prestamo_cascada()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'UPDATE' AND OLD.estado_prestamo = 'FINALIZADO' THEN
        RAISE EXCEPTION 'El préstamo ya está finalizado' USING ERRCODE = 'NS008';
    END IF;

    IF NEW.tipo_direccion = 'OTORGADO' THEN
        IF TG_OP = 'INSERT' THEN
            UPDATE equipo SET estado_operativo = 'EN_PRESTAMO' WHERE id_equipo = NEW.id_equipo;
        ELSIF NEW.estado_prestamo = 'FINALIZADO' THEN
            UPDATE equipo SET estado_operativo = 'OPERATIVO' WHERE id_equipo = NEW.id_equipo;
        END IF;
    END IF;

    IF TG_OP = 'UPDATE' AND NEW.estado_prestamo IN ('PRORROGA','FINALIZADO') THEN
        UPDATE alerta SET resuelta = TRUE
         WHERE tipo_alerta = 'PRESTAMO_VENCIDO' AND id_referencia = NEW.id_prestamo AND resuelta = FALSE;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_prestamo_cascada
    AFTER INSERT OR UPDATE ON prestamo_equipo
    FOR EACH ROW EXECUTE FUNCTION fn_prestamo_cascada();
