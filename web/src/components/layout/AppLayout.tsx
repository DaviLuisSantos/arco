import { CircleUser, ListChecks, Shield, Sun, Target, type LucideIcon } from 'lucide-react'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink, Outlet } from 'react-router'
import { cn } from '../../lib/cn'
import { AccountBanners } from '../../features/account/AccountBanners'
import { useAuthStatus } from '../../lib/useAuthStatus'
import { syncPushSubscription } from '../../lib/push'
import { ArcoLogo } from './ArcoLogo'
import { ConnectionBanner } from './ConnectionBanner'

const NAV: { to: string; key: 'nav.today' | 'nav.habits' | 'nav.cycle' | 'nav.profile' | 'nav.admin'; icon: LucideIcon; end: boolean }[] = [
  { to: '/', key: 'nav.today', icon: Sun, end: true },
  { to: '/habits', key: 'nav.habits', icon: ListChecks, end: false },
  { to: '/cycle', key: 'nav.cycle', icon: Target, end: false },
  { to: '/profile', key: 'nav.profile', icon: CircleUser, end: false },
]

export function AppLayout() {
  const { t } = useTranslation('layout')
  const me = useAuthStatus().data
  const nav: typeof NAV = me?.isAdmin ? [...NAV, { to: '/admin', key: 'nav.admin', icon: Shield, end: false }] : NAV
  // Mantém a inscrição push deste dispositivo registrada no servidor.
  useEffect(() => {
    void syncPushSubscription()
  }, [])

  return (
    <div className="min-h-dvh">
      {/* Sidebar (≥ md) */}
      <aside className="fixed inset-y-0 left-0 hidden w-60 flex-col border-r border-border bg-surface px-3 py-6 md:flex">
        <div className="mb-8 flex items-center gap-2 px-3">
          <ArcoLogo className="size-8" />
          <span className="text-xl font-extrabold tracking-tight">Arco</span>
        </div>
        <nav aria-label={t('nav.main')} className="flex flex-col gap-1">
          {nav.map(({ to, key, icon: Icon, end }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-3 rounded-xl px-3 py-2.5 font-medium transition',
                  isActive ? 'bg-surface-2 text-text' : 'text-muted hover:bg-surface-2 hover:text-text',
                )
              }
            >
              <Icon className="size-5" aria-hidden />
              {t(key)}
            </NavLink>
          ))}
        </nav>
      </aside>

      <main className="w-full px-4 pt-[max(1.25rem,env(safe-area-inset-top))] pb-[calc(6rem+env(safe-area-inset-bottom))] md:pt-8 md:pr-8 md:pb-12 md:pl-68">
        <div className="mx-auto max-w-2xl">
          <AccountBanners />
          <ConnectionBanner />
          <Outlet />
        </div>
      </main>

      {/* Bottom nav (celular) */}
      <nav
        aria-label={t('nav.main')}
        className="fixed inset-x-0 bottom-0 z-40 border-t border-border bg-surface/95 pb-[env(safe-area-inset-bottom)] backdrop-blur md:hidden"
      >
        <ul className={cn('grid', nav.length > 4 ? 'grid-cols-5' : 'grid-cols-4')}>
          {nav.map(({ to, key, icon: Icon, end }) => (
            <li key={to}>
              <NavLink
                to={to}
                end={end}
                className={({ isActive }) =>
                  cn(
                    'group flex flex-col items-center gap-1 py-2 text-xs font-medium',
                    isActive ? 'text-accent' : 'text-muted',
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <span
                      className={cn(
                        'grid h-8 w-14 place-items-center rounded-full transition',
                        isActive ? 'bg-accent/15' : 'group-active:bg-surface-2',
                      )}
                    >
                      <Icon className="size-6" aria-hidden />
                    </span>
                    {t(key)}
                  </>
                )}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
    </div>
  )
}
