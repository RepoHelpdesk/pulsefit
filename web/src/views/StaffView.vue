<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { gymService } from '@/services/gym'
import type { Permission, Staff, StaffRole } from '@/types'
import { humanize } from '@/utils/format'
import PageHeader from '@/components/ui/PageHeader.vue'
import Card from '@/components/ui/Card.vue'
import Badge from '@/components/ui/Badge.vue'

const staff = ref<Staff[]>([])
onMounted(async () => { staff.value = await gymService.staff() })

// What each role may do, as the API grants it (Domain/Permissions.cs). Shown so staff know who to ask.
const roles: StaffRole[] = ['FrontDesk', 'Trainer', 'Manager', 'Owner']
const matrix: { what: string; perm: Permission; roles: StaffRole[] }[] = [
  { what: 'Check members in, guest passes', perm: 'CheckIn', roles: ['FrontDesk', 'Manager', 'Owner'] },
  { what: 'Book and cancel classes for members', perm: 'BookClasses', roles: ['FrontDesk', 'Trainer', 'Manager', 'Owner'] },
  { what: 'Take attendance', perm: 'MarkAttendance', roles: ['Trainer', 'Manager', 'Owner'] },
  { what: 'Waive a fee', perm: 'WaiveFee', roles: ['FrontDesk', 'Manager', 'Owner'] },
  { what: 'Freeze, cancel, change plan', perm: 'ChangePlan', roles: ['Manager', 'Owner'] },
  { what: 'Refund (up to $100 for managers)', perm: 'RequestRefund', roles: ['Manager', 'Owner'] },
  { what: 'Approve big refunds', perm: 'ApproveRefund', roles: ['Owner'] },
  { what: 'Set prices, run background jobs', perm: 'ManagePlans', roles: ['Owner'] },
]
</script>

<template>
  <PageHeader title="Staff" />
  <div class="grid gap-6 lg:grid-cols-2">
    <Card :padded="false">
      <table class="table-base">
        <thead><tr><th>Name</th><th>Role</th><th>Gym</th></tr></thead>
        <tbody>
          <tr v-for="s in staff" :key="s.id">
            <td><div class="font-medium">{{ s.name }}</div><div class="text-xs text-slate-500">{{ s.email }}</div></td>
            <td><Badge :tone="s.role === 'Owner' ? 'violet' : s.role === 'Manager' ? 'blue' : 'gray'">{{ humanize(s.role) }}</Badge></td>
            <td>{{ s.location ?? 'All gyms' }}</td>
          </tr>
        </tbody>
      </table>
    </Card>
    <Card title="Who can do what" :padded="false">
      <table class="table-base">
        <thead><tr><th /><th v-for="r in roles" :key="r" class="text-center">{{ humanize(r) }}</th></tr></thead>
        <tbody>
          <tr v-for="row in matrix" :key="row.what">
            <td>{{ row.what }}</td>
            <td v-for="r in roles" :key="r" class="text-center">{{ row.roles.includes(r) ? '✓' : '' }}</td>
          </tr>
        </tbody>
      </table>
    </Card>
  </div>
</template>
