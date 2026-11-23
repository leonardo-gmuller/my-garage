import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import { existsSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const apiUrl = process.env.GARAGE_API_URL ?? 'http://127.0.0.1:5081'
const root = process.env.GARAGE_ROOT ?? fileURLToPath(new URL('../../', import.meta.url))

const saveiroAvailable=process.env.GARAGE_DISABLE_LOCAL_MODELS!=='true'&&existsSync(fileURLToPath(new URL('./public/models/Saveiro_3D.glb',import.meta.url)))&&existsSync(fileURLToPath(new URL('./public/models/Saveiro_3D_grupos.json',import.meta.url)))

export default defineConfig({
  define: { 'import.meta.env.VITE_SAVEIRO_MODEL_AVAILABLE': JSON.stringify(String(saveiroAvailable)) },
  plugins: [
    vue(), tailwindcss(),
    {
      name: 'garage-dev-identity',
      apply: 'serve',
      configureServer(server) {
        // Local tooling only: prevent reusing another project's Vite on this port.
        server.middlewares.use('/__garage_dev_identity', (_request, response) => {
          response.setHeader('Content-Type', 'application/json')
          response.end(JSON.stringify({ root: root.replace(/\/$/, ''), apiUrl }))
        })
      },
    },
  ],
  server: {
    host: '127.0.0.1',
    port: Number(process.env.GARAGE_VITE_PORT ?? 5175),
    strictPort: true,
    proxy: { '/api': apiUrl, '/health': apiUrl, '/openapi': apiUrl },
  },
  css: { devSourcemap: true },
  build: {
    sourcemap: true,
    rollupOptions: {
      output: { manualChunks: { three: ['three'], vue: ['vue', 'vue-router', 'pinia'], primevue: ['primevue/config', 'primevue/dialog', 'primevue/button'] } },
    },
  },
})
