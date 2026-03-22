import { api } from './client'

export interface Topic {
  id: number
  name: string
  description: string | null
  createdAt: string
  sourceCount: number
}

export const topicsApi = {
  list: () => api.get<Topic[]>('/topics').then((r) => r.data),
  get: (id: number) => api.get<Topic>(`/topics/${id}`).then((r) => r.data),
  create: (data: { name: string; description?: string }) =>
    api.post<Topic>('/topics', data).then((r) => r.data),
  update: (id: number, data: { name: string; description?: string }) =>
    api.put<Topic>(`/topics/${id}`, data).then((r) => r.data),
  delete: (id: number) => api.delete(`/topics/${id}`),
}
