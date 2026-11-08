import { currentLocale } from '../../i18n'

/** Títulos e descrições em inglês, pela chave do catálogo (o pt-BR vem da API, que é a fonte). */
const EN: Record<string, { title: string; description: string }> = {
  'streak-7': { title: 'A solid week', description: '7-day streak on a habit.' },
  'streak-30': { title: 'A month unbroken', description: '30-day streak on a habit.' },
  'streak-100': { title: 'One hundred', description: '100-day streak on a habit.' },
  'streak-weeks-4': { title: 'Four weeks', description: 'Weekly target hit 4 weeks in a row.' },
  'streak-weeks-12': { title: 'A quarter', description: 'Weekly target hit 12 weeks in a row.' },
  'checkins-1': { title: 'First step', description: 'First check-in with Done or Minimum.' },
  'checkins-100': { title: 'A hundred check-ins', description: '100 check-ins with Done or Minimum.' },
  'checkins-500': { title: 'Five hundred', description: '500 check-ins with Done or Minimum.' },
  'minimum-10': { title: 'Never zero', description: '10 days saved with the Minimum.' },
  'habit-level-2': { title: 'Dose up', description: 'A habit reached level 2.' },
  'habit-level-5': { title: 'Level 5', description: 'A habit reached level 5.' },
  'player-5': { title: 'Player level 5', description: 'Reached player level 5.' },
  'player-10': { title: 'Player level 10', description: 'Reached player level 10.' },
  'goal-1': { title: 'Goal achieved', description: 'First goal completed.' },
  'cycle-1': { title: 'Cycle closed', description: 'First cycle closed with a review.' },
  'reviews-4': { title: 'Four reviews', description: '4 weekly reviews done.' },
}

/** Texto da conquista no idioma atual; sem tradução, usa o da API. */
export function achievementText(a: { key: string; title: string; description: string }) {
  const en = currentLocale() === 'en' ? EN[a.key] : undefined
  return en ?? { title: a.title, description: a.description }
}
