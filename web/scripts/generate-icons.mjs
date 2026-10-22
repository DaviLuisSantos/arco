// Gera os ícones PNG do PWA a partir dos SVGs em public/.
// Uso: npm run icons
import { readFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import sharp from 'sharp'

const pub = fileURLToPath(new URL('../public/', import.meta.url))

const targets = [
  { src: 'icon.svg', out: 'pwa-192x192.png', size: 192 },
  { src: 'icon.svg', out: 'pwa-512x512.png', size: 512 },
  { src: 'icon.svg', out: 'favicon-32x32.png', size: 32 },
  { src: 'icon-maskable.svg', out: 'maskable-512x512.png', size: 512 },
  // iOS não respeita transparência: usa a versão de fundo cheio.
  { src: 'icon-maskable.svg', out: 'apple-touch-icon.png', size: 180 },
]

for (const t of targets) {
  const svg = await readFile(pub + t.src)
  await sharp(svg, { density: 384 }).resize(t.size, t.size).png().toFile(pub + t.out)
  console.log(`✓ ${t.out} (${t.size}px)`)
}
