// The one place the app talks to the API. A rule that says no comes back as an ApiError carrying the
// message the API wrote for staff, so screens can show it as it is.

export class ApiError extends Error {
  constructor(public status: number, public code: string, message: string) {
    super(message)
  }
}

/** Called on a 401 so the app can go back to sign-in. Set by the auth store. */
let onSignedOut: (() => void) | null = null
export function setSignedOutHandler(handler: () => void) {
  onSignedOut = handler
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const res = await fetch(`/api${path}`, {
    method,
    credentials: 'same-origin',
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (res.status === 401 && onSignedOut) onSignedOut()
  if (!res.ok) {
    const err = await res.json().catch(() => null)
    throw new ApiError(res.status, err?.code ?? 'error', err?.message ?? `Request failed (${res.status}).`)
  }
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}

/** Builds "?a=1&b=2", leaving out empty values. */
export function query(params: Record<string, string | number | boolean | null | undefined>) {
  const q = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) if (v !== null && v !== undefined && v !== '') q.set(k, String(v))
  const s = q.toString()
  return s ? `?${s}` : ''
}

export const http = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body: unknown = {}) => request<T>('POST', path, body),
  put: <T>(path: string, body: unknown) => request<T>('PUT', path, body),
  del: <T>(path: string) => request<T>('DELETE', path),
}
