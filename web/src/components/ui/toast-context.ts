import { createContext, useContext } from 'react'

export type ToastVariant = 'info' | 'success' | 'error' | 'xp' | 'achievement'

export interface ToastOptions {
  message: string
  variant?: ToastVariant
}

export interface ToastApi {
  show: (options: ToastOptions) => void
  error: (message: string) => void
  success: (message: string) => void
  xp: (amount: number) => void
  achievement: (emoji: string, title: string) => void
}

export const ToastContext = createContext<ToastApi | null>(null)

export function useToast(): ToastApi {
  const ctx = useContext(ToastContext)
  if (!ctx) throw new Error('useToast precisa estar dentro de <ToastProvider>')
  return ctx
}
