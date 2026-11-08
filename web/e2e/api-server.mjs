// Sobe a API para o E2E: publica em Release (nunca em bin/Debug), usa banco temporário e
// escuta em E2E_API_PORT (padrão :5199). O Playwright encerra este processo ao fim; o filho e a
// pasta temporária são limpos junto.
import { execFileSync, spawn } from 'node:child_process'
import { mkdtempSync, readdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..')
const port = process.env.E2E_API_PORT ?? '5199'
// O Playwright pode encerrar este processo à força (Windows) sem passar pelo shutdown:
// limpa o que ficou de execuções anteriores nesta porta, sem tocar nas de outra execução simultânea.
const workPrefix = `arco-e2e-${port}-`
for (const name of readdirSync(tmpdir())) {
  if (!name.startsWith(workPrefix)) continue
  try {
    rmSync(join(tmpdir(), name), { recursive: true, force: true })
  } catch {
    // em uso: fica para a próxima
  }
}

const work = mkdtempSync(join(tmpdir(), workPrefix))
const publishDir = join(work, 'publish')

console.log(`[e2e-api] publicando em ${publishDir}`)
execFileSync(
  'dotnet',
  ['publish', join(root, 'src', 'Arco.Api', 'Arco.Api.csproj'), '-c', 'Release', '-o', publishDir, '--nologo', '-v', 'q'],
  { stdio: 'inherit' },
)

const child = spawn('dotnet', [join(publishDir, 'Arco.Api.dll')], {
  cwd: publishDir,
  stdio: 'inherit',
  env: {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: 'Development',
    ASPNETCORE_URLS: `http://localhost:${port}`,
    ConnectionStrings__Arco: `Data Source=${join(work, 'data', 'arco.db')}`,
    Auth__ActionsPerMinute: '1000',
    Auth__LoginAttemptsPerMinute: '1000',
    Push__SchedulerEnabled: 'false',
    Demo__CleanupEnabled: 'false',
  },
})

let closing = false
function shutdown(code = 0) {
  if (closing) return
  closing = true
  if (child.exitCode === null) child.kill()
  setTimeout(() => {
    try {
      rmSync(work, { recursive: true, force: true })
    } catch {
      // arquivo ainda aberto: o diretório fica no tmp do sistema
    }
    process.exit(code)
  }, 1500)
}

child.on('exit', (code) => shutdown(code ?? 0))
for (const sig of ['SIGINT', 'SIGTERM', 'SIGBREAK']) process.on(sig, () => shutdown(0))
