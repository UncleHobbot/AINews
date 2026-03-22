import { api } from './client'

export interface ScanRun {
  id: number
  status: string
  startedAt: string
  completedAt: string | null
  totalSourcesScanned: number
  totalRawPostsFetched: number
  totalNewsItemsCreated: number
  errorMessage: string | null
}

export const scanApi = {
  trigger: () => api.post<{ scanRunId: number }>('/scan/trigger').then((r) => r.data),
  status: () => api.get<ScanRun | null>('/scan/status').then((r) => r.data),
  history: () => api.get<ScanRun[]>('/scan/history').then((r) => r.data),
}
