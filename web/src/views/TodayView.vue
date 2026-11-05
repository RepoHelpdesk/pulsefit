<script setup lang="ts">
import { onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useAsync } from '@/composables/useAsync'
import { classesService } from '@/services/classes'
import { frontDeskService } from '@/services/frontDesk'
import { billingService } from '@/services/billing'
import { gymService } from '@/services/gym'
import { addDays, formatDateTime, formatMoney, formatTime, startOfDay } from '@/utils/format'
import PageHeader from '@/components/ui/PageHeader.vue'
import Card from '@/components/ui/Card.vue'
import StatTile from '@/components/ui/StatTile.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const auth = useAuthStore()
const today = startOfDay(new Date())
const isTrainer = auth.staff?.role === 'Trainer'

const classes = useAsync(() => classesService.list(today, addDays(today, 1), null, isTrainer))
const report = useAsync(() => (auth.can('ViewReports') ? gymService.report() : Promise.resolve(null)))
const recent = useAsync(() => (auth.can('CheckIn') ? frontDeskService.recent(null, 8) : Promise.resolve([])))
const failed = useAsync(() => (auth.can('ViewMembers') ? billingService.invoices('Failed', '', 0, 8) : Promise.resolve(null)))
const refunds = useAsync(() => (auth.can('ApproveRefund') ? billingService.refunds('Pending') : Promise.resolve([])))

onMounted(() => [classes, report, recent, failed, refunds].forEach((x) => x.run()))

const greeting = (() => {
  const h = new Date().getHours()
  return h < 12 ? 'Good morning' : h < 18 ? 'Good afternoon' : 'Good evening'
})()
</script>

<template>
  <PageHeader :title="`${greeting}, ${auth.staff?.name.split(' ')[0]}`" :subtitle="today.toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' })" />

  <div v-if="report.data.value" class="mb-6 grid grid-cols-2 gap-4 lg:grid-cols-4">
    <StatTile label="Active members" :value="report.data.value.activeMembers" :hint="`${report.data.value.newLast30Days} joined in 30 days`" />
    <StatTile label="Revenue, last 30 days" :value="formatMoney(report.data.value.revenueLast30Days)" />
    <StatTile label="Failed payments" :value="formatMoney(report.data.value.failedOutstanding)" tone="warn" hint="Unpaid, across all members" />
    <StatTile label="Class fill rate" :value="`${report.data.value.classFillPercent}%`" hint="Last 7 days" />
  </div>

  <div v-if="refunds.data.value?.length" class="mb-6 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm">
    <div class="font-semibold text-amber-900">{{ refunds.data.value.length }} refund(s) waiting for your approval</div>
    <ul class="mt-1 text-amber-900">
      <li v-for="r in refunds.data.value" :key="r.id">
        <RouterLink :to="`/members/${r.memberId}`" class="font-medium underline">{{ r.memberName }}</RouterLink>
        — {{ formatMoney(r.amount) }} of “{{ r.invoiceDescription }}”, asked by {{ r.requestedBy }}.
        <RouterLink to="/billing" class="underline">Review</RouterLink>
      </li>
    </ul>
  </div>

  <div class="grid gap-6 lg:grid-cols-3">
    <Card :title="isTrainer ? 'Your classes today' : 'Classes today'" :padded="false" class="lg:col-span-2">
      <table class="table-base">
        <thead><tr><th>Time</th><th>Class</th><th>Gym</th><th>Trainer</th><th class="text-right">Booked</th></tr></thead>
        <tbody>
          <tr v-for="c in classes.data.value ?? []" :key="c.id" class="cursor-pointer hover:bg-slate-50" @click="$router.push(`/classes/${c.id}`)">
            <td class="tabular-nums">{{ formatTime(c.startsAt) }}</td>
            <td class="font-medium">{{ c.name }} <StatusBadge v-if="c.cancelled" status="Cancelled" /></td>
            <td>{{ c.location }}</td>
            <td>{{ c.trainer }}</td>
            <td class="text-right tabular-nums">
              {{ c.booked }}/{{ c.capacity }}<span v-if="c.waitlisted" class="ml-1 text-xs text-amber-700">+{{ c.waitlisted }} waiting</span>
            </td>
          </tr>
          <tr v-if="classes.data.value && !classes.data.value.length"><td colspan="5" class="text-slate-500">No classes today.</td></tr>
        </tbody>
      </table>
    </Card>

    <div class="space-y-6">
      <Card v-if="auth.can('CheckIn')" title="At the door" :padded="false">
        <ul class="divide-y divide-slate-100 text-sm">
          <li v-for="c in recent.data.value ?? []" :key="c.id" class="px-4 py-2.5">
            <div class="flex items-center justify-between">
              <RouterLink :to="`/members/${c.memberId}`" class="font-medium hover:underline">{{ c.memberName }}</RouterLink>
              <StatusBadge :status="c.allowed ? 'Active' : 'Denied'" />
            </div>
            <div class="text-xs text-slate-500">{{ c.location }} · {{ formatDateTime(c.at) }}</div>
            <div v-if="c.deniedReason" class="mt-0.5 text-xs text-rose-700">{{ c.deniedReason }}</div>
          </li>
        </ul>
      </Card>

      <Card v-if="failed.data.value" title="Failed payments" :padded="false">
        <ul class="divide-y divide-slate-100 text-sm">
          <li v-for="i in failed.data.value.items" :key="i.id" class="flex items-center justify-between px-4 py-2.5">
            <div>
              <RouterLink :to="`/members/${i.memberId}`" class="font-medium hover:underline">{{ i.memberName }}</RouterLink>
              <div class="text-xs text-slate-500">{{ i.failureReason }} · {{ formatDateTime(i.issuedAt) }}</div>
            </div>
            <span class="tabular-nums">{{ formatMoney(i.amount) }}</span>
          </li>
        </ul>
      </Card>
    </div>
  </div>
</template>
