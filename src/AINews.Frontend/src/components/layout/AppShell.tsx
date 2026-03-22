import { useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { Newspaper, Rss, Settings, LogOut, Menu } from 'lucide-react'
import { useAuthStore } from '../../store/authStore'
import { authApi } from '../../api/auth'
import { useQuery } from '@tanstack/react-query'
import { topicsApi } from '../../api/topics'

interface AppShellProps {
  children: React.ReactNode
}

export function AppShell({ children }: AppShellProps) {
  const { user, setUser } = useAuthStore()
  const location = useLocation()
  const [sidebarOpen, setSidebarOpen] = useState(false)
  const { data: topics = [] } = useQuery({ queryKey: ['topics'], queryFn: topicsApi.list })

  const handleLogout = async () => {
    await authApi.logout()
    setUser(null)
    window.location.href = '/login'
  }

  const navLink = (to: string, icon: React.ReactNode, label: string) => {
    const active = location.pathname === to || location.pathname.startsWith(to + '/')
    return (
      <Link
        to={to}
        onClick={() => setSidebarOpen(false)}
        className={`flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-medium transition-colors ${
          active
            ? 'bg-indigo-50 text-indigo-700'
            : 'text-gray-600 hover:bg-gray-50 hover:text-gray-900'
        }`}
      >
        {icon}
        {label}
      </Link>
    )
  }

  const sidebar = (
    <div className="flex flex-col h-full">
      <div className="px-4 py-4 border-b border-gray-100">
        <div className="flex items-center gap-2">
          <Newspaper className="w-6 h-6 text-indigo-600" />
          <span className="font-semibold text-gray-900">AI News</span>
        </div>
      </div>

      <nav className="flex-1 px-3 py-4 space-y-1 overflow-y-auto">
        {navLink('/feed', <Newspaper className="w-4 h-4" />, 'All Topics')}
        {topics.map((t) =>
          navLink(`/topics/${t.id}`, <Newspaper className="w-4 h-4" />, t.name)
        )}
        <div className="pt-4 border-t border-gray-100 mt-4">
          {navLink('/sources', <Rss className="w-4 h-4" />, 'Sources')}
          {navLink('/settings', <Settings className="w-4 h-4" />, 'Settings')}
        </div>
      </nav>

      <div className="px-4 py-3 border-t border-gray-100">
        <div className="flex items-center justify-between">
          <div className="text-xs text-gray-500 truncate max-w-[140px]">
            {user?.displayName ?? user?.email}
          </div>
          <button
            onClick={handleLogout}
            className="p-1.5 rounded hover:bg-gray-100 text-gray-500 hover:text-gray-700"
            title="Logout"
          >
            <LogOut className="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  )

  return (
    <div className="flex h-screen bg-gray-50">
      {/* Mobile overlay */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 bg-black/30 z-20 lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* Sidebar */}
      <aside
        className={`fixed lg:static inset-y-0 left-0 z-30 w-56 bg-white border-r border-gray-100 flex flex-col
          transform transition-transform lg:translate-x-0
          ${sidebarOpen ? 'translate-x-0' : '-translate-x-full'}`}
      >
        {sidebar}
      </aside>

      {/* Main */}
      <div className="flex-1 flex flex-col min-w-0 overflow-hidden">
        <header className="lg:hidden flex items-center gap-3 px-4 py-3 bg-white border-b border-gray-100">
          <button onClick={() => setSidebarOpen(true)} className="p-1 rounded">
            <Menu className="w-5 h-5" />
          </button>
          <Newspaper className="w-5 h-5 text-indigo-600" />
          <span className="font-semibold text-sm">AI News</span>
        </header>
        <main className="flex-1 overflow-y-auto p-6">{children}</main>
      </div>
    </div>
  )
}
