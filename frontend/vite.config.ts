import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Hash-based asset fingerprinting is enabled by default in Vite production builds.
// Patterns made explicit here for documentation clarity (AC-001).
export default defineConfig({
  plugins: [react()],
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
