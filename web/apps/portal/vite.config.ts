import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

// In development, /api goes to a local gateway. In production nginx forwards /api to the gateway.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: { '/api': process.env.GATEWAY_URL ?? 'http://localhost:8080' },
  },
});
