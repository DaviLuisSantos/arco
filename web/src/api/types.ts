// Tipos espelhando docs/API.md (Fase 2). Datas de calendário: "YYYY-MM-DD"; instantes: ISO-8601 UTC.

export type FrequencyType = 'Daily' | 'SpecificDays' | 'WeeklyTarget'
export type CheckInStatus = 'Done' | 'Minimum' | 'Missed'
export type GoalType = 'Numeric' | 'Milestones'
export type XpSource = 'CheckIn' | 'HabitLevelUp' | 'Milestone' | 'GoalCompleted' | 'CycleCompleted' | 'WeeklyReview'
export type CycleStatus = 'Active' | 'Completed'

export interface ApiErrorBody {
  error: string
  code: string
}

export interface Player {
  level: number
  totalXp: number
  xpIntoLevel: number
  xpForNextLevel: number
}

export interface Cycle {
  id: number
  name: string
  startDate: string
  endDate: string
  status: CycleStatus
  dayNumber: number
  totalDays: number
  reviewNotes: string | null
  reviewWins: string | null
  reviewObstacles: string | null
  reviewNext: string | null
  completedAt: string | null
  weeks: number
  weeklyFocusMinutes: number
}

export interface Milestone {
  id: number
  title: string
  order: number
  doneAt: string | null
}

export interface Goal {
  id: number
  cycleId: number
  title: string
  description: string | null
  type: GoalType
  unit: string | null
  startValue: number | null
  targetValue: number | null
  currentValue: number | null
  milestones: Milestone[]
  deadline: string
  progress: number
  completedAt: string | null
  habitIds: number[]
}

export interface HabitLevel {
  level: number
  description: string
  minimumDescription: string
  reachedAt: string
}

export interface CheckIn {
  habitId: number
  date: string
  status: CheckInStatus
  xp: number
  /** Minutos manuais do dia. */
  minutes?: number | null
}

export interface Habit {
  id: number
  name: string
  emoji: string | null
  goalId: number | null
  frequencyType: FrequencyType
  frequencyDays: number[]
  timesPerWeek: number | null
  /** "HH:mm" no fuso configurado, ou null. */
  reminderTime: string | null
  level: number
  levelDescription: string
  minimumDescription: string
  levels: HabitLevel[]
  /** A escada à frente; subir de nível usa (e consome) o primeiro. */
  plannedLevels: HabitPlannedLevel[]
  /** Regra "N semanas seguidas com X+ dias" (Feito e Mínimo contam); null = 80% de "Feito" em 14 dias. */
  levelUpWeeks: number | null
  levelUpDaysPerWeek: number | null
  archivedAt: string | null
  createdAt: string
}

/** Nível planejado; `level` é o número que ele vai ter. */
export interface HabitPlannedLevel {
  level: number
  description: string
  minimumDescription: string
}

/** Consistência dos últimos 14 dias (só "Feito" conta). */
export interface Consistency {
  from: string
  to: string
  done: number
  expected: number
  rate: number
}

export type StreakUnit = 'Days' | 'Weeks'

/**
 * Sequência do hábito: dias devidos com Feito/Mínimo (ou semanas com a meta batida).
 * Uma falha isolada por semana é perdoada; duas seguidas, ou duas na mesma semana, zeram.
 */
export interface Streak {
  current: number
  best: number
  unit: StreakUnit
}

export interface HabitDetail extends Habit {
  recentCheckIns: CheckIn[]
  consistency: Consistency
  levelUpSuggested: boolean
  /** Semanas seguidas que contam para a regra de semanas (null na regra padrão). */
  levelUpWeeksDone: number | null
  /** 2 semanas terminadas, no nível atual, abaixo da metade da meta semanal. */
  levelDownSuggested: boolean
  streak: Streak
}

export interface TodayHabit extends Habit {
  isDueToday: boolean
  weekCount: number
  weekTarget: number | null
  checkIn: CheckIn | null
  goalTitle: string | null
  consistency: Consistency
  levelUpSuggested: boolean
  levelUpWeeksDone: number | null
  /** 2 semanas terminadas, no nível atual, abaixo da metade da meta semanal. */
  levelDownSuggested: boolean
  streak: Streak
}

export interface WeeklyReviewStatus {
  weekStart: string
  weekEnd: string
  due: boolean
}

export interface Today {
  date: string
  player: Player
  xpToday: number
  cycle: Cycle | null
  goals: Goal[]
  habits: TodayHabit[]
  weeklyReview: WeeklyReviewStatus
}

/** Toda mutação que gera ou remove XP devolve este par. */
export interface XpResult {
  xpDelta: number
  player: Player
  /** Conquistas que esta ação desbloqueou. */
  achievements: Achievement[]
}

