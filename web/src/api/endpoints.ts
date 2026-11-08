import { api, ApiError } from './client'
import type {
  CycleArch,
  Achievement,
  AdminMetrics,
  ApiKey,
  AuthSession,
  BillingInfo,
  AuthProviders,
  CheckIn,
  CheckInInput,
  CheckInResult,
  CreatedApiKey,
  Cycle,
  CycleCompleteInput,
  CycleCompleteResult,
  CycleSummary,
  FocusList,
  FocusSession,
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
  Me,
  PartnerInvite,
  Partners,
  PartnerSummary,
  PublicProfile,
  PublicProfileInput,
  TwoFactorChallenge,
  TwoFactorEnabled,
  TwoFactorSetup,
  LevelUpInput,
  PlayerDetail,
  PushConfig,
  Settings,
  SettingsInput,
  Stats,
  Today,
  WeeklyReview,
  WeeklyReviewInput,
  WeeklyReviewResult,
  WeeklySummary,
  XpResult,
} from './types'

export const authApi = {
  register: (input: { email: string; password: string; displayName?: string }) =>
    api.post<void>('/auth/register', input),
  /** 204 (undefined) = entrou; com 2FA vem o desafio com o ticket. */
  login: (email: string, password: string) =>
    api.post<TwoFactorChallenge | undefined>('/auth/login', { email, password }),
  login2fa: (ticket: string, code: string) => api.post<void>('/auth/login/2fa', { ticket, code }),
  twoFactorSetup: () => api.post<TwoFactorSetup>('/auth/2fa/setup'),
  twoFactorEnable: (code: string) => api.post<TwoFactorEnabled>('/auth/2fa/enable', { code }),
  twoFactorDisable: (code: string) => api.post<void>('/auth/2fa/disable', { code }),
  sessions: () => api.get<AuthSession[]>('/auth/sessions'),
  revokeSession: (id: number) => api.delete<void>(`/auth/sessions/${id}`),
  revokeOtherSessions: () => api.post<void>('/auth/sessions/revoke-others'),
  setLocale: (locale: string) => api.put<void>('/auth/locale', { locale }),
  completeOnboarding: () => api.post<void>('/auth/onboarding/complete'),
  /** 401 (sem cookie) também conta como sessão encerrada. */
  logout: async (): Promise<void> => {
    try {
      await api.post<void>('/auth/logout')
    } catch (e) {
      if (!(e instanceof ApiError && e.status === 401)) throw e
    }
  },
  /** Usuário logado; 401 vira null. */
  me: async (): Promise<Me | null> => {
    try {
      return await api.get<Me>('/auth/me')
    } catch (e) {
      if (e instanceof ApiError && e.status === 401) return null
      throw e
    }
  },
  providers: () => api.get<AuthProviders>('/auth/providers'),
  demo: () => api.post<void>('/auth/demo'),
  verifyEmail: (token: string) => api.post<void>('/auth/verify-email', { token }),
  resendVerification: () => api.post<void>('/auth/resend-verification'),
  forgotPassword: (email: string) => api.post<void>('/auth/forgot-password', { email }),
  resetPassword: (token: string, password: string) =>
    api.post<void>('/auth/reset-password', { token, password }),
  changePassword: (input: { currentPassword?: string; newPassword: string }) =>
    api.post<void>('/auth/change-password', input),
  deleteAccount: (password?: string) => api.delete<void>('/auth/account', { password }),
}

export const billingApi = {
  get: () => api.get<BillingInfo>('/billing'),
  checkout: () => api.post<{ url: string }>('/billing/checkout'),
  portal: () => api.post<{ url: string }>('/billing/portal'),
}

export const profileApi = {
  updatePublic: (input: PublicProfileInput) => api.put<void>('/profile/public', input),
  getPublic: (slug: string) => api.get<PublicProfile>(`/public/profiles/${encodeURIComponent(slug)}`),
}

