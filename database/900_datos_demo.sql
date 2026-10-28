-- NODUS - Datos de demostración. No ejecutar en producción.

BEGIN;

INSERT INTO equipo (codigo_patrimonial, id_familia, id_marca, modelo, nro_serie, fecha_adquisicion,
                    fecha_garantia_vencimiento, fecha_mantenimiento_preventivo, id_servicio_actual)
SELECT fn_generar_codigo_patrimonial(v.serv, v.fam), v.fam, v.marca, v.modelo, v.serie, v.adq,
       (v.adq + INTERVAL '1 year')::date, (v.adq + INTERVAL '1 year')::date + 365, v.serv
FROM (VALUES
    (1, 1, 1,  'Neumovent GraphNet TS', 'TEC-GN-1001', DATE '2023-04-12'),
    (1, 1, 6,  'Hamilton C1',           'HAM-C1-2207', DATE '2024-02-20'),
    (1, 3, 5,  'Cardiohelp',            'MAQ-CH-0091', DATE '2022-11-03'),
    (2, 2, 11, 'SLE6000',               'SLE-6K-3310', DATE '2023-08-30'),
    (2, 1, 12, 'Babylog VN500',         'DRG-VN-5521', DATE '2024-06-15'),
    (2, 6, 10, 'MR850',                 'FPH-850-778', DATE '2025-01-10'),
    (2, 7, 12, 'INOmax DSIR',           'INO-DS-0042', DATE '2023-03-01'),
    (3, 1, 3,  'SV300',                 'MDR-SV-4410', DATE '2024-09-05'),
    (3, 1, 2,  'Luft 3',                'LEI-L3-1180', DATE '2025-03-22'),
    (4, 4, 10, 'Airvo 2',               'FPH-AV2-901', DATE '2024-05-18'),
    (4, 5, 7,  'Stellar 150',           'RSM-ST-6620', DATE '2023-12-01'),
    (4, 1, 1,  'Neumovent Advance',     'TEC-AD-2290', DATE '2022-07-14')
) AS v(serv, fam, marca, modelo, serie, adq);

INSERT INTO insumo (nombre_insumo, categoria, stock_actual, stock_minimo) VALUES
('Filtro HEPA bacteriano/viral', 'Filtros', 40, 15),
('Sensor de flujo neonatal', 'Sensores', 4, 6),
('Celda de oxígeno', 'Sensores', 7, 4),
('Válvula espiratoria', 'Repuestos', 10, 4),
('Circuito paciente adulto', 'Circuitos', 25, 10),
('Cámara humidificadora', 'Humidificación', 3, 5);

INSERT INTO hospital_externo (nombre_hospital, direccion, latitud, longitud) VALUES
('Hospital de Niños de la Santísima Trinidad', 'Ferroviarios 1250, Córdoba', -31.40670000, -64.16220000),
('Hospital Misericordia Nuevo Siglo', 'Belgrano 1502, Córdoba', -31.42820000, -64.19550000),
('Hospital Rawson', 'Bajada Pucará 2025, Córdoba', -31.40330000, -64.16000000);

COMMIT;
