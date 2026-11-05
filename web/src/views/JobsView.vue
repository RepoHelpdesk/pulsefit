<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { gymService } from '@/services/gym'
import { useAction } from '@/composables/useAsync'
import type { Job } from '@/types'
import { formatDateTime } from '@/utils/format'
import PageHeader from '@/components/ui/PageHeader.vue'
import Card from '@/components/ui/Card.vue'
import Alert from '@/components/ui/Alert.vue'

const jobs = ref<Job[]>([])
const run = useAction()
const running = ref<string | null>(null)

const load = async () => { jobs.value = await gymService.jobs() }
onMounted(load)

async function runNow(name: string) {
  running.value = name
  const job = await run.act(() => gymService.runJob(name))
  running.value = null
  if (job) jobs.value = jobs.value.map((j) => (j.name === name ? job : j))
}
</script>

<template>
  <PageHeader title="Background jobs" subtitle="They run every 15 minutes, in this order. Each only acts on what's due, so running one again is safe." />
  <Alert v-if="run.error.value" class="mb-4">{{ run.error.value }}</Alert>
  <Card :padded="false">
    <table class="table-base">
      <thead><tr><th>Job</th><th>Last run</th><th>Result</th><th /></tr></thead>
      <tbody>
        <tr v-for="j in jobs" :key="j.name">
          <td><div class="font-mono text-sm font-medium">{{ j.name }}</div><div class="text-xs text-slate-500">{{ j.description }}</div></td>
          <td class="whitespace-nowrap text-slate-600">{{ formatDateTime(j.lastRunAt) }}</td>
          <td :class="j.lastFailed ? 'text-rose-700' : 'text-slate-600'">{{ j.lastSummary ?? '—' }}</td>
          <td class="text-right"><button class="btn-secondary px-2.5 py-1 text-xs" :disabled="run.busy.value" @click="runNow(j.name)">{{ running === j.name ? 'Running…' : 'Run now' }}</button></td>
        </tr>
      </tbody>
    </table>
  </Card>
</template>
