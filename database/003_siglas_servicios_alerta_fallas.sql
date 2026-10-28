-- NODUS - Migración 003: siglas patrimoniales UTIA/UTIN/QUIR/GUARD y alerta REPORTE_FALLA.

BEGIN;

UPDATE servicio SET codigo_servicio = 'UTIA'  WHERE nombre_servicio = 'UTI Adultos'       AND codigo_servicio <> 'UTIA';
UPDATE servicio SET codigo_servicio = 'UTIN'  WHERE nombre_servicio = 'UTI Neonatal'      AND codigo_servicio <> 'UTIN';
UPDATE servicio SET codigo_servicio = 'QUIR'  WHERE nombre_servicio = 'Quirófano Central' AND codigo_servicio <> 'QUIR';
UPDATE servicio SET codigo_servicio = 'GUARD' WHERE nombre_servicio = 'Guardia Central'   AND codigo_servicio <> 'GUARD';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_servicio_sigla') THEN
        ALTER TABLE servicio ADD CONSTRAINT chk_servicio_sigla CHECK (
            (nombre_servicio, codigo_servicio) IN (
                ('UTI Adultos', 'UTIA'),
                ('UTI Neonatal', 'UTIN'),
                ('Quirófano Central', 'QUIR'),
                ('Guardia Central', 'GUARD')));
    END IF;
END $$;

ALTER TABLE equipo DISABLE TRIGGER trg_guarda_equipo;

UPDATE equipo
   SET codigo_patrimonial = regexp_replace(codigo_patrimonial, '^([^-]+)-UCI-', '\1-UTIA-')
 WHERE codigo_patrimonial ~ '^[^-]+-UCI-';

ALTER TABLE equipo ENABLE TRIGGER trg_guarda_equipo;

UPDATE alerta
   SET mensaje = regexp_replace(mensaje, '([A-Z0-9]+)-UCI-', '\1-UTIA-', 'g')
 WHERE mensaje ~ '[A-Z0-9]+-UCI-';

CREATE OR REPLACE FUNCTION fn_sigla_servicio(p_nombre VARCHAR)
RETURNS VARCHAR AS $$
    SELECT CASE p_nombre
        WHEN 'UTI Adultos'       THEN 'UTIA'
        WHEN 'UTI Neonatal'      THEN 'UTIN'
        WHEN 'Quirófano Central' THEN 'QUIR'
        WHEN 'Guardia Central'   THEN 'GUARD'
    END;
$$ LANGUAGE sql IMMUTABLE;

CREATE OR REPLACE FUNCTION fn_generar_codigo_patrimonial(p_id_servicio INT, p_id_familia INT)
RETURNS VARCHAR AS $$
DECLARE
    v_hosp  VARCHAR;
    v_serv  VARCHAR;
    v_fam   VARCHAR;
    v_nro   INT;
BEGIN
    SELECT valor INTO v_hosp FROM parametro_sistema WHERE clave = 'CODIGO_HOSPITAL';
    SELECT fn_sigla_servicio(nombre_servicio) INTO v_serv FROM servicio WHERE id_servicio = p_id_servicio;
    SELECT codigo_familia INTO v_fam FROM tipo_equipo WHERE id_familia = p_id_familia;

    IF v_serv IS NULL OR v_fam IS NULL THEN
        RAISE EXCEPTION 'Servicio sin sigla asignada o familia inexistente' USING ERRCODE = 'NS000';
    END IF;

    INSERT INTO contador_patrimonial (id_familia, ultimo) VALUES (p_id_familia, 1)
    ON CONFLICT (id_familia) DO UPDATE SET ultimo = contador_patrimonial.ultimo + 1
    RETURNING ultimo INTO v_nro;

    RETURN v_hosp || '-' || v_serv || '-' || v_fam || '-' || LPAD(v_nro::TEXT, 4, '0');
END;
$$ LANGUAGE plpgsql;

ALTER TABLE alerta DROP CONSTRAINT IF EXISTS chk_alerta_tipo;
ALTER TABLE alerta ADD CONSTRAINT chk_alerta_tipo CHECK (tipo_alerta IN
    ('PREVENTIVO_PROXIMO', 'PRESTAMO_VENCIDO', 'TERCERIZADO_VENCIDO', 'STOCK_BAJO', 'REPORTE_FALLA'));

CREATE OR REPLACE FUNCTION fn_alerta_reporte_falla()
RETURNS TRIGGER AS $$
DECLARE
    v_codigo   VARCHAR;
    v_servicio VARCHAR;
BEGIN
    IF TG_OP = 'INSERT' AND NEW.estado_reporte = 'PENDIENTE' THEN
        SELECT e.codigo_patrimonial, s.nombre_servicio INTO v_codigo, v_servicio
          FROM equipo e JOIN servicio s ON s.id_servicio = e.id_servicio_actual
         WHERE e.id_equipo = NEW.id_equipo;

        INSERT INTO alerta (tipo_alerta, id_referencia, mensaje)
        VALUES ('REPORTE_FALLA', NEW.id_reporte,
                format('Falla reportada en %s (%s) por %s %s: %s',
                       v_codigo, v_servicio, NEW.nombre_solicitante, NEW.apellido_solicitante, left(NEW.descripcion_falla, 160)))
        ON CONFLICT DO NOTHING;
    ELSIF TG_OP = 'UPDATE' AND NEW.estado_reporte <> 'PENDIENTE' THEN
        UPDATE alerta SET resuelta = TRUE
         WHERE tipo_alerta = 'REPORTE_FALLA' AND id_referencia = NEW.id_reporte AND resuelta = FALSE;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_alerta_reporte_falla ON reporte_falla;
CREATE TRIGGER trg_alerta_reporte_falla
    AFTER INSERT OR UPDATE OF estado_reporte ON reporte_falla
    FOR EACH ROW EXECUTE FUNCTION fn_alerta_reporte_falla();

INSERT INTO alerta (tipo_alerta, id_referencia, mensaje)
SELECT 'REPORTE_FALLA', r.id_reporte,
       format('Falla reportada en %s (%s) por %s %s: %s',
              e.codigo_patrimonial, s.nombre_servicio, r.nombre_solicitante, r.apellido_solicitante, left(r.descripcion_falla, 160))
  FROM reporte_falla r
  JOIN equipo e ON e.id_equipo = r.id_equipo
  JOIN servicio s ON s.id_servicio = e.id_servicio_actual
 WHERE r.estado_reporte = 'PENDIENTE'
ON CONFLICT DO NOTHING;

COMMIT;
