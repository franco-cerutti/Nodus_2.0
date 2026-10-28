/**
 * Logo institucional.
 * @param variante `isotipo` (símbolo cuadrado) o `completo` (símbolo + NODUS).
 * @param tamano alto en píxeles.
 * @param placa monta el logo sobre una placa blanca para fondos oscuros.
 */
export default function Logo({ variante = 'isotipo', tamano = 34, placa = false, className = '' }: {
  variante?: 'isotipo' | 'completo'; tamano?: number; placa?: boolean; className?: string;
}) {
  const img = (
    <img
      src={variante === 'completo' ? '/logo-completo.png' : '/logo.png'}
      alt="NODUS"
      height={tamano}
      style={{ height: tamano, width: 'auto' }}
      className={`logo logo-${variante} ${placa ? '' : className}`}
      draggable={false}
    />
  );
  return placa ? <span className={`logo-placa ${className}`} style={{ width: tamano + 8, height: tamano + 8 }}>{img}</span> : img;
}
