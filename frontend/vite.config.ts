import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Hash-based asset fingerprinting is enabled by default in Vite production builds.
// Patterns made explicit here for documentation clarity (AC-001).
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // Mirror the nginx `location /api/` rule: strip the `/api` prefix before
      // forwarding to the ASP.NET Core Kestrel server (port 8080).
      // nginx: proxy_pass http://api:8080/; (trailing slash strips /api/)
      '/api': {
        target: 'http://localhost:8080',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
      // Forward SignalR negotiate (HTTP) and WebSocket upgrade to the backend.
      // No path rewriting needed — backend registers hubs at /hubs/*.
      '/hubs': {
        target: 'http://localhost:8080',
        changeOrigin: true,
        ws: true,
      },
    },
  },
  build: {
    rollupOptions: {
      output: {
        entryFileNames: 'assets/[name].[hash].js',
        chunkFileNames: 'assets/[name].[hash].js',
        assetFileNames: 'assets/[name].[hash][extname]',
      },
    },
  },
})
