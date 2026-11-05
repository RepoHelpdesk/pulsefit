<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import { useGymStore } from '@/stores/gym'
import { useAuthStore } from '@/stores/auth'
import { membersService } from '@/services/members'
import type { MemberRow, MemberStatus } from '@/types'
import { formatDate, humanize } from '@/utils/format'
import PageHeader from '@/components/ui/PageHeader.vue'
import Card from '@/components/ui/Card.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import Badge from '@/components/ui/Badge.vue'
import Pager from '@/components/ui/Pager.vue'

const gym = useGymStore()
const auth = useAuthStore()
const statuses: MemberStatus[] = ['Active', 'PastDue', 'Suspended', 'Frozen', 'Cancelled']
const filters = reactive({ q: '', status: '' as MemberStatus | '', planId: null as number | null, locationId: null as number | null })
const skip = ref(0)
const take = 25
const rows = ref<MemberRow[]>([])
const total = ref(0)

let timer: ReturnType<typeof setTimeout> | undefined
async function load() {
  const page = await membersService.search({ ...filters, skip: skip.value, take })
  rows.value = page.items
  total.value = page.total
}
watch(filters, () => { skip.value = 0; clearTimeout(timer); timer = setTimeout(load, 200) })
watch(skip, load, { immediate: true })
</script>

<template>
  <PageHeader title="Members" :subtitle="`${total} members match`">
    <RouterLink v-if="auth.can('EditMembers')" to="/members/new" class="btn-primary">New member</RouterLink>
  </PageHeader>

  <Card flush>
    <div class="grid gap-3 border-b border-slate-100 p-4 sm:grid-cols-4">
      <input v-model="filters.q" class="field sm:col-span-1" placeholder="Name, email or phone" />
      <select v-model="filters.status" class="field">
        <option value="">Any status</option>
        <option v-for="s in statuses" :key="s" :value="s">{{ humanize(s) }}</option>
      </select>
      <select v-model="filters.planId" class="field">
        <option :value="null">Any plan</option>
        <option v-for="p in gym.plans" :key="p.id" :value="p.id">{{ p.name }}</option>
      </select>
      <select v-model="filters.locationId" class="field">
        <option :value="null">Any home gym</option>
        <option v-for="l in gym.locations" :key="l.id" :value="l.id">{{ l.name }}</option>
      </select>
    </div>
    <div class="overflow-x-auto">
      <table class="table-base">
        <thead><tr><th>Member</th><th>Plan</th><th>Home gym</th><th>Status</th><th>Next bill</th></tr></thead>
        <tbody>
          <tr v-for="m in rows" :key="m.id" class="cursor-pointer hover:bg-slate-50" @click="$router.push(`/members/${m.id}`)">
            <td>
              <div class="font-medium">{{ m.name }}</div>
              <div class="text-xs text-slate-500">{{ m.email }}</div>
            </td>
            <td>{{ m.plan }} <span class="text-xs text-slate-500">· {{ m.billingCycle.toLowerCase() }}</span></td>
            <td>{{ m.homeLocation }}</td>
            <td class="space-x-1">
              <StatusBadge :status="m.status" />
              <Badge v-if="m.cancelEffectiveOn && m.status !== 'Cancelled'" tone="amber">Leaving {{ formatDate(m.cancelEffectiveOn) }}</Badge>
              <Badge v-if="m.bookingBlocked" tone="red">Booking blocked</Badge>
            </td>
            <td class="tabular-nums">{{ m.status === 'Cancelled' ? '—' : formatDate(m.nextBillingDate) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
    <Pager v-model:skip="skip" :take="take" :total="total" />
  </Card>
</template>
