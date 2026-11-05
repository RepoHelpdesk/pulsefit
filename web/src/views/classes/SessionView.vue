<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { classesService } from '@/services/classes'
import { useAction } from '@/composables/useAsync'
import type { MemberRow, SessionDetail } from '@/types'
import { formatDateTime } from '@/utils/format'
import PageHeader from '@/components/ui/PageHeader.vue'
import Card from '@/components/ui/Card.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import Alert from '@/components/ui/Alert.vue'
import Modal from '@/components/ui/Modal.vue'
import MemberPicker from '@/components/members/MemberPicker.vue'

const props = defineProps<{ id: number }>()
const auth = useAuthStore()
const detail = ref<SessionDetail | null>(null)
const action = useAction()
const cancelling = ref(false)
const cancelReason = ref('')

const load = async () => { detail.value = await classesService.get(props.id) }
onMounted(load)

const s = computed(() => detail.value?.session)
const started = computed(() => !!s.value && new Date(s.value.startsAt) <= new Date())
const ended = computed(() => !!s.value && new Date(s.value.startsAt).getTime() + s.value.durationMinutes * 60_000 <= Date.now())
const canMark = computed(() => auth.can('MarkAttendance') && (auth.staff?.role !== 'Trainer' || s.value?.trainerId === auth.staff?.id))

async function book(m: MemberRow) {
  const d = await action.act(() => classesService.book(props.id, m.id))
  if (d) detail.value = d
}
async function cancelBooking(bookingId: number) {
  if ((await action.act(() => classesService.cancelBooking(bookingId))) !== null) await load()
}
async function mark(bookingId: number, attended: boolean) {
  if ((await action.act(() => classesService.markAttendance(bookingId, attended))) !== null) await load()
}
async function cancelClass() {
  const d = await action.act(() => classesService.cancelSession(props.id, cancelReason.value))
  if (d) { detail.value = d; cancelling.value = false }
}
</script>

<template>
  <template v-if="s && detail">
    <PageHeader :title="s.name" :subtitle="`${formatDateTime(s.startsAt)} · ${s.durationMinutes} min · ${s.location}, ${s.room} · ${s.trainer}`">
      <template #before><RouterLink to="/classes" class="mb-1 inline-block text-xs text-slate-500 hover:underline">← Classes</RouterLink></template>
      <StatusBadge v-if="s.cancelled" status="Cancelled" />
      <button v-if="auth.can('ManageClasses') && !s.cancelled && !started" class="btn-danger" @click="cancelling = true">Cancel class</button>
    </PageHeader>

    <Alert v-if="action.error.value" class="mb-4">{{ action.error.value }}</Alert>

    <div class="grid gap-6 lg:grid-cols-3">
      <Card :title="`Roster · ${s.booked}/${s.capacity} booked${s.waitlisted ? `, ${s.waitlisted} waiting` : ''}`" :padded="false" class="lg:col-span-2">
        <table class="table-base">
          <thead><tr><th>#</th><th>Member</th><th>Plan</th><th>Booked</th><th>Status</th><th /></tr></thead>
          <tbody>
            <tr v-for="(r, i) in detail.roster" :key="r.bookingId">
              <td class="text-slate-400 tabular-nums">{{ i + 1 }}</td>
              <td><RouterLink :to="`/members/${r.memberId}`" class="font-medium hover:underline">{{ r.memberName }}</RouterLink></td>
              <td>{{ r.plan }}</td>
              <td class="whitespace-nowrap text-xs text-slate-500">
                {{ formatDateTime(r.createdAt) }}
                <div v-if="r.promotedAt">Off the waitlist {{ formatDateTime(r.promotedAt) }}</div>
              </td>
              <td><StatusBadge :status="r.status" /></td>
              <td class="whitespace-nowrap text-right">
                <template v-if="canMark && started && ['Booked', 'Attended', 'NoShow'].includes(r.status)">
                  <button class="btn-ghost px-2 py-1 text-xs" :disabled="action.busy.value || r.status === 'Attended'" @click="mark(r.bookingId, true)">Came</button>
                  <button class="btn-ghost px-2 py-1 text-xs" :disabled="action.busy.value || r.status === 'NoShow'" @click="mark(r.bookingId, false)">No-show</button>
                </template>
                <button
                  v-if="auth.can('BookClasses') && !ended && ['Booked', 'Waitlisted'].includes(r.status)"
                  class="btn-ghost px-2 py-1 text-xs"
                  :disabled="action.busy.value"
                  @click="cancelBooking(r.bookingId)"
                >Cancel</button>
              </td>
            </tr>
            <tr v-if="!detail.roster.length"><td colspan="6" class="text-slate-500">Nobody booked yet.</td></tr>
          </tbody>
        </table>
      </Card>

      <Card v-if="auth.can('BookClasses') && !started && !s.cancelled" title="Book a member">
        <MemberPicker placeholder="Find a member to book…" @pick="book" />
        <p class="mt-3 text-xs text-slate-500">Basic doesn't include classes. Plus includes 4 a month; Premium is unlimited. A full class puts the member on the waitlist.</p>
      </Card>
    </div>

    <Modal title="Cancel this class" :open="cancelling" @close="cancelling = false">
      <p class="text-sm text-slate-600">Everyone booked or waiting is taken off. Nobody is charged.</p>
      <label class="block"><span class="label">Reason</span><input v-model="cancelReason" class="field" placeholder="Trainer sick, room closed…" /></label>
      <template #footer>
        <button class="btn-secondary" @click="cancelling = false">Back</button>
        <button class="btn-danger" :disabled="action.busy.value || !cancelReason" @click="cancelClass">Cancel class</button>
      </template>
    </Modal>
  </template>
</template>
