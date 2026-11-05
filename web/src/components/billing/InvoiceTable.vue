<script setup lang="ts">
// Bills, with waive (fees only) and refund (paid charges) where the role allows.
import { reactive, ref } from 'vue'
import { useAuthStore } from '@/stores/auth'
import type { Invoice } from '@/types'
import { formatDateTime, formatMoney, humanize } from '@/utils/format'
import { MANAGER_REFUND_LIMIT, WAIVABLE_KINDS } from '@/utils/rules'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import Modal from '@/components/ui/Modal.vue'
import Alert from '@/components/ui/Alert.vue'

defineProps<{ invoices: Invoice[]; showMember?: boolean; busy?: boolean; error?: string | null }>()
const emit = defineEmits<{ waive: [invoiceId: number, reason: string]; refund: [invoiceId: number, amount: number, reason: string]; clearError: [] }>()

const auth = useAuthStore()
const target = ref<{ invoice: Invoice; mode: 'waive' | 'refund' } | null>(null)
const form = reactive({ reason: '', amount: 0 })

const canWaive = (i: Invoice) => auth.can('WaiveFee') && (WAIVABLE_KINDS as readonly string[]).includes(i.kind) && ['Paid', 'Failed', 'Open'].includes(i.status)
const canRefund = (i: Invoice) => auth.can('RequestRefund') && ['Paid', 'PartiallyRefunded'].includes(i.status)

function start(invoice: Invoice, mode: 'waive' | 'refund') {
  emit('clearError')
  form.reason = ''
  form.amount = invoice.amount - invoice.refundedAmount
  target.value = { invoice, mode }
}

function submit() {
  const t = target.value
  if (!t) return
  if (t.mode === 'waive') emit('waive', t.invoice.id, form.reason)
  else emit('refund', t.invoice.id, form.amount, form.reason)
}

defineExpose({ close: () => { target.value = null } })
</script>

<template>
  <div class="overflow-x-auto">
    <table class="table-base">
      <thead>
        <tr><th>Date</th><th v-if="showMember">Member</th><th>What</th><th class="text-right">Amount</th><th>Status</th><th /></tr>
      </thead>
      <tbody>
        <tr v-for="i in invoices" :key="i.id">
          <td class="whitespace-nowrap tabular-nums text-slate-600">{{ formatDateTime(i.issuedAt) }}</td>
          <td v-if="showMember"><RouterLink :to="`/members/${i.memberId}`" class="font-medium hover:underline">{{ i.memberName }}</RouterLink></td>
          <td>
            <div>{{ i.description }}</div>
            <div class="text-xs text-slate-500">{{ humanize(i.kind) }}<span v-if="i.failureReason" class="text-rose-700"> · {{ i.failureReason }}</span></div>
          </td>
          <td class="text-right tabular-nums">
            {{ formatMoney(i.amount) }}
            <div v-if="i.refundedAmount" class="text-xs text-violet-700">−{{ formatMoney(i.refundedAmount) }} refunded</div>
          </td>
          <td><StatusBadge :status="i.status" /></td>
          <td class="whitespace-nowrap text-right">
            <button v-if="canWaive(i)" class="btn-ghost px-2 py-1 text-xs" @click="start(i, 'waive')">Waive</button>
            <button v-if="canRefund(i)" class="btn-ghost px-2 py-1 text-xs" @click="start(i, 'refund')">Refund</button>
          </td>
        </tr>
        <tr v-if="!invoices.length"><td :colspan="showMember ? 6 : 5" class="text-slate-500">No bills.</td></tr>
      </tbody>
    </table>
  </div>

  <Modal :title="target?.mode === 'waive' ? 'Waive fee' : 'Refund'" :open="!!target" @close="target = null">
    <p class="text-sm text-slate-600">{{ target?.invoice.description }} — {{ formatMoney(target?.invoice.amount ?? 0) }}</p>
    <p v-if="target?.mode === 'waive' && auth.staff?.role === 'FrontDesk'" class="text-xs text-slate-500">The front desk can waive one fee per member every 90 days.</p>
    <p v-if="target?.mode === 'refund' && auth.staff?.role === 'Manager'" class="text-xs text-slate-500">Refunds over {{ formatMoney(MANAGER_REFUND_LIMIT) }} go to the owner for approval.</p>
    <label v-if="target?.mode === 'refund'" class="block"><span class="label">Amount</span><input v-model.number="form.amount" type="number" step="0.01" min="0.01" class="field" /></label>
    <label class="block"><span class="label">Reason</span><input v-model="form.reason" class="field" /></label>
    <Alert v-if="error">{{ error }}</Alert>
    <template #footer>
      <button class="btn-secondary" @click="target = null">Back</button>
      <button class="btn-primary" :disabled="busy || !form.reason" @click="submit">{{ target?.mode === 'waive' ? 'Waive' : 'Refund' }}</button>
    </template>
  </Modal>
</template>
