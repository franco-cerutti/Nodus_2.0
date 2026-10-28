-- NODUS - Migración 002: bloqueo de OT por insumo faltante y parámetros del tablero de control.

ALTER TABLE orden_trabajo
    ADD COLUMN IF NOT EXISTS id_insumo_faltante INT NULL REFERENCES insumo(id_insumo);

COMMENT ON COLUMN orden_trabajo.id_insumo_faltante IS
    'OT detenida por falta de stock de este insumo (NULL = no bloqueada por stock)';

CREATE INDEX IF NOT EXISTS ix_ot_insumo_faltante
    ON orden_trabajo(id_insumo_faltante) WHERE id_insumo_faltante IS NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_ot_faltante_abierta') THEN
        ALTER TABLE orden_trabajo ADD CONSTRAINT chk_ot_faltante_abierta
            CHECK (estado_ot <> 'FINALIZADA' OR id_insumo_faltante IS NULL);
    END IF;
END $$;

INSERT INTO parametro_sistema (clave, valor) VALUES
    ('VIDA_UTIL_ANIOS', '7'),
    ('FAMILIAS_RESPIRADOR', 'EMED,VAFO,VNIV')
ON CONFLICT (clave) DO NOTHING;
