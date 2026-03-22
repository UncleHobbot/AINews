import { useEffect } from 'react'
import { authApi } from '../api/auth'
import { useAuthStore } from '../store/authStore'

export function useAuth() {
  const { user, isLoading, setUser, setLoading } = useAuthStore()

  useEffect(() => {
    authApi
      .me()
      .then((u) => { setUser(u); setLoading(false) })
      .catch(() => { setUser(null); setLoading(false) })
  }, [])

  return { user, isLoading }
}
