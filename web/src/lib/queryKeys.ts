export const queryKeys = {
  auth: ['auth'] as const,
  today: ['today'] as const,
  habits: ['habits'] as const,
  habit: (id: number) => ['habits', id] as const,
  cycles: ['cycles'] as const,
  currentCycle: ['cycles', 'current'] as const,
  goals: (cycleId: number) => ['goals', cycleId] as const,
  currentGoals: ['goals', 'current'] as const,
  player: ['player'] as const,
  settings: ['settings'] as const,
}
