import { api } from './client'

export interface Source {
  id: number
  topicId: number
  type: 'Reddit' | 'X'
  displayName: string
  config: string
  isActive: boolean
  lastScannedAt: string | null
  createdAt: string
}

export const sourcesApi = {
  list: (topicId?: number) =>
    api.get<Source[]>('/sources', { params: topicId ? { topicId } : {} }).then((r) => r.data),
  create: (data: { topicId: number; type: string; displayName: string; config: string }) =>
    api.post<Source>('/sources', data).then((r) => r.data),
  update: (id: number, data: { displayName: string; config: string; isActive: boolean }) =>
    api.put<Source>(`/sources/${id}`, data).then((r) => r.data),
  delete: (id: number) => api.delete(`/sources/${id}`),
  toggle: (id: number) => api.patch<Source>(`/sources/${id}/toggle`).then((r) => r.data),
}
