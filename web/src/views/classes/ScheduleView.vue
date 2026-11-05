<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useGymStore } from '@/stores/gym'
import { useAuthStore } from '@/stores/auth'
import { classesService } from '@/services/classes'
import type { SessionRow } from '@/types'
import { addDays, formatDay, formatTime, startOfDay } from '@/utils/format'
import PageHeader from '@/components/ui/PageHeader.vue'

const gym = useGymStore()
const auth = useAuthStore()
const weekStart = ref(startOfDay(new Date()))
const locationId = ref<number | null>(null)
const mine = ref(auth.staff?.role === 'Trainer')
const sessions = ref<SessionRow[]>([])

watch([weekStart, locationId, mine], async () => {
  sessions.value = await classesService.list(weekStart.value, addDays(weekStart.value, 7), locationId.value, mine.value)
}, { immediate: true })

const days = computed(() => Array.from({ length: 7 }, (_, i) => {
  const day = addDays(weekStart.value, i)
  const next = addDays(day, 1)
  return { day, sessions: sessions.value.filter((s) => new Date(s.startsAt) >= day && new Date(s.startsAt) < next) }
}))

const fill = (s: SessionRow) => (s.booked >= s.capacity ? 'bg-rose-500' : s.booked / s.capacity > 0.75 ? 'bg-amber-500' : 'bg-emerald-500')
const isPast = (s: SessionRow) => new Date(s.startsAt).getTime() + s.durationMinutes * 60_000 < Date.now()
</script>

<template>
  <PageHeader title="Classes" subtitle="Booking opens 7 days before a class. Cancelling within 4 hours is a $10 late cancel.">
    <label v-if="auth.staff?.role === 'Trainer'" class="flex items-center gap-2 text-sm"><input v-model="mine" type="checkbox" /> Only mine</label>
    <select v-model="locationId" class="field w-auto">
      <option :value="null">All gyms</option>
      <option v-for="l in gym.locations" :key="l.id" :value="l.id">{{ l.name }}</option>
    </select>
    <div class="flex">
      <button class="btn-secondary rounded-r-none" @click="weekStart = addDays(weekStart, -7)">←</button>
      <button class="btn-secondary rounded-none border-x-0" @click="weekStart = startOfDay(new Date())">Today</button>
      <button class="btn-secondary rounded-l-none" @click="weekStart = addDays(weekStart, 7)">→</button>
    </div>
  </PageHeader>

  <div class="grid gap-3 md:grid-cols-7">
    <div v-for="d in days" :key="d.day.toISOString()" class="min-w-0">
      <div class="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500">{{ formatDay(d.day) }}</div>
      <div class="space-y-2">
        <RouterLink
          v-for="s in d.sessions"
          :key="s.id"
          :to="`/classes/${s.id}`"
          class="block rounded-lg border border-slate-200 bg-white p-2.5 text-xs shadow-xs hover:border-brand-500"
          :class="[isPast(s) && 'opacity-60', s.cancelled && 'line-through']"
        >
          <div class="font-semibold text-slate-800">{{ formatTime(s.startsAt) }} · {{ s.name }}</div>
          <div class="mt-0.5 text-slate-500">{{ s.location }} · {{ s.trainer }}</div>
          <div class="mt-1.5 flex items-center gap-2">
            <div class="h-1.5 flex-1 rounded-full bg-slate-100"><div class="h-1.5 rounded-full" :class="fill(s)" :style="{ width: `${Math.min(100, (100 * s.booked) / s.capacity)}%` }" /></div>
            <span class="tabular-nums text-slate-600">{{ s.booked }}/{{ s.capacity }}</span>
          </div>
          <div v-if="s.waitlisted" class="mt-1 text-amber-700">{{ s.waitlisted }} on waitlist</div>
        </RouterLink>
        <div v-if="!d.sessions.length" class="text-xs text-slate-400">No classes</div>
      </div>
    </div>
  </div>
</template>
