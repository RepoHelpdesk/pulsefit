<script setup lang="ts">
defineProps<{ tone?: 'error' | 'success' | 'info' }>()
</script>

<template>
  <div
    class="rounded-md px-3 py-2 text-sm"
    :class="{
      'bg-rose-50 text-rose-800': (tone ?? 'error') === 'error',
      'bg-emerald-50 text-emerald-800': tone === 'success',
      'bg-sky-50 text-sky-800': tone === 'info',
    }"
  >
    <slot />
  </div>
</template>
