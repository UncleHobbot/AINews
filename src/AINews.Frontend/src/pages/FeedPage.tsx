import { useParams } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { newsApi, type NewsItem } from '../api/news'
import { scanApi } from '../api/scan'
import { useSignalR } from '../hooks/useSignalR'
import {
  Loader2, RefreshCw, Github, Youtube, FileText, ExternalLink,
  Zap, Newspaper, ThumbsUp, ThumbsDown,
} from 'lucide-react'
import { useState } from 'react'
import { topicsApi } from '../api/topics'

function LinkTypeIcon({ type }: { type: string }) {
  const cls = 'w-4 h-4'
  if (type === 'GitHub') return <Github className={cls} />
  if (type === 'YouTube') return <Youtube className={cls} />
  return <FileText className={cls} />
}

function linkTypeBg(type: string) {
  if (type === 'GitHub') return 'bg-gray-100 text-gray-700'
  if (type === 'YouTube') return 'bg-red-50 text-red-700'
  if (type === 'Docs') return 'bg-blue-50 text-blue-700'
  return 'bg-green-50 text-green-700'
}

function FeedbackButtons({ item, onFeedback }: {
  item: NewsItem
  onFeedback: (id: number, feedback: 'Liked' | 'Disliked' | null) => void
}) {
  const liked = item.userFeedback === 'Liked'
  const disliked = item.userFeedback === 'Disliked'

  return (
    <div className="flex items-center gap-1 shrink-0">
      <button
        onClick={() => onFeedback(item.id, liked ? null : 'Liked')}
        title={liked ? 'Remove like' : 'Like'}
        className={`p-1.5 rounded-lg transition-colors ${
          liked
            ? 'bg-green-100 text-green-600'
            : 'text-gray-300 hover:text-green-500 hover:bg-green-50'
        }`}
      >
        <ThumbsUp className="w-4 h-4" />
      </button>
      <button
        onClick={() => onFeedback(item.id, disliked ? null : 'Disliked')}
        title={disliked ? 'Remove dislike' : 'Dislike & hide'}
        className={`p-1.5 rounded-lg transition-colors ${
          disliked
            ? 'bg-red-100 text-red-500'
            : 'text-gray-300 hover:text-red-400 hover:bg-red-50'
        }`}
      >
        <ThumbsDown className="w-4 h-4" />
      </button>
    </div>
  )
}