export const partnersApi = {
  list: () => api.get<Partners>('/partners'),
  invite: () => api.post<PartnerInvite>('/partners/invites'),
  accept: (token: string) =>
    api.post<{ id: number; owner: { displayName: string } }>('/partners/accept', { token }),
  summary: (id: number) => api.get<PartnerSummary>(`/partners/${id}/summary`),
  remove: (id: number) => api.delete<void>(`/partners/${id}`),
}

export const adminApi = {
  metrics: () => api.get<AdminMetrics>('/admin/metrics'),
}

/** Exporta os dados do usuário (GET /api/backup) e dispara o download do JSON. */
export const exportApi = {
  download: async (): Promise<void> => {
    const data = await api.get<unknown>('/backup')
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `arco-export-${new Date().toISOString().slice(0, 10)}.json`
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(url)
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
  list: (from: string, to: string, habitId?: number) =>
    api.get<CheckIn[]>(
      `/checkins?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}${habitId ? `&habitId=${habitId}` : ''}`,
    ),
}

export const habitsApi = {
  list: (includeArchived = false) => api.get<Habit[]>(`/habits?includeArchived=${includeArchived}`),
  get: (id: number) => api.get<HabitDetail>(`/habits/${id}`),
  create: (input: HabitInput) => api.post<Habit>('/habits', input),
  update: (id: number, input: HabitInput) => api.put<Habit>(`/habits/${id}`, input),
  levelUp: (id: number, input: LevelUpInput) => api.post<HabitLevelUpResult>(`/habits/${id}/level-up`, input),
  dismissLevelUpSuggestion: (id: number) => api.post<void>(`/habits/${id}/level-up-suggestion/dismiss`),
  /** Volta um nível: o atual volta para o topo da escada planejada. Não mexe no XP. */
  levelDown: (id: number) => api.post<Habit>(`/habits/${id}/level-down`),
  dismissLevelDownSuggestion: (id: number) => api.post<void>(`/habits/${id}/level-down-suggestion/dismiss`),
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
  arch: (id: number) => api.get<CycleArch>(`/cycles/${id}/arch`),
  complete: (id: number, input: CycleCompleteInput) => api.post<CycleCompleteResult>(`/cycles/${id}/complete`, input),
  /** Apaga o ciclo (ativo ou encerrado), seus objetivos e o XP que eles deram. */
  remove: (id: number) => api.delete<XpResult>(`/cycles/${id}`),
}

export const goalsApi = {
  list: (cycleId?: number) => api.get<Goal[]>(cycleId ? `/goals?cycleId=${cycleId}` : '/goals'),
  create: (input: GoalInput) => api.post<Goal>('/goals', input),
  update: (id: number, input: GoalUpdateInput) => api.put<Goal>(`/goals/${id}`, input),
  /** Também tira o XP que o objetivo deu (conclusão e marcos). */
  remove: (id: number) => api.delete<XpResult>(`/goals/${id}`),
  progress: (id: number, input: GoalProgressInput) => api.post<GoalResult>(`/goals/${id}/progress`, input),
  toggleMilestone: (id: number, milestoneId: number) =>
    api.post<GoalResult>(`/goals/${id}/milestones/${milestoneId}/toggle`),
  complete: (id: number) => api.post<GoalResult>(`/goals/${id}/complete`),
}

export const apiKeysApi = {
  list: () => api.get<ApiKey[]>('/api-keys'),
  create: (name: string) => api.post<CreatedApiKey>('/api-keys', { name }),
  revoke: (id: number) => api.delete<void>(`/api-keys/${id}`),
}

export const statsApi = {
  get: () => api.get<Stats>('/stats'),
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

export const focusApi = {
  list: (from: string, to: string, habitId?: number) =>
    api.get<FocusList>(
      `/focus-sessions?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}${habitId ? `&habitId=${habitId}` : ''}`,
    ),
  create: (input: { habitId: number; minutes: number; date?: string; startedAt?: string }) =>
    api.post<{ session: FocusSession }>('/focus-sessions', input),
  remove: (id: number) => api.delete<void>(`/focus-sessions/${id}`),
}
