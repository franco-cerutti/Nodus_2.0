import {
  ArcElement, BarElement, CategoryScale, Chart as ChartJS, Filler, Legend, LinearScale, LineElement, PointElement, Tooltip,
} from 'chart.js';

ChartJS.register(ArcElement, BarElement, CategoryScale, LinearScale, LineElement, PointElement, Filler, Legend, Tooltip);

export const C = {
  navy: '#14324f',
  navy2: '#2b5480',
  azulGris: '#6b8cae',
  gris: '#94a3b8',
  grisClaro: '#e2e8f0',
  texto: '#334155',
  verde: '#2e7d32',
  rojo: '#c62828',
  ambar: '#b26a00',
  teal: '#2a9d8f',
};

export const COLOR_ESTADO = {
  operativos: C.verde,
  fueraDeServicio: C.rojo,
  enMantenimiento: C.ambar,
  enPrestamo: C.azulGris,
};

export const SERIE = ['#14324f', '#2b5480', '#4f7aa6', '#86a7c8', '#b7c9dc', '#5f6f82'];

ChartJS.defaults.font.family = "'Inter', system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif";
ChartJS.defaults.font.size = 12;
ChartJS.defaults.color = '#64748b';
ChartJS.defaults.borderColor = '#eef2f6';
ChartJS.defaults.plugins.legend.labels.boxWidth = 10;
ChartJS.defaults.plugins.legend.labels.boxHeight = 10;
ChartJS.defaults.plugins.legend.labels.usePointStyle = true;
ChartJS.defaults.plugins.tooltip.backgroundColor = 'rgba(15, 31, 46, .94)';
ChartJS.defaults.plugins.tooltip.padding = 10;
ChartJS.defaults.plugins.tooltip.cornerRadius = 8;
ChartJS.defaults.plugins.tooltip.titleFont = { weight: 600 };
ChartJS.defaults.maintainAspectRatio = false;
