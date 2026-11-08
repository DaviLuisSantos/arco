import { createHmac } from 'node:crypto'

const ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'

function base32Decode(input: string): Buffer {
  let bits = 0
  let value = 0
  const out: number[] = []
  for (const ch of input.replace(/[=\s]/g, '').toUpperCase()) {
    const idx = ALPHABET.indexOf(ch)
    if (idx < 0) throw new Error(`Base32 inválido: ${ch}`)
    value = (value << 5) | idx
    bits += 5
    if (bits >= 8) {
      out.push((value >>> (bits - 8)) & 0xff)
      bits -= 8
    }
  }
  return Buffer.from(out)
}

/** Código TOTP (RFC 6238: SHA-1, 6 dígitos, 30 s) do passo `offset` em relação ao atual. */
export function totp(secret: string, offset = 0, now = Date.now()): string {
  const counter = Math.floor(now / 1000 / 30) + offset
  const buf = Buffer.alloc(8)
  buf.writeBigUInt64BE(BigInt(counter))
  const hmac = createHmac('sha1', base32Decode(secret)).update(buf).digest()
  const o = hmac[hmac.length - 1] & 0x0f
  const bin = ((hmac[o] & 0x7f) << 24) | (hmac[o + 1] << 16) | (hmac[o + 2] << 8) | hmac[o + 3]
  return String(bin % 1_000_000).padStart(6, '0')
}
