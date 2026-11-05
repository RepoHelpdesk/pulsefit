<script setup lang="ts">
// flush: no inner padding, for tables and lists that run edge to edge.
defineProps<{ title?: string; flush?: boolean }>()
</script>

<template>
  <section class="rounded-xl border border-slate-200 bg-white shadow-xs">
    <header v-if="title || $slots.actions" class="flex items-center justify-between gap-3 border-b border-slate-100 px-4 py-3">
      <h2 class="text-sm font-semibold text-slate-800">{{ title }}</h2>
      <div class="flex items-center gap-2"><slot name="actions" /></div>
    </header>
    <div :class="flush ? '' : 'p-4'"><slot /></div>
  </section>
</template>