export function FeedPage() {
  const { topicId } = useParams<{ topicId?: string }>()
  const tid = topicId ? parseInt(topicId) : undefined
  const [scanning, setScanning] = useState(false)
  const { progress } = useSignalR()
  const qc = useQueryClient()

  const { data: topics = [] } = useQuery({ queryKey: ['topics'], queryFn: topicsApi.list })
  const topicName = tid ? topics.find((t) => t.id === tid)?.name : 'All Topics'

  const queryKey = ['news', tid]
  const { data, isLoading, refetch } = useQuery({
    queryKey,
    queryFn: () => newsApi.list({ topicId: tid, limit: 50 }),
  })

  const handleScan = async () => {
    setScanning(true)
    try {
      await scanApi.trigger()
    } catch (e: any) {
      if (e.response?.status !== 409) console.error(e)
    }
  }

  const isActiveScan = scanning && progress && progress.status === 'Running'
  const scanDone = scanning && progress && ['Completed', 'Failed'].includes(progress.status)
  if (scanDone && !isActiveScan) {
    setScanning(false)
    refetch()
  }

  const handleFeedback = async (id: number, feedback: 'Liked' | 'Disliked' | null) => {
    // Optimistic update — disliked items vanish immediately, liked get highlighted
    qc.setQueryData(queryKey, (old: typeof data) => {
      if (!old) return old
      return {
        ...old,
        items: feedback === 'Disliked'
          // Remove disliked item from view immediately
          ? old.items.filter((n) => n.id !== id)
          // Update feedback state for liked/un-liked
          : old.items.map((n) => n.id === id ? { ...n, userFeedback: feedback } : n),
      }
    })
    try {
      await newsApi.feedback(id, feedback)
    } catch {
      // Revert on failure
      qc.invalidateQueries({ queryKey })
    }
  }

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-gray-900">{topicName}</h2>
        <button
          onClick={handleScan}
          disabled={!!isActiveScan}
          className="flex items-center gap-2 px-4 py-2 bg-indigo-600 text-white text-sm font-medium
            rounded-lg hover:bg-indigo-700 disabled:opacity-60 disabled:cursor-not-allowed transition-colors"
        >
          {isActiveScan
            ? <Loader2 className="w-4 h-4 animate-spin" />
            : <RefreshCw className="w-4 h-4" />}
          {isActiveScan ? 'Scanning…' : 'Scan Now'}
        </button>
      </div>

      {/* Scan progress banner */}
      {isActiveScan && progress && (
        <div className="bg-indigo-50 border border-indigo-100 rounded-xl p-4 space-y-2">
          <div className="flex items-center gap-2 text-sm font-medium text-indigo-700">
            <Zap className="w-4 h-4" />
            Scanning {progress.currentSource}
          </div>
          <div className="w-full bg-indigo-100 rounded-full h-1.5">
            <div
              className="bg-indigo-600 h-1.5 rounded-full transition-all"
              style={{
                width: progress.totalSources > 0
                  ? `${(progress.sourcesCompleted / progress.totalSources) * 100}%`
                  : '10%',
              }}
            />
          </div>
          <div className="text-xs text-indigo-500">
            {progress.sourcesCompleted}/{progress.totalSources} sources · {progress.postsFetched} posts · {progress.newsItemsCreated} items
          </div>
        </div>
      )}

      {/* News feed */}
      {isLoading ? (
        <div className="flex justify-center py-20">
          <Loader2 className="w-6 h-6 animate-spin text-gray-400" />
        </div>
      ) : !data?.items.length ? (
        <div className="text-center py-20 text-gray-400">
          <Newspaper className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">No news yet. Add sources and click Scan Now.</p>
        </div>
      ) : (
        <div className="space-y-4">
          {data.items.map((item) => (
            <article
              key={item.id}
              className={`bg-white rounded-xl border shadow-sm overflow-hidden transition-colors ${
                item.userFeedback === 'Liked'
                  ? 'border-green-200'
                  : 'border-gray-100'
              }`}
            >
              <div className="p-5">
                <div className="flex items-start gap-3">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-2">
                      <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${
                        item.sourceType === 'Reddit'
                          ? 'bg-orange-50 text-orange-700'
                          : 'bg-sky-50 text-sky-700'
                      }`}>
                        {item.sourceType === 'Reddit' ? 'r/' : 'X'} {item.sourceDisplayName}
                      </span>
                      {item.topicName && (
                        <span className="text-xs px-2 py-0.5 rounded-full bg-gray-50 text-gray-500">
                          {item.topicName}
                        </span>
                      )}
                      <span className="text-xs text-gray-400 ml-auto">
                        {new Date(item.publishedAt).toLocaleDateString()}
                      </span>
                    </div>
                    {item.externalUrl ? (
                      <a
                        href={item.externalUrl}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="text-base font-semibold text-gray-900 hover:text-indigo-700 line-clamp-2 transition-colors"
                      >
                        {item.title}
                      </a>
                    ) : (
                      <h3 className="text-base font-semibold text-gray-900 line-clamp-2">{item.title}</h3>
                    )}
                  </div>

                  {/* Feedback buttons + relevance */}
                  <div className="flex flex-col items-end gap-1 shrink-0">
                    <FeedbackButtons item={item} onFeedback={handleFeedback} />
                    {item.relevance > 0 && (
                      <span className="text-xs text-gray-300">{Math.round(item.relevance * 100)}%</span>
                    )}
                  </div>
                </div>

                {item.aiSummary && (
                  <p className="mt-2 text-sm text-gray-600 leading-relaxed">{item.aiSummary}</p>
                )}

                {item.aiInsights && item.aiInsights.length > 0 && (
                  <ul className="mt-3 space-y-1">
                    {item.aiInsights.map((ins, i) => (
                      <li key={i} className="flex items-start gap-1.5 text-xs text-gray-500">
                        <span className="text-indigo-400 mt-0.5">•</span>
                        {ins}
                      </li>
                    ))}
                  </ul>
                )}
              </div>

              {item.extractedLinks.length > 0 && (
                <div className="px-5 pb-4 space-y-2 border-t border-gray-50 pt-3">
                  {item.extractedLinks.map((link) => (
                    <div key={link.id} className="flex items-start gap-2">
                      <span className={`shrink-0 flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium ${linkTypeBg(link.linkType)}`}>
                        <LinkTypeIcon type={link.linkType} />
                        {link.linkType}
                      </span>
                      <div className="flex-1 min-w-0">
                        <a
                          href={link.url}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="text-xs font-medium text-gray-700 hover:text-indigo-600 truncate flex items-center gap-1"
                        >
                          {link.title || link.url}
                          <ExternalLink className="w-3 h-3 shrink-0" />
                        </a>
                        {link.summary && (
                          <p className="text-xs text-gray-500 mt-0.5 line-clamp-2">{link.summary}</p>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </article>
          ))}
        </div>
      )}
    </div>
  )
}
