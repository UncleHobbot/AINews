import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { settingsApi } from '../api/settings'
import { topicsApi } from '../api/topics'
import { Save, Plus, Trash2, Loader2, Eye, EyeOff } from 'lucide-react'

const SETTING_LABELS: Record<string, string> = {
  'Google:ClientId': 'Google Client ID',
  'Google:ClientSecret': 'Google Client Secret',
  'X:AuthToken': 'X Auth Token (auth_token cookie)',
  'X:CsrfToken': 'X CSRF Token (ct0 cookie)',
  'ZAi:ApiKey': 'Z.ai API Key',
  'ZAi:BaseUrl': 'Z.ai Base URL',
  'OpenAi:ApiKey': 'OpenAI API Key',
}

export function SettingsPage() {
  const qc = useQueryClient()
  const { data: settings = [], isLoading } = useQuery({
    queryKey: ['settings'],
    queryFn: settingsApi.list,
  })
  const { data: topics = [] } = useQuery({ queryKey: ['topics'], queryFn: topicsApi.list })

  const [values, setValues] = useState<Record<string, string>>({})
  const [shown, setShown] = useState<Record<string, boolean>>({})
  const [topicForm, setTopicForm] = useState({ name: '', description: '' })
  const [showTopicForm, setShowTopicForm] = useState(false)

  const saveMutation = useMutation({
    mutationFn: () => settingsApi.update(values),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['settings'] })
      setValues({})
    },
  })

  const createTopicMutation = useMutation({
    mutationFn: () => topicsApi.create({ name: topicForm.name, description: topicForm.description || undefined }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['topics'] })
      setTopicForm({ name: '', description: '' })
      setShowTopicForm(false)
    },
  })

  const deleteTopicMutation = useMutation({
    mutationFn: topicsApi.delete,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['topics'] }),
  })

  const settingKeys = Object.keys(SETTING_LABELS)

  return (
    <div className="max-w-2xl mx-auto space-y-8">
      {/* API Keys */}
      <section className="bg-white rounded-xl border border-gray-100 shadow-sm p-6 space-y-4">
        <h2 className="text-lg font-semibold text-gray-900">API Keys</h2>
        {isLoading ? (
          <Loader2 className="w-5 h-5 animate-spin text-gray-400" />
        ) : (
          <div className="space-y-3">
            {settingKeys.map((key) => {
              const setting = settings.find((s) => s.key === key)
              const label = SETTING_LABELS[key]
              const isSecret = key.includes('Secret') || key.includes('Key') || key.includes('Token')
              return (
                <div key={key}>
                  <label className="block text-xs font-medium text-gray-600 mb-1">{label}</label>
                  <div className="flex gap-2">
                    <div className="relative flex-1">
                      <input
                        type={isSecret && !shown[key] ? 'password' : 'text'}
                        placeholder={setting?.isSet ? '••••••••' : 'Not set'}
                        value={values[key] ?? ''}
                        onChange={(e) => setValues({ ...values, [key]: e.target.value })}
                        className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm pr-8"
                      />
                      {isSecret && (
                        <button
                          type="button"
                          onClick={() => setShown({ ...shown, [key]: !shown[key] })}
                          className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                        >
                          {shown[key] ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                        </button>
                      )}
                    </div>
                    {setting?.isSet && <span className="self-center text-xs text-green-600 font-medium">Set</span>}
                  </div>
                </div>
              )
            })}
            <div className="rounded-lg bg-blue-50 border border-blue-100 p-3 text-xs text-blue-700 space-y-1">
              <p className="font-medium">How to get X Auth Token &amp; CSRF Token:</p>
              <ol className="list-decimal list-inside space-y-0.5 text-blue-600">
                <li>Log into x.com in your browser</li>
                <li>Open DevTools → Application → Cookies → https://x.com</li>
                <li>Copy <strong>auth_token</strong> value → paste into "X Auth Token"</li>
                <li>Copy <strong>ct0</strong> value → paste into "X CSRF Token"</li>
              </ol>
            </div>
            <div className="pt-2">
              <button
                onClick={() => saveMutation.mutate()}
                disabled={saveMutation.isPending || Object.keys(values).length === 0}
                className="flex items-center gap-2 px-4 py-2 bg-indigo-600 text-white text-sm rounded-lg hover:bg-indigo-700 disabled:opacity-60"
              >
                {saveMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                Save Changes
              </button>
            </div>
          </div>
        )}
      </section>

      {/* Topics */}
      <section className="bg-white rounded-xl border border-gray-100 shadow-sm p-6 space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold text-gray-900">Topics</h2>
          <button
            onClick={() => setShowTopicForm(true)}
            className="flex items-center gap-1 text-sm text-indigo-600 hover:text-indigo-700"
          >
            <Plus className="w-4 h-4" /> Add
          </button>
        </div>
        {showTopicForm && (
          <form
            onSubmit={(e) => { e.preventDefault(); createTopicMutation.mutate() }}
            className="flex gap-2"
          >
            <input
              required
              placeholder="Topic name"
              value={topicForm.name}
              onChange={(e) => setTopicForm({ ...topicForm, name: e.target.value })}
              className="flex-1 border border-gray-200 rounded-lg px-3 py-1.5 text-sm"
            />
            <button
              type="submit"
              disabled={createTopicMutation.isPending}
              className="px-3 py-1.5 bg-indigo-600 text-white text-sm rounded-lg hover:bg-indigo-700"
            >
              Add
            </button>
            <button type="button" onClick={() => setShowTopicForm(false)} className="text-sm text-gray-400">✕</button>
          </form>
        )}
        <div className="space-y-2">
          {topics.map((t) => (
            <div key={t.id} className="flex items-center justify-between py-1">
              <div>
                <span className="text-sm font-medium text-gray-800">{t.name}</span>
                <span className="text-xs text-gray-400 ml-2">{t.sourceCount} sources</span>
              </div>
              <button
                onClick={() => { if (confirm(`Delete topic "${t.name}"?`)) deleteTopicMutation.mutate(t.id) }}
                className="p-1 text-gray-300 hover:text-red-500 transition-colors"
              >
                <Trash2 className="w-4 h-4" />
              </button>
            </div>
          ))}
          {!topics.length && <p className="text-sm text-gray-400">No topics yet.</p>}
        </div>
      </section>
    </div>
  )
}
