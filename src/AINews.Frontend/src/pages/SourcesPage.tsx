import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { sourcesApi } from '../api/sources'
import { topicsApi } from '../api/topics'
import { Plus, Trash2, ToggleLeft, ToggleRight, Loader2 } from 'lucide-react'

const DEFAULT_REDDIT_CONFIG = JSON.stringify({ subreddit: '', limit: 100 }, null, 2)
const DEFAULT_X_CONFIG = JSON.stringify({ query: '-is:retweet lang:en', maxResults: 100 }, null, 2)

export function SourcesPage() {
  const qc = useQueryClient()
  const { data: sources = [], isLoading } = useQuery({
    queryKey: ['sources'],
    queryFn: () => sourcesApi.list(),
  })
  const { data: topics = [] } = useQuery({ queryKey: ['topics'], queryFn: topicsApi.list })

  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState({
    topicId: '',
    type: 'Reddit',
    displayName: '',
    config: DEFAULT_REDDIT_CONFIG,
  })

  const createMutation = useMutation({
    mutationFn: () =>
      sourcesApi.create({
        topicId: parseInt(form.topicId),
        type: form.type,
        displayName: form.displayName,
        config: form.config,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['sources'] })
      setShowForm(false)
      setForm({ topicId: '', type: 'Reddit', displayName: '', config: DEFAULT_REDDIT_CONFIG })
    },
  })

  const deleteMutation = useMutation({
    mutationFn: sourcesApi.delete,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['sources'] }),
  })

  const toggleMutation = useMutation({
    mutationFn: sourcesApi.toggle,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['sources'] }),
  })

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-gray-900">Sources</h2>
        <button
          onClick={() => setShowForm(true)}
          className="flex items-center gap-2 px-3 py-1.5 bg-indigo-600 text-white text-sm rounded-lg hover:bg-indigo-700"
        >
          <Plus className="w-4 h-4" /> Add Source
        </button>
      </div>

      {showForm && (
        <form
          onSubmit={(e) => { e.preventDefault(); createMutation.mutate() }}
          className="bg-white border border-gray-100 rounded-xl p-5 space-y-4 shadow-sm"
        >
          <h3 className="font-medium text-gray-900">New Source</h3>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">Topic</label>
              <select
                required
                value={form.topicId}
                onChange={(e) => setForm({ ...form, topicId: e.target.value })}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm"
              >
                <option value="">Select topic…</option>
                {topics.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">Type</label>
              <select
                value={form.type}
                onChange={(e) => setForm({
                  ...form,
                  type: e.target.value,
                  config: e.target.value === 'Reddit' ? DEFAULT_REDDIT_CONFIG : DEFAULT_X_CONFIG,
                })}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm"
              >
                <option value="Reddit">Reddit</option>
                <option value="X">X (Twitter)</option>
              </select>
            </div>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">Display Name</label>
            <input
              required
              value={form.displayName}
              onChange={(e) => setForm({ ...form, displayName: e.target.value })}
              placeholder={form.type === 'Reddit' ? 'r/ClaudeAI' : 'Claude Code keyword'}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">Config (JSON)</label>
            <textarea
              required
              rows={4}
              value={form.config}
              onChange={(e) => setForm({ ...form, config: e.target.value })}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm font-mono text-xs"
            />
            <p className="text-xs text-gray-400 mt-1">
              {form.type === 'Reddit'
                ? 'Reddit: {"subreddit":"ClaudeAI","limit":100}'
                : 'X: {"query":"Claude Code -is:retweet lang:en","maxResults":100}'}
            </p>
          </div>
          <div className="flex gap-2 pt-1">
            <button
              type="submit"
              disabled={createMutation.isPending}
              className="px-4 py-2 bg-indigo-600 text-white text-sm rounded-lg hover:bg-indigo-700 disabled:opacity-60"
            >
              {createMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Add Source'}
            </button>
            <button type="button" onClick={() => setShowForm(false)} className="px-4 py-2 text-sm text-gray-600 hover:text-gray-900">
              Cancel
            </button>
          </div>
        </form>
      )}

      {isLoading ? (
        <div className="flex justify-center py-10"><Loader2 className="w-5 h-5 animate-spin text-gray-400" /></div>
      ) : !sources.length ? (
        <div className="text-center py-16 text-gray-400 text-sm">No sources yet. Add your first one above.</div>
      ) : (
        <div className="space-y-2">
          {sources.map((s) => {
            const topic = topics.find((t) => t.id === s.topicId)
            return (
              <div key={s.id} className="bg-white border border-gray-100 rounded-xl px-4 py-3 flex items-center gap-3 shadow-sm">
                <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${
                  s.type === 'Reddit' ? 'bg-orange-50 text-orange-700' : 'bg-sky-50 text-sky-700'
                }`}>{s.type}</span>
                <div className="flex-1 min-w-0">
                  <div className="text-sm font-medium text-gray-900 truncate">{s.displayName}</div>
                  {topic && <div className="text-xs text-gray-400">{topic.name}</div>}
                </div>
                {s.lastScannedAt && (
                  <div className="text-xs text-gray-400 hidden sm:block">
                    Last: {new Date(s.lastScannedAt).toLocaleDateString()}
                  </div>
                )}
                <button
                  onClick={() => toggleMutation.mutate(s.id)}
                  className={`p-1 rounded transition-colors ${s.isActive ? 'text-indigo-500' : 'text-gray-300'}`}
                  title={s.isActive ? 'Disable' : 'Enable'}
                >
                  {s.isActive ? <ToggleRight className="w-5 h-5" /> : <ToggleLeft className="w-5 h-5" />}
                </button>
                <button
                  onClick={() => { if (confirm('Delete this source?')) deleteMutation.mutate(s.id) }}
                  className="p-1 rounded text-gray-300 hover:text-red-500 transition-colors"
                >
                  <Trash2 className="w-4 h-4" />
                </button>
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}
