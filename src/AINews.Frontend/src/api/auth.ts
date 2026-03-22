import { api } from './client'

export interface AuthUser {
  id: number
  email: string
  displayName: string | null
}

export const authApi = {
  me: () => api.get<AuthUser>('/auth/me').then((r) => r.data),
  logout: () => api.post('/auth/logout'),
  loginUrl: () => `/api/auth/login/google?returnUrl=/`,
}
