/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite';
import { tanstackRouter } from '@tanstack/router-plugin/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// Aspire passes PORT and API_URL; the defaults match a plain `pnpm dev` next to `dotnet run --project src/Bayubai.Api`.
const port = Number(process.env.PORT ?? 5174);
const api = process.env.API_URL ?? 'http://localhost:5042';

export default defineConfig({
  plugins: [tanstackRouter({ target: 'react', autoCodeSplitting: true }), react(), tailwindcss()],
  server: {
    port,
    strictPort: true,
    // Same-origin /api keeps the session cookie first-party in development; Host is kept so OAuth redirect URIs point back here.
    proxy: { '/api': { target: api, changeOrigin: false, secure: false } },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['@bayubai/ui/testing/setup'],
  },
});
