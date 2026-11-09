export const HABIT_TEMPLATES = [
  { id: 'water', emoji: '💧' },
  { id: 'walk', emoji: '🚶' },
  { id: 'read', emoji: '📖' },
  { id: 'meditate', emoji: '🧘' },
  { id: 'workout', emoji: '💪' },
  { id: 'sleep', emoji: '😴' },
  { id: 'write', emoji: '✍️' },
  { id: 'study', emoji: '🎓' },
  { id: 'stretch', emoji: '🤸' },
  { id: 'tidy', emoji: '🧹' },
  { id: 'language', emoji: '🗣️' },
  { id: 'gratitude', emoji: '🙏' },
] as const

export type TemplateId = (typeof HABIT_TEMPLATES)[number]['id']

/** Atalhos do estado vazio de Hoje: um recorte variado do catálogo, sem sobrecarregar a tela. */
export const SHORTCUT_TEMPLATE_IDS: readonly TemplateId[] = ['water', 'walk', 'read', 'meditate', 'stretch', 'write']
