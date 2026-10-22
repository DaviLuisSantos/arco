import { Sparkles } from 'lucide-react'
import type { Player } from '../../api/types'
import { ProgressBar } from '../../components/ui/ProgressBar'
import { formatNumber, formatXp } from '../../lib/format'

interface PlayerProgressProps {
  player: Player
  xpToday?: number
}

export function PlayerProgress({ player, xpToday }: PlayerProgressProps) {
  const ratio = player.xpForNextLevel > 0 ? player.xpIntoLevel / player.xpForNextLevel : 0
  return (
    <div className="flex items-center gap-4">
      <div
        className="grid size-14 shrink-0 place-items-center rounded-2xl bg-gradient-to-br from-accent-strong to-xp text-bg shadow"
        aria-label={`Nível ${player.level}`}
      >
        <span className="text-[0.6rem] font-bold uppercase leading-none tracking-wider opacity-80">Nível</span>
        <span className="-mt-2 text-2xl font-extrabold leading-none">{player.level}</span>
      </div>
      <div className="min-w-0 flex-1">
        <div className="mb-1.5 flex items-baseline justify-between gap-2 text-sm">
          <span className="font-semibold">
            {formatNumber(player.xpIntoLevel)}
            <span className="text-muted"> / {formatNumber(player.xpForNextLevel)} XP</span>
          </span>
          {xpToday !== undefined && (
            <span className="inline-flex items-center gap-1 font-semibold text-xp">
              <Sparkles className="size-3.5" aria-hidden />
              {xpToday > 0 ? `${formatXp(xpToday)} hoje` : '0 XP hoje'}
            </span>
          )}
        </div>
        <ProgressBar value={ratio} tone="xp" label={`Progresso para o nível ${player.level + 1}`} />
        <p className="mt-1 text-xs text-muted">
          Faltam {formatNumber(Math.max(0, player.xpForNextLevel - player.xpIntoLevel))} XP para o nível {player.level + 1}
        </p>
      </div>
    </div>
  )
}
