import { ref, shallowRef } from 'vue'

/**
 * Loads something and keeps its loading state and error message. `run` reloads; `act` performs a
 * change and keeps its own busy flag and error, so a failed action doesn't blank the screen.
 */
export function useAsync<T>(loader: () => Promise<T>) {
  const data = shallowRef<T | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function run() {
    loading.value = true
    error.value = null
    try {
      data.value = await loader()
    } catch (e) {
      error.value = e instanceof Error ? e.message : String(e)
    } finally {
      loading.value = false
    }
  }

  return { data, loading, error, run }
}

export function useAction() {
  const busy = ref(false)
  const error = ref<string | null>(null)

  /** Runs the change; returns its result, or null when it failed (the message is in `error`). */
  async function act<R>(fn: () => Promise<R>): Promise<R | null> {
    busy.value = true
    error.value = null
    try {
      return await fn()
    } catch (e) {
      error.value = e instanceof Error ? e.message : String(e)
      return null
    } finally {
      busy.value = false
    }
  }

  return { busy, error, act }
}
