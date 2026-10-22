import { createContext, useContext } from 'react'

export interface XpFeedbackApi {
  /** Abre o diálogo comemorativo de subida de nível do jogador. */
  celebrateLevel: (level: number) => void
}

export const XpFeedbackContext = createContext<XpFeedbackApi | null>(null)

export function useXpFeedback(): XpFeedbackApi {
  const ctx = useContext(XpFeedbackContext)
  if (!ctx) throw new Error('useXpFeedback precisa estar dentro de <XpFeedbackProvider>')
  return ctx
}
