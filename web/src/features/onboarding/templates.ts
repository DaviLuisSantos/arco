export const HABIT_TEMPLATES = [
  { id: 'water', emoji: '💧' },
  { id: 'walk', emoji: '🚶' },
  { id: 'read', emoji: '📖' },
  { id: 'meditate', emoji: '🧘' },
  { id: 'workout', emoji: '💪' },
  { id: 'sleep', emoji: '😴' },
  { id: 'write', emoji: '✍️' },
  { id: 'study', emoji: '🎓' },
] as const

export type TemplateId = (typeof HABIT_TEMPLATES)[number]['id']