/** Conquista do catálogo: desbloqueia sozinha, não dá XP e nunca é perdida. */
export interface Achievement {
  key: string
  emoji: string
  title: string
  description: string
  target: number
  /** 0..target */
  progress: number
  /** null enquanto bloqueada. */
  unlockedAt: string | null
}

export interface CheckInResult extends XpResult {
  checkIn: CheckIn
}

export interface HabitLevelUpResult extends XpResult {
  habit: Habit
}

export interface CycleCompleteResult extends XpResult {
  cycle: Cycle
}

export interface GoalResult extends XpResult {
  goal: Goal
}

export interface XpEvent {
  date: string
  amount: number
  source: XpSource
  description: string
}

export interface PlayerDetail extends Player {
  recentEvents: XpEvent[]
}

export interface WeeklyReviewReminder {
  enabled: boolean
  /** 0 = domingo … 6 = sábado. */
  day: number
  /** "HH:mm" */
  time: string
}

export interface Settings {
  timezone: string
  weeklyReviewReminder: WeeklyReviewReminder
}

export interface SettingsInput {
  timezone?: string
  weeklyReviewReminder?: Partial<WeeklyReviewReminder>
}

export interface WeeklyReview {
  id: number
  weekStart: string
  weekEnd: string
  wins: string | null
  obstacles: string | null
  next: string | null
  createdAt: string
}

export interface HabitPeriodStats {
  habitId: number
  name: string
  emoji: string | null
  level: number
  levelsGained: number
  done: number
  minimum: number
  missed: number
  expected: number
  /** (Feito + Mínimo) / esperado, de 0 a 1. */
  rate: number
  levelUpSuggested: boolean
  /** Minutos de foco no período (opcional: respostas em cache antigas não têm). */
  minutes?: number
}

/** Uma semana (segunda a domingo) das estatísticas; a semana atual conta só até hoje. */
export interface WeekStats {
  weekStart: string
  weekEnd: string
  xp: number
  done: number
  minimum: number
  missed: number
  expected: number
  /** (done + minimum) / expected, 0..1 */
  rate: number
  /** Minutos de foco na semana. */
  minutes?: number
}

export interface Stats {
  weeks: WeekStats[]
  recent: {
    from: string
    to: string
    xp: number
    checkIns: CheckInTotals
    habits: HabitPeriodStats[]
    /** Minutos de foco nos últimos 30 dias. */
    minutes?: number
  }
}

/** Chave de API (o segredo inteiro só aparece em CreatedApiKey, na criação). */
export interface ApiKey {
  id: number
  name: string
  /** Primeiros caracteres da chave, para reconhecer. */
  prefix: string
  createdAt: string
  lastUsedAt: string | null
}

export interface CreatedApiKey extends Omit<ApiKey, 'lastUsedAt'> {
  key: string
}

export interface CheckInTotals {
  done: number
  minimum: number
  missed: number
}

export interface WeeklySummary {
  weekStart: string
  weekEnd: string
  review: WeeklyReview | null
  xp: number
  checkIns: CheckInTotals
  habits: HabitPeriodStats[]
  cycle: Cycle | null
  goals: Goal[]
}

export interface WeeklyReviewResult extends XpResult {
  review: WeeklyReview
}

export interface CycleSummary {
  cycle: Cycle
  from: string
  to: string
  xp: number
  checkIns: CheckInTotals
  goals: Goal[]
  habits: HabitPeriodStats[]
  weeklyReviews: WeeklyReview[]
}

export interface PushConfig {
  publicKey: string
  subscriptions: number
}

export interface Health {
  status: string
  version: string
}

// Corpos de requisição

export interface HabitInput {
  name: string
  emoji?: string | null
  goalId?: number | null
  frequencyType: FrequencyType
  frequencyDays?: number[]
  timesPerWeek?: number | null
  levelDescription: string
  minimumDescription: string
  reminderTime?: string | null
  /** Mesmos campos de `Habit.plannedLevels` (sem o `level`, que é calculado). */
  plannedLevels?: Array<{ description: string; minimumDescription: string }>
  levelUpWeeks?: number | null
  levelUpDaysPerWeek?: number | null
}

/** Sem campos, usa o próximo nível planejado. */
export interface LevelUpInput {
  levelDescription?: string
  minimumDescription?: string
}

export interface CheckInInput {
  habitId: number
  date: string
  status: CheckInStatus
  /** Ausente = não mexe; 0 = remove a sessão manual do dia. */
  minutes?: number
}

export interface CycleInput {
  name: string
  startDate?: string
  weeks?: number
  weeklyFocusMinutes?: number
}

/** Respostas guiadas da revisão (semanal ou de ciclo). */
export interface ReviewAnswers {
  wins?: string | null
  obstacles?: string | null
  next?: string | null
}

