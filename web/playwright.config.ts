import { defineConfig, devices } from '@playwright/test'

// Portas configuráveis para permitir execuções simultâneas (ex.: dois worktrees).
const API_PORT = Number(process.env.E2E_API_PORT ?? 5199)
const WEB_PORT = Number(process.env.E2E_WEB_PORT ?? 4199)

export default defineConfig({
  testDir: './e2e',
  testMatch: /.*\.spec\.ts/,
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  timeout: 45_000,
  expect: { timeout: 10_000 },
  reporter: process.env.CI ? [['github'], ['list']] : 'list',
  use: {
    baseURL: `http://localhost:${WEB_PORT}`,
    trace: 'retain-on-failure',
    locale: 'pt-BR',
    // O service worker só atrapalharia o E2E (cache do shell entre testes).
    serviceWorkers: 'block',
  },
  projects: [
    { name: 'e2e', testIgnore: /screenshots\.spec\.ts/, use: { ...devices['Desktop Chrome'] } },
    {
      name: 'e2e-mobile',
      testIgnore: [/screenshots\.spec\.ts/, /public-profile\.spec\.ts/],
      use: { ...devices['Pixel 7'] },
    },
    { name: 'screenshots', testMatch: /screenshots\.spec\.ts/, use: { ...devices['Desktop Chrome'] } },
  ],
  webServer: [
    {
      // API publicada em Release, banco temporário, porta livre.
      command: 'node e2e/api-server.mjs',
      url: `http://localhost:${API_PORT}/api/health`,
      timeout: 300_000,
      reuseExistingServer: false,
      env: { E2E_API_PORT: String(API_PORT) },
      stdout: 'pipe',
      stderr: 'pipe',
      gracefulShutdown: { signal: 'SIGTERM', timeout: 5_000 },
    },
    {
      // Build do web + vite preview com proxy de /api para a API do E2E.
      command: `npx vite build && npx vite preview --port ${WEB_PORT} --strictPort`,
      url: `http://localhost:${WEB_PORT}/`,
      timeout: 300_000,
      reuseExistingServer: false,
      env: { ARCO_API_URL: `http://localhost:${API_PORT}`, VITE_SENTRY_DSN: '' },
    },
  ],
})
