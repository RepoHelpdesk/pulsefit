import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'

// The API serves the built app from its wwwroot, so the page and /api share one origin in production.
// In development Vite proxies /api to the API.
export default defineConfig({
  plugins: [vue(), tailwindcss()],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  server: { port: 5173, proxy: { '/api': 'http://localhost:5080' } },
  build: { outDir: '../src/PulseFit.Api/wwwroot', emptyOutDir: true },
})
