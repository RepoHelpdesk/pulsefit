<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import { gymService } from '@/services/gym'
import type { AuditEntry } from '@/types'
import { formatDateTime } from '@/utils/format'
import PageHeader from '@/components/ui/PageHeader.vue'
import Card from '@/components/ui/Card.vue'
import Pager from '@/components/ui/Pager.vue'

const filters = reactive({ actor: '', action: '' })
const skip = ref(0)
const take = 50
const rows = ref<AuditEntry[]>([])
const total = ref(0)

async function load() {
  const page = await gymService.audit({ ...filters, skip: skip.value, take })
  rows.value = page.items
  total.value = page.total
}
let timer: ReturnType<typeof setTimeout> | undefined
watch(filters, () => { skip.value = 0; clearTimeout(timer); timer = setTimeout(load, 250) })
watch(skip, load, { immediate: true })
</script>

<template>
  <PageHeader title="Audit log" subtitle="Everything staff and the background jobs did, newest first." />
  <Card :padded="false">
    <div class="flex flex-wrap gap-3 border-b border-slate-100 p-4">
      <input v-model="filters.actor" class="field max-w-xs" placeholder="Who (name, or system:billing-run)" />
      <input v-model="filters.action" class="field max-w-xs" placeholder="Action (billing, class, member…)" />
    </div>
    <table class="table-base">
      <thead><tr><th>When</th><th>Who</th><th>Action</th><th>Member</th><th>Details</th></tr></thead>
      <tbody>
        <tr v-for="a in rows" :key="a.id" class="align-top">
          <td class="whitespace-nowrap tabular-nums text-slate-600">{{ formatDateTime(a.at) }}</td>
          <td class="whitespace-nowrap">{{ a.actor }}</td>
          <td class="whitespace-nowrap font-mono text-xs">{{ a.action }}</td>
          <td class="whitespace-nowrap"><RouterLink v-if="a.memberId" :to="`/members/${a.memberId}`" class="hover:underline">{{ a.memberName }}</RouterLink></td>
          <td class="text-slate-600">{{ a.details }}</td>
        </tr>
      </tbody>
    </table>
    <Pager v-model:skip="skip" :take="take" :total="total" />
  </Card>
</template>
