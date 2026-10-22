import { CircleUser, ListChecks, Sun, Target } from 'lucide-react'
import { useEffect } from 'react'
import { NavLink, Outlet } from 'react-router'
import { cn } from '../../lib/cn'
import { syncPushSubscription } from '../../lib/push'
import { ArcoLogo } from './ArcoLogo'

const NAV = [
  { to: '/', label: 'Hoje', icon: Sun, end: true },
  { to: '/habits', label: 'Hábitos', icon: ListChecks, end: false },
  { to: '/cycle', label: 'Ciclo', icon: Target, end: false },
  { to: '/profile', label: 'Perfil', icon: CircleUser, end: false },
]

export function AppLayout() {
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
        <nav aria-label="Principal" className="flex flex-col gap-1">
          {NAV.map(({ to, label, icon: Icon, end }) => (
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
              {label}
            </NavLink>
          ))}
        </nav>
      </aside>

      <main className="w-full px-4 pt-[max(1.25rem,env(safe-area-inset-top))] pb-[calc(6rem+env(safe-area-inset-bottom))] md:pt-8 md:pr-8 md:pb-12 md:pl-68">
        <div className="mx-auto max-w-2xl">
          <Outlet />
        </div>
      </main>

      {/* Bottom nav (celular) */}
      <nav
        aria-label="Principal"
        className="fixed inset-x-0 bottom-0 z-40 border-t border-border bg-surface/95 pb-[env(safe-area-inset-bottom)] backdrop-blur md:hidden"
      >
        <ul className="grid grid-cols-4">
          {NAV.map(({ to, label, icon: Icon, end }) => (
            <li key={to}>
              <NavLink
                to={to}
                end={end}
                className={({ isActive }) =>
                  cn(
                    'flex flex-col items-center gap-1 py-2.5 text-xs font-medium',
                    isActive ? 'text-accent' : 'text-muted',
                  )
                }
              >
                <Icon className="size-6" aria-hidden />
                {label}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
    </div>
  )
}
