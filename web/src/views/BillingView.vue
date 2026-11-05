<script setup lang="ts">
import { ref, watch } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { billingService } from '@/services/billing'
import { useAction } from '@/composables/useAsync'
import type { Invoice, InvoiceKind, InvoiceStatus, Refund } from '@/types'
import { formatDateTime, formatMoney, humanize } from '@/utils/format'
import PageHeader from '@/components/ui/PageHeader.vue'
import Card from '@/components/ui/Card.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import Alert from '@/components/ui/Alert.vue'
import Pager from '@/components/ui/Pager.vue'
import InvoiceTable from '@/components/billing/InvoiceTable.vue'

const auth = useAuthStore()
const statuses: InvoiceStatus[] = ['Paid', 'Failed', 'Waived', 'Refunded', 'PartiallyRefunded']
const kinds: InvoiceKind[] = ['Membership', 'Proration', 'LateCancelFee', 'NoShowFee', 'FreezeFee', 'EarlyExitFee', 'DayPass']
const status = ref<InvoiceStatus | ''>('')
const kind = ref<InvoiceKind | ''>('')
const skip = ref(0)
const take = 25
const invoices = ref<Invoice[]>([])
const total = ref(0)
const refunds = ref<Refund[]>([])
const action = useAction()
const table = ref<InstanceType<typeof InvoiceTable> | null>(null)

async function load() {
  const page = await billingService.invoices(status.value, kind.value, skip.value, take)
  invoices.value = page.items
  total.value = page.total
  if (auth.can('RequestRefund')) refunds.value = await billingService.refunds()
}
watch([status, kind], () => { skip.value = 0; load() })
watch(skip, load, { immediate: true })

async function done(p: Promise<unknown>) {
  if ((await action.act(() => p)) !== null) { table.value?.close(); await load() }
}
const decide = (id: number, approve: boolean) => done(billingService.decide(id, approve))
</script>

<template>
  <PageHeader title="Billing" subtitle="Bills are taken from the card on file. A failed payment gives the member 5 days before access is suspended." />

  <Card v-if="refunds.length" title="Refunds" :padded="false" class="mb-6">
    <table class="table-base">
      <thead><tr><th>Asked</th><th>Member</th><th>Charge</th><th class="text-right">Refund</th><th>By</th><th>Status</th><th /></tr></thead>
      <tbody>
        <tr v-for="r in refunds" :key="r.id">
          <td class="whitespace-nowrap text-slate-600">{{ formatDateTime(r.createdAt) }}</td>
          <td><RouterLink :to="`/members/${r.memberId}`" class="font-medium hover:underline">{{ r.memberName }}</RouterLink></td>
          <td>{{ r.invoiceDescription }}<div class="text-xs text-slate-500">{{ r.reason }}</div></td>
          <td class="text-right tabular-nums">{{ formatMoney(r.amount) }}</td>
          <td>{{ r.requestedBy }}</td>
          <td><StatusBadge :status="r.status" /></td>
          <td class="whitespace-nowrap text-right">
            <template v-if="r.status === 'Pending' && auth.can('ApproveRefund')">
              <button class="btn-ghost px-2 py-1 text-xs text-emerald-700" :disabled="action.busy.value" @click="decide(r.id, true)">Approve</button>
              <button class="btn-ghost px-2 py-1 text-xs text-rose-700" :disabled="action.busy.value" @click="decide(r.id, false)">Reject</button>
            </template>
          </td>
        </tr>
      </tbody>
    </table>
  </Card>

  <Alert v-if="action.error.value" class="mb-4">{{ action.error.value }}</Alert>

  <Card :padded="false">
    <div class="flex flex-wrap gap-3 border-b border-slate-100 p-4">
      <select v-model="status" class="field w-auto"><option value="">Any status</option><option v-for="s in statuses" :key="s" :value="s">{{ humanize(s) }}</option></select>
      <select v-model="kind" class="field w-auto"><option value="">Any kind</option><option v-for="k in kinds" :key="k" :value="k">{{ humanize(k) }}</option></select>
    </div>
    <InvoiceTable
      ref="table"
      :invoices="invoices"
      show-member
      :busy="action.busy.value"
      :error="action.error.value"
      @clear-error="action.error.value = null"
      @waive="(id, r) => done(billingService.waive(id, r))"
      @refund="(id, a, r) => done(billingService.refund(id, a, r))"
    />
    <Pager v-model:skip="skip" :take="take" :total="total" />
  </Card>
</template>
