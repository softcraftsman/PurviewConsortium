import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const buildTimestamp = new Date().toISOString();
const deploymentTimestamp = process.env.VITE_DEPLOYED_AT ?? buildTimestamp;

export default defineConfig({
  plugins: [react()],
  define: {
    __BUILD_TIMESTAMP__: JSON.stringify(buildTimestamp),
    __DEPLOY_TIMESTAMP__: JSON.stringify(deploymentTimestamp),
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5236',
        changeOrigin: true,
      },
    },
  },
});
