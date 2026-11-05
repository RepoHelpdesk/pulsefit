<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useGymStore } from '@/stores/gym'
import { frontDeskService } from '@/services/frontDesk'
import { useAction } from '@/composables/useAsync'
import type { CheckIn, CheckInResult, MemberRow } from '@/types'
import { formatDateTime } from '@/utils/format'
import PageHeader from '@/components/ui/PageHeader.vue'
import Card from '@/components/ui/Card.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import Alert from '@/components/ui/Alert.vue'
import MemberPicker from '@/components/members/MemberPicker.vue'

const auth = useAuthStore()
const gym = useGymStore()
const locationId = ref<number | null>(null)
const member = ref<MemberRow | null>(null)
const result = ref<CheckInResult | null>(null)
const guestName = ref('')
const guestMessage = ref<string | null>(null)
const recent = ref<CheckIn[]>([])
const action = useAction()

// Start at the desk's own gym.
watch(() => gym.locations, (ls) => {
  if (locationId.value === null) locationId.value = ls.find((l) => l.name === auth.staff?.location)?.id ?? ls[0]?.id ?? null
}, { immediate: true })

const loadRecent = async () => { recent.value = await frontDeskService.recent(locationId.value, 15) }
watch(locationId, loadRecent)
onMounted(loadRecent)

function pick(m: MemberRow) {
  member.value = m
  result.value = null
  guestMessage.value = null
  action.error.value = null
}

async function checkIn() {
  if (!member.value || !locationId.value) return
  const r = await action.act(() => frontDeskService.checkIn(member.value!.id, locationId.value!))
  if (r) { result.value = r; await loadRecent() }
}

async function guest(buyDayPass: boolean) {
  if (!member.value || !locationId.value || !guestName.value) return
  const r = await action.act(() => frontDeskService.guest(member.value!.id, locationId.value!, guestName.value, buyDayPass))
  if (r) { guestMessage.value = r.message; guestName.value = '' }
}
</script>

<template>
  <PageHeader title="Front desk" subtitle="Check members in and bring their guests.">
    <select v-model="locationId" class="field w-auto">
      <option v-for="l in gym.locations" :key="l.id" :value="l.id">{{ l.name }}</option>
    </select>
  </PageHeader>

  <div class="grid gap-6 lg:grid-cols-3">
    <div class="space-y-6 lg:col-span-2">
      <Card title="Check in">
        <MemberPicker @pick="pick" />
        <div v-if="member" class="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-lg border border-slate-200 p-4">
          <div>
            <RouterLink :to="`/members/${member.id}`" class="text-base font-semibold hover:underline">{{ member.name }}</RouterLink>
            <div class="text-sm text-slate-500">{{ member.plan }} · home gym {{ member.homeLocation }}</div>
          </div>
          <div class="flex items-center gap-2">
            <StatusBadge :status="member.status" />
            <button class="btn-primary" :disabled="action.busy.value" @click="checkIn">Check in</button>
          </div>
        </div>
        <div v-if="result" class="mt-4 rounded-lg p-4 text-sm" :class="result.allowed ? 'bg-emerald-50 text-emerald-900' : 'bg-rose-50 text-rose-900'">
          <div class="text-base font-semibold">{{ result.allowed ? '✓ Let in' : '✕ Turned away' }}</div>
          <div class="mt-0.5">{{ result.message }}</div>
          <div v-if="result.classesMarked" class="mt-1">Marked as attending {{ result.classesMarked }} class(es).</div>
        </div>
      </Card>

      <Card v-if="member && auth.can('IssueGuestPass')" title="Bring a guest">
        <div class="flex flex-wrap gap-2">
          <input v-model="guestName" class="field max-w-xs" placeholder="Guest's name" />
          <button class="btn-secondary" :disabled="action.busy.value || !guestName" @click="guest(false)">Use a guest pass</button>
          <button class="btn-secondary" :disabled="action.busy.value || !guestName" @click="guest(true)">Sell a $15 day pass</button>
        </div>
        <p class="mt-2 text-xs text-slate-500">Premium includes 2 guest passes a month (they don't roll over). Otherwise the guest needs a day pass, charged to the member.</p>
        <Alert v-if="guestMessage" tone="success" class="mt-3">{{ guestMessage }}</Alert>
      </Card>

      <Alert v-if="action.error.value">{{ action.error.value }}</Alert>
    </div>

    <Card title="Recent at this gym" :padded="false">
      <ul class="divide-y divide-slate-100 text-sm">
        <li v-for="c in recent" :key="c.id" class="px-4 py-2.5">
          <div class="flex items-center justify-between">
            <RouterLink :to="`/members/${c.memberId}`" class="font-medium hover:underline">{{ c.memberName }}</RouterLink>
            <StatusBadge :status="c.allowed ? 'Active' : 'Denied'" />
          </div>
          <div class="text-xs text-slate-500">{{ formatDateTime(c.at) }}</div>
          <div v-if="c.deniedReason" class="mt-0.5 text-xs text-rose-700">{{ c.deniedReason }}</div>
        </li>
      </ul>
    </Card>
  </div>
</template>
