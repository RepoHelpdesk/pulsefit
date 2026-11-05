<script setup lang="ts">
// A labelled horizontal bar per row, scaled to the largest value.
import { computed } from 'vue'
import { formatMoney } from '@/utils/format'

const props = defineProps<{ items: { label: string; value: number }[]; money?: boolean }>()
const max = computed(() => Math.max(1, ...props.items.map((i) => i.value)))
</script>

<template>
  <ul class="space-y-2.5">
    <li v-for="i in items" :key="i.label" class="text-sm">
      <div class="mb-1 flex justify-between"><span class="text-slate-600">{{ i.label }}</span><span class="font-medium tabular-nums">{{ money ? formatMoney(i.value) : i.value }}</span></div>
      <div class="h-2 rounded-full bg-slate-100"><div class="h-2 rounded-full bg-brand-500" :style="{ width: `${(100 * i.value) / max}%` }" /></div>
    </li>
  </ul>
</template>