export interface WeeklyReviewInput extends ReviewAnswers {
  weekStart: string
}

export interface CycleCompleteInput extends ReviewAnswers {
  reviewNotes?: string | null
}

export interface CycleUpdateInput {
  name: string
  endDate: string
  weeklyFocusMinutes?: number
}

export interface GoalInput {
  cycleId: number
  title: string
  description?: string | null
  type: GoalType
  unit?: string | null
  startValue?: number | null
  targetValue?: number | null
  deadline?: string
  milestones?: string[]
}

export interface GoalUpdateInput {
  title: string
  description?: string | null
  unit?: string | null
  targetValue?: number | null
  deadline?: string
}

export interface GoalProgressInput {
  value: number
  note?: string | null
}

export interface FocusSession {
  id: number
  habitId: number
  date: string
  minutes: number
  source: 'Timer' | 'Manual'
  startedAt: string | null
}

export interface FocusList {
  sessions: FocusSession[]
  totalMinutes: number
  byHabit: { habitId: number; name: string; emoji: string | null; minutes: number; sessions: number }[]
}

export type FocusSourceKind = 'Timer' | 'Manual'

export interface ArchHabit {
  habitId: number
  name: string
  emoji: string | null
}

export interface ArchHabitMinutes {
  habitId: number
  minutes: number
}

export interface ArchBrick {
  sessionId: number
  habitId: number
  minutes: number
  source: FocusSourceKind
}

export interface ArchDay {
  date: string
  minutes: number
  bricks: ArchBrick[]
}

export interface ArchStone {
  week: number
  from: string
  to: string
  minutes: number
  byHabit: ArchHabitMinutes[]
  days: ArchDay[]
}

export interface CycleArch {
  cycleId: number
  startDate: string
  endDate: string
  closed: boolean
  weeks: number
  layout: 'Arch' | 'Row'
  weeklyTargetMinutes: number
  totalMinutes: number
  habits: ArchHabit[]
  stones: ArchStone[]
}

export interface Me {
  id: number
  email: string
  displayName: string
  emailVerified: boolean
  isDemo: boolean
  hasPassword: boolean
  hasGoogle: boolean
  twoFactorEnabled: boolean
  plan: Plan
  locale: string
  isAdmin: boolean
  onboarded: boolean
  publicProfile: PublicProfileSettings
}

export type Plan = 'free' | 'pro'

export interface PublicProfileSettings {
  enabled: boolean
  slug: string | null
  showHabits: boolean
  theme: string
}

/** Login com 2FA: 200 com ticket em vez de 204. */
export interface TwoFactorChallenge {
  twoFactorRequired: true
  ticket: string
}

export interface TwoFactorSetup {
  secret: string
  otpauthUri: string
}

export interface TwoFactorEnabled {
  recoveryCodes: string[]
}

export interface AuthSession {
  id: number
  createdAt: string
  lastSeenAt: string
  userAgent: string | null
  current: boolean
}

export interface BillingInfo {
  plan: Plan
  renewsAt: string | null
  cancelAtPeriodEnd: boolean
  stripeEnabled: boolean
  limits: { habits: number | null; apiKeys: number | null; mcp: boolean; profileTheme: boolean }
  usage: { habits: number; apiKeys: number }
}

export interface PublicProfileInput {
  enabled: boolean
  slug: string
  showHabits: boolean
  theme: string
}

export interface PublicProfile {
  displayName: string
  level: number
  totalXp: number
  theme: string
  cycle: {
    name: string
    week: number
    totalWeeks: number
    stones: { week: number; rate: number }[]
  } | null
  habits: { name: string; level: number; streak: number }[] | null
}

export interface PartnerLink {
  id: number
  displayName: string
  since: string
}

export interface PendingInvite {
  id: number
  createdAt: string
  expiresAt: string
}

export interface Partners {
  sharingWith: PartnerLink[]
  sharedWithMe: PartnerLink[]
  pendingInvites: PendingInvite[]
}

export interface PartnerInvite {
  id: number
  link: string
  expiresAt: string
}

export interface PartnerWeekHabit {
  name: string
  level: number
  done: number
  minimum: number
  expected: number
  rate: number
  streak: number
}

export interface PartnerSummary {
  owner: { displayName: string; level: number }
  weeks: {
    start: string
    end: string
    habits: PartnerWeekHabit[]
    totals: CheckInTotals
  }[]
}

export interface AdminMetrics {
  users: { total: number; verified: number; demo: number; pro: number }
  signupsByDay: { date: string; count: number }[]
  checkInsByDay: { date: string; count: number }[]
  activeUsers: { d1: number; d7: number; d30: number }
  retention: { cohortWeek: string; size: number; week1: number; week2: number; week4: number }[]
}

export interface AuthProviders {
  google: boolean
}
