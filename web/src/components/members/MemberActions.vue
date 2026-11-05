<script setup lang="ts">
// The buttons along the top of a member, each opening a small form. Only what the signed-in role may do
// is shown; the API checks again and explains if a rule says no.
import { computed, reactive, ref } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useGymStore } from '@/stores/gym'
import type { MemberDetail } from '@/types'
import { formatMoney } from '@/utils/format'
import { FREEZE_FEE_PER_MONTH, YEARLY_EARLY_EXIT_FEE } from '@/utils/rules'
import Modal from '@/components/ui/Modal.vue'
import Alert from '@/components/ui/Alert.vue'

const props = defineProps<{ member: MemberDetail; busy: boolean; error: string | null }>()
const emit = defineEmits<{
  freeze: [months: number]; unfreeze: []; changePlan: [planId: number]; cancel: [reason: string]; undoCancel: []
  applyPromo: [code: string]; updateCard: [last4: string]; updateDetails: [phone: string, homeLocationId: number, notes: string]
  clearError: []
}>()

const auth = useAuthStore()
const gym = useGymStore()
type Dialog = 'freeze' | 'plan' | 'cancel' | 'promo' | 'card' | 'edit' | null
const open = ref<Dialog>(null)
const form = reactive({ months: 1, planId: 0, reason: '', code: '', last4: '', phone: '', homeLocationId: 0, notes: '' })

function show(d: Dialog) {
  emit('clearError')
  const m = props.member
  Object.assign(form, { months: 1, planId: m.plan.id, reason: '', code: '', last4: '', phone: m.phone, homeLocationId: m.homeLocationId, notes: m.notes })
  open.value = d
}

const active = computed(() => props.member.status !== 'Cancelled')
const otherPlans = computed(() => gym.plans.filter((p) => p.active))

/** The parent runs the change; close the dialog once it has gone through. */
defineExpose({ close: () => { open.value = null } })
</script>

<template>
  <div class="flex flex-wrap gap-2">
    <button v-if="auth.can('EditMembers')" class="btn-secondary" @click="show('edit')">Edit</button>
    <button v-if="auth.can('ChangePlan') && active" class="btn-secondary" @click="show('plan')">Change plan</button>
    <button v-if="auth.can('FreezeMembership') && member.status === 'Active'" class="btn-secondary" @click="show('freeze')">Freeze</button>
    <button v-if="auth.can('FreezeMembership') && member.status === 'Frozen'" class="btn-secondary" :disabled="busy" @click="emit('unfreeze')">Unfreeze</button>
    <button v-if="auth.can('EditMembers') && active" class="btn-secondary" @click="show('promo')">Promo code</button>
    <button v-if="auth.can('EditMembers') && active" class="btn-secondary" @click="show('card')">Update card</button>
    <button v-if="auth.can('CancelMembership') && active && !member.cancelEffectiveOn" class="btn-danger" @click="show('cancel')">Cancel membership</button>
    <button v-if="auth.can('CancelMembership') && active && member.cancelEffectiveOn" class="btn-secondary" :disabled="busy" @click="emit('undoCancel')">Undo cancellation</button>
  </div>

  <Modal title="Freeze membership" :open="open === 'freeze'" @close="open = null">
    <p class="text-sm text-slate-600">{{ member.firstName }} has {{ member.freezeMonthsLeftThisYear }} freeze month(s) left this year. While frozen they pay {{ formatMoney(FREEZE_FEE_PER_MONTH) }} a month and can't come in.</p>
    <label class="block"><span class="label">Months</span><input v-model.number="form.months" type="number" min="1" max="2" class="field" /></label>
    <Alert v-if="error">{{ error }}</Alert>
    <template #footer>
      <button class="btn-secondary" @click="open = null">Back</button>
      <button class="btn-primary" :disabled="busy" @click="emit('freeze', form.months)">Freeze</button>
    </template>
  </Modal>

  <Modal title="Change plan" :open="open === 'plan'" @close="open = null">
    <p class="text-sm text-slate-600">An upgrade starts now and the difference is charged for the rest of this cycle. A downgrade starts on the next billing date.</p>
    <label class="block">
      <span class="label">New plan</span>
      <select v-model="form.planId" class="field">
        <option v-for="p in otherPlans" :key="p.id" :value="p.id">
          {{ p.name }} — {{ formatMoney(member.billingCycle === 'Yearly' ? p.yearlyPrice : p.monthlyPrice) }}/{{ member.billingCycle === 'Yearly' ? 'yr' : 'mo' }}
        </option>
      </select>
    </label>
    <Alert v-if="error">{{ error }}</Alert>
    <template #footer>
      <button class="btn-secondary" @click="open = null">Back</button>
      <button class="btn-primary" :disabled="busy || form.planId === member.plan.id" @click="emit('changePlan', form.planId)">Change plan</button>
    </template>
  </Modal>

  <Modal title="Cancel membership" :open="open === 'cancel'" @close="open = null">
    <p class="text-sm text-slate-600">
      Takes effect after 30 days' notice; {{ member.firstName }} is billed as normal until then.
      <template v-if="member.billingCycle === 'Yearly'"> Yearly contracts ended early pay a {{ formatMoney(YEARLY_EARLY_EXIT_FEE) }} early-exit fee now.</template>
    </p>
    <label class="block"><span class="label">Reason</span><input v-model="form.reason" class="field" placeholder="Moving away, cost, injury…" /></label>
    <Alert v-if="error">{{ error }}</Alert>
    <template #footer>
      <button class="btn-secondary" @click="open = null">Back</button>
      <button class="btn-danger" :disabled="busy || !form.reason" @click="emit('cancel', form.reason)">Cancel membership</button>
    </template>
  </Modal>

  <Modal title="Apply a promo code" :open="open === 'promo'" @close="open = null">
    <label class="block"><span class="label">Code</span><input v-model="form.code" class="field uppercase" placeholder="NEWYEAR30" /></label>
    <Alert v-if="error">{{ error }}</Alert>
    <template #footer>
      <button class="btn-secondary" @click="open = null">Back</button>
      <button class="btn-primary" :disabled="busy || !form.code" @click="emit('applyPromo', form.code)">Apply</button>
    </template>
  </Modal>

  <Modal title="Update card on file" :open="open === 'card'" @close="open = null">
    <p class="text-sm text-slate-600">Any failed charges are retried on the new card right away.</p>
    <label class="block"><span class="label">Last 4 digits</span><input v-model="form.last4" maxlength="4" inputmode="numeric" class="field" /></label>
    <Alert v-if="error">{{ error }}</Alert>
    <template #footer>
      <button class="btn-secondary" @click="open = null">Back</button>
      <button class="btn-primary" :disabled="busy || form.last4.length !== 4" @click="emit('updateCard', form.last4)">Save card</button>
    </template>
  </Modal>

  <Modal title="Edit member" :open="open === 'edit'" @close="open = null">
    <label class="block"><span class="label">Phone</span><input v-model="form.phone" class="field" /></label>
    <label class="block">
      <span class="label">Home gym</span>
      <select v-model="form.homeLocationId" class="field"><option v-for="l in gym.locations" :key="l.id" :value="l.id">{{ l.name }}</option></select>
    </label>
    <label class="block"><span class="label">Notes</span><textarea v-model="form.notes" rows="3" class="field" /></label>
    <Alert v-if="error">{{ error }}</Alert>
    <template #footer>
      <button class="btn-secondary" @click="open = null">Back</button>
      <button class="btn-primary" :disabled="busy" @click="emit('updateDetails', form.phone, form.homeLocationId, form.notes)">Save</button>
    </template>
  </Modal>
</template>
