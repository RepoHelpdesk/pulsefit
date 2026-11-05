import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { authService } from '@/services/auth'
import type { Me, Permission } from '@/types'

export const useAuthStore = defineStore('auth', () => {
  const me = ref<Me | null>(null)
  const loaded = ref(false)

  const staff = computed(() => me.value?.staff ?? null)
  const can = (p: Permission) => me.value?.permissions.includes(p) ?? false

  async function load() {
    try {
      me.value = await authService.me()
    } catch {
      me.value = null
    } finally {
      loaded.value = true
    }
  }

  async function signIn(staffId: number) {
    me.value = await authService.signIn(staffId)
  }

  async function signOut() {
    await authService.signOut()
    me.value = null
  }

  /** The session ended on the server (expired, or the demo data was reset). */
  function forget() {
    me.value = null
  }

  return { me, loaded, staff, can, load, signIn, signOut, forget }
})
