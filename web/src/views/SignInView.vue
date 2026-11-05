<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useGymStore } from '@/stores/gym'
import { authService } from '@/services/auth'
import { useAction, useAsync } from '@/composables/useAsync'
import type { StaffRole } from '@/types'

const auth = useAuthStore()
const gym = useGymStore()
const route = useRoute()
const router = useRouter()
const staff = useAsync(authService.staff)
const signIn = useAction()

const roles: { role: StaffRole; title: string; blurb: string }[] = [
  { role: 'Owner', title: 'Owner', blurb: 'Everything, including prices, refund approvals and background jobs.' },
  { role: 'Manager', title: 'Managers', blurb: 'Memberships and money: freezes, cancellations, plan changes, refunds up to $100.' },
  { role: 'FrontDesk', title: 'Front desk', blurb: 'Check-ins, guest passes, class bookings, one fee waiver per member every 90 days.' },
  { role: 'Trainer', title: 'Trainers', blurb: 'Their own classes and attendance.' },
]

const groups = computed(() => roles.map((r) => ({ ...r, people: (staff.data.value ?? []).filter((s) => s.role === r.role) })))

async function choose(id: number) {
  const ok = await signIn.act(() => auth.signIn(id))
  if (ok !== null) router.push(typeof route.query.next === 'string' ? route.query.next : '/')
}

onMounted(staff.run)
</script>

<template>
  <div class="min-h-screen bg-slate-50">
    <div class="mx-auto max-w-4xl px-4 py-12">
      <div class="mb-8 flex items-center gap-3">
        <img src="/favicon.svg" alt="" class="h-10 w-10" />
        <div>
          <h1 class="text-2xl font-semibold tracking-tight">{{ gym.config.gymName }} Manager</h1>
          <p class="text-sm text-slate-500">The back office for three gyms: Downtown, Riverside and Northgate.</p>
        </div>
      </div>

      <div class="mb-6 rounded-xl border border-indigo-200 bg-indigo-50 p-4 text-sm text-indigo-900">
        This is a demo, so there are no passwords. Pick who you want to be: each role sees and may do different things.
        <a v-if="gym.config.dashboardUrl" :href="gym.config.dashboardUrl" target="_blank" rel="noopener" class="ml-1 font-semibold underline">See the RepoHelpdesk dashboard →</a>
      </div>

      <p v-if="staff.error.value || signIn.error.value" class="mb-4 text-sm text-rose-700">{{ staff.error.value ?? signIn.error.value }}</p>

      <div class="grid gap-4 sm:grid-cols-2">
        <section v-for="g in groups" :key="g.role" class="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
          <h2 class="text-sm font-semibold">{{ g.title }}</h2>
          <p class="mb-3 mt-0.5 text-xs text-slate-500">{{ g.blurb }}</p>
          <div class="space-y-1.5">
            <button
              v-for="p in g.people"
              :key="p.id"
              class="flex w-full items-center justify-between rounded-md border border-slate-200 px-3 py-2 text-left text-sm hover:border-brand-500 hover:bg-brand-50 disabled:opacity-50"
              :disabled="signIn.busy.value"
              @click="choose(p.id)"
            >
              <span class="font-medium">{{ p.name }}</span>
              <span class="text-xs text-slate-500">{{ p.location ?? 'All gyms' }}</span>
            </button>
          </div>
        </section>
      </div>
    </div>
  </div>
</template>
