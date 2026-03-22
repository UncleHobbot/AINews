import { api } from './client'

export interface Setting {
  key: string
  maskedValue: string | null
  isSet: boolean
}

export const settingsApi = {
  list: () => api.get<Setting[]>('/settings').then((r) => r.data),
  update: (settings: Record<string, string>) =>
    api.put('/settings', { settings }),
  redditAuthUrl: () =>
    api.get<{ authUrl: string }>('/settings/reddit/auth-url').then((r) => r.data),
}
