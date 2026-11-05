<script setup lang="ts">
import { reactive, watchEffect } from 'vue'
import { useRouter } from 'vue-router'
import { useGymStore } from '@/stores/gym'
import { membersService } from '@/services/members'
import { useAction } from '@/composables/useAsync'
import type { BillingCycle, DiscountType, NewMember } from '@/types'
import { formatMoney } from '@/utils/format'
import PageHeader from '@/components/ui/PageHeader.vue'
import Card from '@/components/ui/Card.vue'
import Alert from '@/components/ui/Alert.vue'

const gym = useGymStore()
const router = useRouter()
const save = useAction()
const form = reactive<NewMember>({
  firstName: '', lastName: '', email: '', phone: '', homeLocationId: 0, planId: 0,
  billingCycle: 'Monthly' as BillingCycle, discount: 'None' as DiscountType, cardLast4: '',
})

watchEffect(() => {
  if (!form.homeLocationId && gym.locations[0]) form.homeLocationId = gym.locations[0].id
  if (!form.planId && gym.plans[0]) form.planId = gym.plans[0].id
})

async function submit() {
  const m = await save.act(() => membersService.join(form))
  if (m) router.push(`/members/${m.id}`)
}
</script>

<template>
  <PageHeader title="New member" subtitle="The first month (or year) is charged to the card now." />
  <Card class="max-w-2xl">
    <form class="grid gap-4 sm:grid-cols-2" @submit.prevent="submit">
      <label><span class="label">First name</span><input v-model="form.firstName" required class="field" /></label>
      <label><span class="label">Last name</span><input v-model="form.lastName" required class="field" /></label>
      <label><span class="label">Email</span><input v-model="form.email" type="email" required class="field" /></label>
      <label><span class="label">Phone</span><input v-model="form.phone" class="field" /></label>
      <label>
        <span class="label">Home gym</span>
        <select v-model="form.homeLocationId" class="field"><option v-for="l in gym.locations" :key="l.id" :value="l.id">{{ l.name }}</option></select>
      </label>
      <label>
        <span class="label">Plan</span>
        <select v-model="form.planId" class="field">
          <option v-for="p in gym.plans" :key="p.id" :value="p.id">{{ p.name }} — {{ formatMoney(p.monthlyPrice) }}/mo or {{ formatMoney(p.yearlyPrice) }}/yr</option>
        </select>
      </label>
      <label>
        <span class="label">Billing</span>
        <select v-model="form.billingCycle" class="field"><option value="Monthly">Monthly</option><option value="Yearly">Yearly (12-month contract)</option></select>
      </label>
      <label>
        <span class="label">Discount</span>
        <select v-model="form.discount" class="field">
          <option value="None">None</option><option value="Student">Student (20%)</option><option value="Corporate">Corporate (15%)</option>
        </select>
      </label>
      <label><span class="label">Card, last 4 digits</span><input v-model="form.cardLast4" maxlength="4" required class="field" /></label>
      <div class="sm:col-span-2 space-y-3">
        <Alert v-if="save.error.value">{{ save.error.value }}</Alert>
        <button class="btn-primary" :disabled="save.busy.value">Sign up and charge</button>
      </div>
    </form>
  </Card>
</template>
