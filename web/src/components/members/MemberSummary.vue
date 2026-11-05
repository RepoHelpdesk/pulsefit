<script setup lang="ts">
// The membership at a glance: plan, money, and anything unusual (frozen, leaving, owing, blocked).
import type { MemberDetail } from '@/types'
import { formatDate, formatDateTime, formatMoney, humanize, relativeDays } from '@/utils/format'
import Card from '@/components/ui/Card.vue'
import Alert from '@/components/ui/Alert.vue'

const props = defineProps<{ member: MemberDetail }>()
const price = () => (props.member.billingCycle === 'Yearly' ? props.member.plan.yearlyPrice : props.member.plan.monthlyPrice)
</script>

<template>
  <div class="space-y-3">
    <Alert v-if="member.status === 'Suspended'">
      Suspended since the payment of {{ formatDate(member.pastDueSince) }} wasn't made within the grace period.
      {{ formatMoney(member.outstanding) }} is owed. Updating the card collects it and restores access.
    </Alert>
    <Alert v-else-if="member.status === 'PastDue'">
      Payment failed on {{ formatDate(member.pastDueSince) }} ({{ formatMoney(member.outstanding) }} owed). Access continues until the grace period ends.
    </Alert>
    <Alert v-if="member.status === 'Frozen'" tone="info">Frozen until {{ formatDate(member.freezeUntil) }} ({{ relativeDays(member.freezeUntil!) }}).</Alert>
    <Alert v-if="member.cancelEffectiveOn" :tone="member.status === 'Cancelled' ? 'info' : 'error'">
      <template v-if="member.status === 'Cancelled'">Cancelled on {{ formatDate(member.cancelEffectiveOn) }} (asked {{ formatDate(member.cancelRequestedOn) }}).</template>
      <template v-else>Leaving on {{ formatDate(member.cancelEffectiveOn) }} — notice given {{ formatDate(member.cancelRequestedOn) }}.</template>
    </Alert>
    <Alert v-if="member.bookingBlockedUntil">Can't book classes until {{ formatDateTime(member.bookingBlockedUntil) }} (repeat no-shows).</Alert>
    <Alert v-if="member.pendingPlan" tone="info">Moves to {{ member.pendingPlan.name }} on {{ formatDate(member.nextBillingDate) }}.</Alert>

    <Card title="Membership">
      <dl class="grid grid-cols-2 gap-x-4 gap-y-3 text-sm">
        <div><dt class="label">Plan</dt><dd class="font-medium">{{ member.plan.name }} · {{ member.billingCycle.toLowerCase() }}</dd></div>
        <div><dt class="label">Price</dt><dd>{{ formatMoney(price()) }} / {{ member.billingCycle === 'Yearly' ? 'year' : 'month' }}</dd></div>
        <div><dt class="label">Next bill</dt><dd>{{ member.status === 'Cancelled' ? '—' : formatDate(member.nextBillingDate) }}</dd></div>
        <div v-if="member.contractEndsOn"><dt class="label">Contract ends</dt><dd>{{ formatDate(member.contractEndsOn) }}</dd></div>
        <div><dt class="label">Discount</dt><dd>{{ member.discount === 'None' ? (member.promoCode ? `${member.promoCode} (${member.promoMonthsLeft} bill(s) left)` : 'None') : humanize(member.discount) }}</dd></div>
        <div><dt class="label">Card</dt><dd :class="member.cardStatus !== 'Valid' && 'text-rose-700'">•••• {{ member.cardLast4 }} <span v-if="member.cardStatus !== 'Valid'">({{ member.cardStatus.toLowerCase() }})</span></dd></div>
        <div><dt class="label">Home gym</dt><dd>{{ member.homeLocation }}{{ member.plan.allLocations ? ' (all gyms included)' : ' only' }}</dd></div>
        <div><dt class="label">Member since</dt><dd>{{ formatDate(member.joinedOn) }}</dd></div>
        <div>
          <dt class="label">Classes this month</dt>
          <dd>{{ member.plan.classesPerMonth === 0 ? 'Not included' : member.plan.classesPerMonth === null ? `${member.classesThisMonth} (unlimited)` : `${member.classesThisMonth} of ${member.plan.classesPerMonth}` }}</dd>
        </div>
        <div><dt class="label">Guest passes left</dt><dd>{{ member.plan.guestPassesPerMonth ? `${member.guestPassesLeftThisMonth} of ${member.plan.guestPassesPerMonth}` : 'Not included' }}</dd></div>
        <div><dt class="label">Freeze months left ({{ new Date().getFullYear() }})</dt><dd>{{ member.freezeMonthsLeftThisYear }}</dd></div>
        <div class="col-span-2"><dt class="label">Contact</dt><dd class="break-all">{{ member.email }}<br />{{ member.phone }}</dd></div>
      </dl>
      <p v-if="member.notes" class="mt-4 rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-900">📝 {{ member.notes }}</p>
    </Card>
  </div>
</template>
