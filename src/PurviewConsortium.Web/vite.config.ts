import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, '.', '');
  const buildTimestamp = new Date().toISOString();
  const deploymentTimestamp = env.VITE_DEPLOYED_AT || buildTimestamp;

  return {
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
  };
});
