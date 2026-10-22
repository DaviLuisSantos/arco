// Tipos espelhando docs/API.md (Fase 1). Datas de calendário: "YYYY-MM-DD"; instantes: ISO-8601 UTC.

export type FrequencyType = 'Daily' | 'SpecificDays' | 'WeeklyTarget'
export type CheckInStatus = 'Done' | 'Minimum' | 'Missed'
export type GoalType = 'Numeric' | 'Milestones'
export type XpSource = 'CheckIn' | 'HabitLevelUp' | 'Milestone' | 'GoalCompleted' | 'CycleCompleted'
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
  completedAt: string | null
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
}

export interface Habit {
  id: number
  name: string
  emoji: string | null
  goalId: number | null
  frequencyType: FrequencyType
  frequencyDays: number[]
  timesPerWeek: number | null
  level: number
  levelDescription: string
  minimumDescription: string
  levels: HabitLevel[]
  archivedAt: string | null
  createdAt: string
}

export interface HabitDetail extends Habit {
  recentCheckIns: CheckIn[]
}

export interface TodayHabit extends Habit {
  isDueToday: boolean
  weekCount: number
  weekTarget: number | null
  checkIn: CheckIn | null
  goalTitle: string | null
}

export interface Today {
  date: string
  player: Player
  xpToday: number
  cycle: Cycle | null
  goals: Goal[]
  habits: TodayHabit[]
}

/** Toda mutação que gera ou remove XP devolve este par. */
export interface XpResult {
  xpDelta: number
  player: Player
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

export interface Settings {
  timezone: string
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
}

export interface LevelUpInput {
  levelDescription: string
  minimumDescription: string
}

export interface CheckInInput {
  habitId: number
  date: string
  status: CheckInStatus
}

export interface CycleInput {
  name: string
  startDate?: string
  weeks?: number
}

export interface CycleUpdateInput {
  name: string
  endDate: string
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
