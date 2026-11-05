<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useGymStore } from '@/stores/gym'
import { navigation } from './navigation'
import DemoBar from './DemoBar.vue'
import { humanize } from '@/utils/format'

const auth = useAuthStore()
const gym = useGymStore()
const router = useRouter()

const items = computed(() => navigation.filter((n) => !n.needs || auth.can(n.needs)))

async function switchUser() {
  await auth.signOut()
  router.push('/sign-in')
}
</script>

<template>
  <div class="flex min-h-screen flex-col">
    <DemoBar />
    <div class="flex flex-1">
      <aside class="hidden w-60 shrink-0 flex-col border-r border-slate-200 bg-white md:flex">
        <div class="flex items-center gap-2 px-5 py-5">
          <img src="/favicon.svg" alt="" class="h-8 w-8" />
          <div>
            <div class="text-sm font-semibold leading-tight">{{ gym.config.gymName }}</div>
            <div class="text-xs text-slate-500">Manager</div>
          </div>
        </div>
        <nav class="flex-1 space-y-0.5 px-3">
          <RouterLink
            v-for="item in items"
            :key="item.to"
            :to="item.to"
            class="flex items-center gap-2.5 rounded-md px-3 py-2 text-sm text-slate-600 hover:bg-slate-100"
            active-class="!bg-brand-50 !text-brand-700 font-medium"
            :exact-active-class="item.to === '/' ? '!bg-brand-50 !text-brand-700 font-medium' : ''"
          >
            <svg class="h-4 w-4 shrink-0" fill="none" stroke="currentColor" stroke-width="1.8" viewBox="0 0 24 24" stroke-linecap="round" stroke-linejoin="round">
              <path :d="item.icon" />
            </svg>
            {{ item.label }}
          </RouterLink>
        </nav>
        <div v-if="auth.staff" class="border-t border-slate-200 p-4">
          <div class="text-sm font-medium">{{ auth.staff.name }}</div>
          <div class="text-xs text-slate-500">{{ humanize(auth.staff.role) }}<span v-if="auth.staff.location"> · {{ auth.staff.location }}</span></div>
          <button class="mt-2 text-xs font-medium text-brand-600 hover:underline" @click="switchUser">Switch staff member</button>
        </div>
      </aside>

      <main class="min-w-0 flex-1">
        <!-- Small screens: a compact top nav instead of the sidebar. -->
        <nav class="flex gap-1 overflow-x-auto border-b border-slate-200 bg-white px-3 py-2 md:hidden">
          <RouterLink v-for="item in items" :key="item.to" :to="item.to" class="whitespace-nowrap rounded px-2.5 py-1.5 text-sm text-slate-600" active-class="bg-brand-50 text-brand-700">
            {{ item.label }}
          </RouterLink>
        </nav>
        <div class="mx-auto max-w-7xl px-4 py-6 md:px-8">
          <RouterView />
        </div>
      </main>
    </div>
  </div>
</template>
