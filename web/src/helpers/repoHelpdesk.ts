// The RepoHelpdesk chat widget. Staff ask it about members, bills and rules; it answers from this
// app's code. The script URL and key come from the API's config (RepoHelpdesk:*), so the widget can
// be turned on or pointed elsewhere without rebuilding the app.

import type { AppConfig } from '@/types'

let installed = false

export function installRepoHelpdeskWidget(config: AppConfig) {
  if (installed || !config.widgetScriptUrl || !config.widgetKey) return
  installed = true
  const script = document.createElement('script')
  script.async = true
  script.src = config.widgetScriptUrl
  script.dataset.key = config.widgetKey
  document.body.appendChild(script)
}
