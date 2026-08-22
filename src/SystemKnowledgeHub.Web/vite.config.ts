import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    host: '0.0.0.0',
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5090',
        changeOrigin: true,
      },
      '/auth': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5090',
        changeOrigin: true,
      },
    },
  },
})
