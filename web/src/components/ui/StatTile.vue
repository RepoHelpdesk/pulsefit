<script setup lang="ts">
defineProps<{ label: string; value: string | number; hint?: string; tone?: 'default' | 'warn' }>()
</script>

<template>
  <div class="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
    <div class="text-xs font-medium text-slate-500">{{ label }}</div>
    <div class="mt-1 text-2xl font-semibold tabular-nums" :class="tone === 'warn' ? 'text-rose-600' : 'text-slate-900'">{{ value }}</div>
    <div v-if="hint" class="mt-0.5 text-xs text-slate-500">{{ hint }}</div>
  </div>
</template>
