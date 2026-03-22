import { api } from './client'

export interface ExtractedLink {
  id: number
  url: string
  linkType: string
  title: string | null
  summary: string | null
  fetchStatus: string
}

export interface NewsItem {
  id: number
  topicId: number
  topicName: string
  sourceType: string
  sourceDisplayName: string
  title: string
  aiSummary: string | null
  aiInsights: string[] | null
  relevance: number
  publishedAt: string
  externalUrl: string | null
  author: string | null
  extractedLinks: ExtractedLink[]
}

export interface NewsPage {
  items: NewsItem[]
  total: number
  page: number
  pageSize: number
}

export const newsApi = {
  list: (params: { topicId?: number; page?: number; limit?: number; since?: string }) =>
    api.get<NewsPage>('/news', { params }).then((r) => r.data),
}
