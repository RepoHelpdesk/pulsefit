<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useGymStore } from '@/stores/gym'
import { gymService } from '@/services/gym'
import { useAction } from '@/composables/useAsync'
import type { Plan, PromoCode } from '@/types'
import { formatDate, formatMoney } from '@/utils/format'
import PageHeader from '@/components/ui/PageHeader.vue'
import Card from '@/components/ui/Card.vue'
import Modal from '@/components/ui/Modal.vue'
import Alert from '@/components/ui/Alert.vue'
import Badge from '@/components/ui/Badge.vue'

const auth = useAuthStore()
const gym = useGymStore()
const promos = ref<PromoCode[]>([])
const editing = ref<Plan | null>(null)
const form = reactive({ monthly: 0, yearly: 0 })
const save = useAction()

onMounted(async () => { promos.value = await gymService.promoCodes() })

function edit(p: Plan) {
  save.error.value = null
  form.monthly = p.monthlyPrice
  form.yearly = p.yearlyPrice
  editing.value = p
}

async function submit() {
  if (!editing.value) return
  if (await save.act(() => gymService.setPlanPrice(editing.value!.id, form.monthly, form.yearly))) {
    await gym.loadReference()
    editing.value = null
  }
}

const classes = (p: Plan) => (p.classesPerMonth === null ? 'Unlimited classes' : p.classesPerMonth === 0 ? 'No classes' : `${p.classesPerMonth} classes a month`)
</script>

<template>
  <PageHeader title="Plans & pricing" subtitle="Discounts: students 20%, corporate 15%. Promo codes don't stack with either." />

  <div class="mb-8 grid gap-4 md:grid-cols-3">
    <div v-for="p in gym.plans" :key="p.id" class="rounded-xl border border-slate-200 bg-white p-5 shadow-xs">
      <div class="flex items-center justify-between">
        <h2 class="text-lg font-semibold">{{ p.name }}</h2>
        <button v-if="auth.can('ManagePlans')" class="btn-ghost px-2 py-1 text-xs" @click="edit(p)">Edit prices</button>
      </div>
      <div class="mt-2 text-2xl font-semibold tabular-nums">{{ formatMoney(p.monthlyPrice) }}<span class="text-sm font-normal text-slate-500">/month</span></div>
      <div class="text-sm text-slate-500">or {{ formatMoney(p.yearlyPrice) }} a year, paid up front</div>
      <ul class="mt-4 space-y-1.5 text-sm text-slate-700">
        <li>{{ p.allLocations ? 'Every PulseFit gym' : 'Home gym only' }}</li>
        <li>{{ classes(p) }}</li>
        <li>{{ p.guestPassesPerMonth ? `${p.guestPassesPerMonth} guest passes a month` : 'No guest passes' }}</li>
      </ul>
    </div>
  </div>

  <Card title="Promo codes" flush>
    <table class="table-base">
      <thead><tr><th>Code</th><th>Discount</th><th>Lasts</th><th>Valid until</th><th /></tr></thead>
      <tbody>
        <tr v-for="c in promos" :key="c.id">
          <td class="font-mono font-medium">{{ c.code }}</td>
          <td>{{ c.percentOff }}% off</td>
          <td>{{ c.months }} bill(s)</td>
          <td>{{ formatDate(c.validUntil) }}</td>
          <td><Badge :tone="c.active && new Date(c.validUntil) >= new Date() ? 'green' : 'gray'">{{ c.active && new Date(c.validUntil) >= new Date() ? 'Active' : 'Expired' }}</Badge></td>
        </tr>
      </tbody>
    </table>
  </Card>

  <Modal :title="`${editing?.name} prices`" :open="!!editing" @close="editing = null">
    <p class="text-sm text-slate-600">New prices apply from each member's next bill.</p>
    <label class="block"><span class="label">Monthly</span><input v-model.number="form.monthly" type="number" step="1" class="field" /></label>
    <label class="block"><span class="label">Yearly</span><input v-model.number="form.yearly" type="number" step="1" class="field" /></label>
    <Alert v-if="save.error.value">{{ save.error.value }}</Alert>
    <template #footer>
      <button class="btn-secondary" @click="editing = null">Back</button>
      <button class="btn-primary" :disabled="save.busy.value" @click="submit">Save</button>
    </template>
  </Modal>
</template>
