import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  build: {
    rollupOptions: {
      output: { manualChunks: { react: ['react', 'react-dom', 'react-router-dom'], graficos: ['chart.js', 'react-chartjs-2'], mapas: ['leaflet', 'react-leaflet'] } },
    },
  },
  server: {
    host: true,
    proxy: { '/api': { target: 'http://localhost:5080', changeOrigin: true } },
  },
});
