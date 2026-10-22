import { useQueryClient } from '@tanstack/react-query'
import { useCallback } from 'react'
import type { Player, PlayerDetail, Today, XpResult } from '../../api/types'
import { useToast } from '../../components/ui/toast-context'
import { queryKeys } from '../../lib/queryKeys'
import { useXpFeedback } from './xp-context'

/**
 * Aplica o resultado de uma mutação que devolve { xpDelta, player }:
 * atualiza o jogador em cache, mostra "+N XP" e comemora subida de nível.
 */
export function useApplyXpResult() {
  const qc = useQueryClient()
  const toast = useToast()
  const { celebrateLevel } = useXpFeedback()

  return useCallback(
    ({ xpDelta, player, achievements }: XpResult) => {
      const previous: Player | undefined =
        qc.getQueryData<Today>(queryKeys.today)?.player ?? qc.getQueryData<PlayerDetail>(queryKeys.player)

      qc.setQueryData<Today>(queryKeys.today, (d) => (d ? { ...d, player, xpToday: d.xpToday + xpDelta } : d))
      qc.setQueryData<PlayerDetail>(queryKeys.player, (d) => (d ? { ...d, ...player } : d))

      if (xpDelta !== 0) toast.xp(xpDelta)
      // Resposta de uma versão antiga da API pode vir sem o campo.
      for (const a of achievements ?? []) toast.achievement(a.emoji, a.title)
      if (achievements?.length) void qc.invalidateQueries({ queryKey: queryKeys.achievements })
      // Sem jogador em cache: subiu de nível se o XP ganho é maior que o acumulado no nível atual.
      const leveledUp = previous ? player.level > previous.level : xpDelta > player.xpIntoLevel
      if (leveledUp) celebrateLevel(player.level)
    },
    [qc, toast, celebrateLevel],
  )
}
