import { CircleUser, ListChecks, Sun, Target, TrendingUp, type LucideIcon } from 'lucide-react'
import { useEffect, type MouseEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, Outlet, useLocation } from 'react-router'
import { cn } from '../../lib/cn'
import { AccountBanners } from '../../features/account/AccountBanners'
import { syncPushSubscription } from '../../lib/push'
import { ArcoLogo } from './ArcoLogo'
import { ConnectionBanner } from './ConnectionBanner'

interface NavItem {
  to: string
  key: 'nav.today' | 'nav.habits' | 'nav.cycle' | 'nav.progress' | 'nav.profile'
  icon: LucideIcon
  // Rotas que deixam a aba ativa (prefixos; '/' só casa exato).
  paths: string[]
}

const NAV: NavItem[] = [
  { to: '/', key: 'nav.today', icon: Sun, paths: ['/'] },
  { to: '/habits', key: 'nav.habits', icon: ListChecks, paths: ['/habits'] },
  { to: '/cycle', key: 'nav.cycle', icon: Target, paths: ['/cycle'] },
  { to: '/progress', key: 'nav.progress', icon: TrendingUp, paths: ['/progress', '/stats', '/review'] },
  { to: '/profile', key: 'nav.profile', icon: CircleUser, paths: ['/profile', '/settings'] },
]

function isActivePath(pathname: string, paths: string[]) {
  return paths.some((p) => (p === '/' ? pathname === '/' : pathname === p || pathname.startsWith(`${p}/`)))
}

function scrollToTop() {
  const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches
  window.scrollTo({ top: 0, behavior: reduced ? 'auto' : 'smooth' })
}

export function AppLayout() {
  const { t } = useTranslation('layout')
  const { pathname } = useLocation()
  // Mantém a inscrição push deste dispositivo registrada no servidor.
  useEffect(() => {
    void syncPushSubscription()
  }, [])

  // Retoque na aba ativa, já na raiz dela: só volta ao topo.
  const onNavClick = (e: MouseEvent, to: string) => {
    if (pathname === to) {
      e.preventDefault()
      scrollToTop()
    }
  }

  return (
    <div className="min-h-dvh">
      {/* Sidebar (≥ md) */}
      <aside className="fixed inset-y-0 left-0 hidden w-60 flex-col border-r border-border bg-surface px-3 py-6 md:flex">
        <div className="mb-8 flex items-center gap-2 px-3">
          <ArcoLogo className="size-8" />
          <span className="text-xl font-extrabold tracking-tight">Arco</span>
        </div>
        <nav aria-label={t('nav.main')} className="flex flex-col gap-1">
          {NAV.map(({ to, key, icon: Icon, paths }) => {
            const active = isActivePath(pathname, paths)
            return (
              <Link
                key={to}
                to={to}
                viewTransition
                aria-current={active ? 'page' : undefined}
                onClick={(e) => onNavClick(e, to)}
                className={cn(
                  'flex items-center gap-3 rounded-xl px-3 py-2.5 font-medium transition',
                  active ? 'bg-surface-2 text-text' : 'text-muted hover:bg-surface-2 hover:text-text',
                )}
              >
                <Icon className="size-5" aria-hidden />
                {t(key)}
              </Link>
            )
          })}
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
        <ul className="grid grid-cols-5">
          {NAV.map(({ to, key, icon: Icon, paths }) => {
            const active = isActivePath(pathname, paths)
            return (
              <li key={to}>
                <Link
                  to={to}
                  viewTransition
                  aria-current={active ? 'page' : undefined}
                  onClick={(e) => onNavClick(e, to)}
                  className={cn(
                    'group flex flex-col items-center gap-1 py-2 text-xs font-medium',
                    active ? 'text-accent' : 'text-muted',
                  )}
                >
                  <span
                    className={cn(
                      'grid h-8 w-14 place-items-center rounded-full transition',
                      active ? 'bg-accent/15' : 'group-active:bg-surface-2',
                    )}
                  >
                    <Icon className="size-6" aria-hidden />
                  </span>
                  {t(key)}
                </Link>
              </li>
            )
          })}
        </ul>
      </nav>
    </div>
  )
}
