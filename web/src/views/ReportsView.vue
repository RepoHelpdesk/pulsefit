<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { gymService } from '@/services/gym'
import { useAsync } from '@/composables/useAsync'
import { formatMoney, humanize } from '@/utils/format'
import PageHeader from '@/components/ui/PageHeader.vue'
import Card from '@/components/ui/Card.vue'
import StatTile from '@/components/ui/StatTile.vue'
import BarList from '@/components/ui/BarList.vue'

const report = useAsync(gymService.report)
onMounted(report.run)
const r = computed(() => report.data.value)
</script>

<template>
  <PageHeader title="Reports" subtitle="Last 30 days unless it says otherwise." />
  <template v-if="r">
    <div class="mb-6 grid grid-cols-2 gap-4 lg:grid-cols-4">
      <StatTile label="Active members" :value="r.activeMembers" />
      <StatTile label="Joined" :value="r.newLast30Days" />
      <StatTile label="Cancelled" :value="r.cancelledLast30Days" :hint="`${r.churnPercent}% churn · ${r.leaving} more leaving`" />
      <StatTile label="Revenue" :value="formatMoney(r.revenueLast30Days)" :hint="`${formatMoney(r.failedOutstanding)} in failed payments`" />
    </div>
    <div class="grid gap-6 md:grid-cols-2">
      <Card title="Members by plan"><BarList :items="r.byPlan.map((x) => ({ label: x.label, value: x.count }))" /></Card>
      <Card title="Members by status"><BarList :items="r.byStatus.map((x) => ({ label: humanize(x.label), value: x.count }))" /></Card>
      <Card title="Revenue by home gym"><BarList :items="r.revenueByLocation.map((x) => ({ label: x.label, value: x.amount }))" money /></Card>
      <Card :title="`Check-ins, last 7 days · classes ${r.classFillPercent}% full`"><BarList :items="r.checkInsByDay.map((x) => ({ label: x.label, value: x.count }))" /></Card>
    </div>
  </template>
</template>
