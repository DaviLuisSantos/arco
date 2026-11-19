import type { TimeOfDay } from '../../api/types'

/** `timeOfDay` é o período sugerido para o modelo. */
export const HABIT_TEMPLATES = [
  { id: 'water', emoji: '💧', timeOfDay: 'Anytime' },
  { id: 'walk', emoji: '🚶', timeOfDay: 'Afternoon' },
  { id: 'read', emoji: '📖', timeOfDay: 'Evening' },
  { id: 'meditate', emoji: '🧘', timeOfDay: 'Morning' },
  { id: 'workout', emoji: '💪', timeOfDay: 'Morning' },
  { id: 'sleep', emoji: '😴', timeOfDay: 'Evening' },
  { id: 'write', emoji: '✍️', timeOfDay: 'Morning' },
  { id: 'study', emoji: '🎓', timeOfDay: 'Afternoon' },
  { id: 'stretch', emoji: '🤸', timeOfDay: 'Morning' },
  { id: 'tidy', emoji: '🧹', timeOfDay: 'Evening' },
  { id: 'language', emoji: '🗣️', timeOfDay: 'Afternoon' },
  { id: 'gratitude', emoji: '🙏', timeOfDay: 'Evening' },
] as const satisfies readonly { id: string; emoji: string; timeOfDay: TimeOfDay }[]

export type TemplateId = (typeof HABIT_TEMPLATES)[number]['id']

/** Atalhos do estado vazio de Hoje: um recorte variado do catálogo, sem sobrecarregar a tela. */
export const SHORTCUT_TEMPLATE_IDS: readonly TemplateId[] = ['water', 'walk', 'read', 'meditate', 'stretch', 'write']
