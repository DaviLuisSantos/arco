# Arco — web

Frontend do Arco (PWA, pt-BR): Vite + React 19 + TypeScript + React Router 7 (modo data/SPA) + TanStack Query 5 + Tailwind CSS 4.

## Rodar

```bash
npm install
npm run dev       # http://localhost:5173 (proxy /api → http://localhost:5080)
npm run build     # tsc -b + vite build → dist/
npm run preview   # serve dist/ em http://localhost:4173 (com o mesmo proxy)
npm run lint      # oxlint (linter do template atual do Vite)
npm run icons     # regera os PNGs do PWA a partir de public/icon*.svg (sharp)
```

A API precisa estar rodando em `:5080` (`src/Arco.Api`). Em Development a senha é `arco`.

## Estrutura

- `src/api/` — `client.ts` (fetch com cookie, lança `ApiError{status, code, message}`), `endpoints.ts` (uma função por rota de `docs/API.md`), `types.ts` (espelho do contrato).
- `src/lib/` — datas sem fuso (`dates.ts`, nunca `new Date("YYYY-MM-DD")`), mensagens de erro pt-BR (`errors.ts`), formatação, chaves de query, `useAppMutation` (toast de erro + XP + revalidação).
- `src/features/` — peças por domínio: check-in otimista, hábitos, ciclo, objetivos, XP (toast "+N XP" e diálogo de subida de nível).
- `src/components/ui/` — primitivos (Button, Card, Input/Select/Textarea, Dialog/sheet, ProgressBar, Toast…).
- `src/pages/` — Hoje, Hábitos, Detalhe do hábito, Ciclo, Perfil, Login.

## PWA

`vite-plugin-pwa` com `generateSW` e `registerType: 'autoUpdate'`: só o app shell é pré-cacheado; `/api` nunca é cacheado e fica fora do `navigateFallback`.
Na Fase 2 (push), trocar para `strategies: 'injectManifest'` com um service worker próprio.
