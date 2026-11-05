<script setup lang="ts">
import { ref, toRef } from 'vue'
import { useMember } from '@/composables/useMember'
import { formatDateTime, humanize } from '@/utils/format'
import PageHeader from '@/components/ui/PageHeader.vue'
import Card from '@/components/ui/Card.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import Alert from '@/components/ui/Alert.vue'
import MemberSummary from '@/components/members/MemberSummary.vue'
import MemberActions from '@/components/members/MemberActions.vue'
import InvoiceTable from '@/components/billing/InvoiceTable.vue'
import { useAuthStore } from '@/stores/auth'

const props = defineProps<{ id: number }>()
const auth = useAuthStore()
const m = useMember(toRef(props, 'id'))

const tabs = ['Bills', 'Classes', 'Visits', 'History'] as const
const tab = ref<(typeof tabs)[number]>('Bills')

const actions = ref<InstanceType<typeof MemberActions> | null>(null)
const bills = ref<InstanceType<typeof InvoiceTable> | null>(null)

/** Run a member action; close its dialog when it went through. */
async function run(ok: Promise<boolean>, dialog: { close: () => void } | null) {
  if (await ok) dialog?.close()
}
</script>

<template>
  <Alert v-if="m.loadError.value">{{ m.loadError.value }}</Alert>
  <template v-else-if="m.member.value">
    <PageHeader :title="`${m.member.value.firstName} ${m.member.value.lastName}`" :subtitle="`${m.member.value.plan.name} · ${m.member.value.homeLocation}`">
      <template #before>
        <RouterLink to="/members" class="mb-1 inline-block text-xs text-slate-500 hover:underline">← Members</RouterLink>
      </template>
      <StatusBadge :status="m.member.value.status" />
    </PageHeader>

    <div class="mb-4">
      <MemberActions
        ref="actions"
        :member="m.member.value"
        :busy="m.busy.value"
        :error="m.error.value"
        @clear-error="m.clearError"
        @freeze="(n) => run(m.freeze(n), actions)"
        @unfreeze="m.unfreeze()"
        @change-plan="(id) => run(m.changePlan(id), actions)"
        @cancel="(r) => run(m.cancel(r), actions)"
        @undo-cancel="m.undoCancel()"
        @apply-promo="(c) => run(m.applyPromo(c), actions)"
        @update-card="(l) => run(m.updateCard(l), actions)"
        @update-details="(p, l, n) => run(m.updateDetails(p, l, n), actions)"
      />
      <Alert v-if="m.error.value" class="mt-3">{{ m.error.value }}</Alert>
    </div>

    <div class="grid gap-6 lg:grid-cols-5">
      <div class="lg:col-span-2"><MemberSummary :member="m.member.value" /></div>

      <Card :padded="false" class="lg:col-span-3">
        <div class="flex gap-1 border-b border-slate-100 px-3 pt-2">
          <button
            v-for="t in tabs"
            :key="t"
            class="-mb-px border-b-2 px-3 py-2 text-sm"
            :class="tab === t ? 'border-brand-600 font-medium text-brand-700' : 'border-transparent text-slate-500 hover:text-slate-800'"
            @click="tab = t"
          >{{ t }}</button>
        </div>

        <InvoiceTable
          v-if="tab === 'Bills'"
          ref="bills"
          :invoices="m.member.value.invoices"
          :busy="m.busy.value"
          :error="m.error.value"
          @clear-error="m.clearError"
          @waive="(id, r) => run(m.waive(id, r), bills)"
          @refund="(id, a, r) => run(m.refund(id, a, r), bills)"
        />

        <table v-else-if="tab === 'Classes'" class="table-base">
          <thead><tr><th>When</th><th>Class</th><th>Gym</th><th>Status</th><th /></tr></thead>
          <tbody>
            <tr v-for="b in m.member.value.bookings" :key="b.id">
              <td class="whitespace-nowrap tabular-nums text-slate-600">{{ formatDateTime(b.startsAt) }}</td>
              <td><RouterLink :to="`/classes/${b.sessionId}`" class="hover:underline">{{ b.className }}</RouterLink></td>
              <td>{{ b.location }}</td>
              <td>
                <StatusBadge :status="b.status" />
                <div v-if="b.promotedAt" class="mt-0.5 text-xs text-slate-500">From the waitlist {{ formatDateTime(b.promotedAt) }}</div>
              </td>
              <td class="text-right">
                <button
                  v-if="auth.can('BookClasses') && (b.status === 'Booked' || b.status === 'Waitlisted') && new Date(b.startsAt) > new Date()"
                  class="btn-ghost px-2 py-1 text-xs"
                  :disabled="m.busy.value"
                  @click="m.cancelBooking(b.id)"
                >Cancel</button>
              </td>
            </tr>
            <tr v-if="!m.member.value.bookings.length"><td colspan="5" class="text-slate-500">No classes booked.</td></tr>
          </tbody>
        </table>

        <table v-else-if="tab === 'Visits'" class="table-base">
          <thead><tr><th>When</th><th>Gym</th><th>Result</th></tr></thead>
          <tbody>
            <tr v-for="v in m.member.value.visits" :key="v.id">
              <td class="whitespace-nowrap tabular-nums text-slate-600">{{ formatDateTime(v.at) }}</td>
              <td>{{ v.location }}</td>
              <td>
                <StatusBadge :status="v.allowed ? 'Active' : 'Denied'" />
                <div v-if="v.deniedReason" class="mt-0.5 text-xs text-rose-700">{{ v.deniedReason }}</div>
              </td>
            </tr>
          </tbody>
        </table>

        <ul v-else class="divide-y divide-slate-100">
          <li v-for="a in m.member.value.audit" :key="a.id" class="px-4 py-3 text-sm">
            <div class="flex flex-wrap items-center justify-between gap-2">
              <span class="font-medium">{{ humanize(a.action.replace('.', ' ').replace('_', ' ')) }}</span>
              <span class="text-xs text-slate-500">{{ formatDateTime(a.at) }} · {{ a.actor }}</span>
            </div>
            <p class="mt-0.5 text-slate-600">{{ a.details }}</p>
          </li>
          <li v-if="!m.member.value.audit.length" class="px-4 py-3 text-sm text-slate-500">Nothing recorded yet.</li>
        </ul>
      </Card>
    </div>
  </template>
</template>
