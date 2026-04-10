import { Activity, LayoutDashboard, ListTree, Shield } from 'lucide-react'
import { NavLink, Outlet } from 'react-router-dom'
import { cn } from '@/lib/utils'

const nav = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/jobs', label: 'Crawl jobs', icon: ListTree },
  { to: '/compliance', label: 'Compliance', icon: Shield },
]

export function AppShell() {
  return (
    <div className="flex min-h-screen">
      <aside className="flex w-56 flex-col border-r border-[var(--color-border)] bg-[var(--color-muted)]/40">
        <div className="flex h-14 items-center gap-2 border-b border-[var(--color-border)] px-4">
          <Activity className="size-5" />
          <span className="font-semibold">SamoBot</span>
        </div>
        <nav className="flex flex-1 flex-col gap-1 p-3">
          {nav.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              end={to === '/'}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                  isActive
                    ? 'bg-white text-[var(--color-foreground)] shadow-sm'
                    : 'text-[var(--color-muted-foreground)] hover:bg-white/60 hover:text-[var(--color-foreground)]',
                )
              }
            >
              <Icon className="size-4" />
              {label}
            </NavLink>
          ))}
        </nav>
      </aside>
      <div className="flex flex-1 flex-col">
        <header className="flex h-14 items-center border-b border-[var(--color-border)] px-6">
          <span className="text-sm text-[var(--color-muted-foreground)]">Crawl dashboard</span>
        </header>
        <main className="flex-1 overflow-auto p-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
