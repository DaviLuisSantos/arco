import { api, ApiError } from './client'
import type {
  Achievement,
  CheckIn,
  CheckInInput,
  CheckInResult,
  Cycle,
  CycleCompleteInput,
  CycleCompleteResult,
  CycleSummary,
  CycleInput,
  CycleUpdateInput,
  Goal,
  GoalInput,
  GoalProgressInput,
  GoalResult,
  GoalUpdateInput,
  Habit,
  HabitDetail,
  HabitInput,
  HabitLevelUpResult,
  Health,
  LevelUpInput,
  PlayerDetail,
  PushConfig,
  Settings,
  SettingsInput,
  Today,
  WeeklyReview,
  WeeklyReviewInput,
  WeeklyReviewResult,
  WeeklySummary,
  XpResult,
} from './types'

export const authApi = {
  login: (password: string) => api.post<void>('/auth/login', { password }),
  /** 401 (sem cookie) também conta como sessão encerrada. */
  logout: async (): Promise<void> => {
    try {
      await api.post<void>('/auth/logout')
    } catch (e) {
      if (!(e instanceof ApiError && e.status === 401)) throw e
    }
  },
  /** true quando há sessão válida; 401 vira false. */
  me: async (): Promise<boolean> => {
    try {
      await api.get<{ authenticated: boolean }>('/auth/me')
      return true
    } catch (e) {
      if (e instanceof ApiError && e.status === 401) return false
      throw e
    }
  },
}

export const healthApi = {
  get: () => api.get<Health>('/health'),
}

export const todayApi = {
  get: () => api.get<Today>('/today'),
}

export const checkInsApi = {
  upsert: (input: CheckInInput) => api.put<CheckInResult>('/checkins', input),
  remove: (habitId: number, date: string) => api.delete<XpResult>(`/checkins/${habitId}/${date}`),
  list: (from: string, to: string) =>
    api.get<CheckIn[]>(`/checkins?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`),
}

export const habitsApi = {
  list: (includeArchived = false) => api.get<Habit[]>(`/habits?includeArchived=${includeArchived}`),
  get: (id: number) => api.get<HabitDetail>(`/habits/${id}`),
  create: (input: HabitInput) => api.post<Habit>('/habits', input),
  update: (id: number, input: HabitInput) => api.put<Habit>(`/habits/${id}`, input),
  levelUp: (id: number, input: LevelUpInput) => api.post<HabitLevelUpResult>(`/habits/${id}/level-up`, input),
  dismissLevelUpSuggestion: (id: number) => api.post<void>(`/habits/${id}/level-up-suggestion/dismiss`),
  archive: (id: number) => api.post<Habit>(`/habits/${id}/archive`),
  restore: (id: number) => api.post<Habit>(`/habits/${id}/restore`),
}

export const cyclesApi = {
  list: () => api.get<Cycle[]>('/cycles'),
  /** null quando não há ciclo ativo (204). */
  current: async (): Promise<Cycle | null> => (await api.get<Cycle | undefined>('/cycles/current')) ?? null,
  create: (input: CycleInput) => api.post<Cycle>('/cycles', input),
  update: (id: number, input: CycleUpdateInput) => api.put<Cycle>(`/cycles/${id}`, input),
  summary: (id: number) => api.get<CycleSummary>(`/cycles/${id}/summary`),
  complete: (id: number, input: CycleCompleteInput) => api.post<CycleCompleteResult>(`/cycles/${id}/complete`, input),
}

export const goalsApi = {
  list: (cycleId?: number) => api.get<Goal[]>(cycleId ? `/goals?cycleId=${cycleId}` : '/goals'),
  create: (input: GoalInput) => api.post<Goal>('/goals', input),
  update: (id: number, input: GoalUpdateInput) => api.put<Goal>(`/goals/${id}`, input),
  remove: (id: number) => api.delete<void>(`/goals/${id}`),
  progress: (id: number, input: GoalProgressInput) => api.post<GoalResult>(`/goals/${id}/progress`, input),
  toggleMilestone: (id: number, milestoneId: number) =>
    api.post<GoalResult>(`/goals/${id}/milestones/${milestoneId}/toggle`),
  complete: (id: number) => api.post<GoalResult>(`/goals/${id}/complete`),
}

export const achievementsApi = {
  list: () => api.get<Achievement[]>('/achievements'),
}

export const playerApi = {
  get: () => api.get<PlayerDetail>('/player'),
}

export const settingsApi = {
  get: () => api.get<Settings>('/settings'),
  update: (input: SettingsInput) => api.put<Settings>('/settings', input),
}

export const reviewsApi = {
  current: () => api.get<WeeklySummary>('/reviews/weekly/current'),
  list: () => api.get<WeeklyReview[]>('/reviews/weekly'),
  create: (input: WeeklyReviewInput) => api.post<WeeklyReviewResult>('/reviews/weekly', input),
}

export const pushApi = {
  config: () => api.get<PushConfig>('/push/config'),
  subscribe: (subscription: PushSubscriptionJSON) => api.post<void>('/push/subscriptions', subscription),
  unsubscribe: (endpoint: string) => api.post<void>('/push/subscriptions/remove', { endpoint }),
  test: () => api.post<{ sent: number }>('/push/test'),
}
