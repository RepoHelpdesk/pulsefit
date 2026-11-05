<script setup lang="ts">
const props = defineProps<{ skip: number; take: number; total: number }>()
const emit = defineEmits<{ 'update:skip': [number] }>()
const go = (skip: number) => emit('update:skip', Math.max(0, Math.min(skip, Math.max(0, props.total - 1))))
</script>

<template>
  <div v-if="total > take" class="flex items-center justify-between px-4 py-3 text-sm text-slate-500">
    <span>{{ skip + 1 }}–{{ Math.min(skip + take, total) }} of {{ total }}</span>
    <div class="flex gap-2">
      <button class="btn-secondary px-2.5 py-1" :disabled="skip === 0" @click="go(skip - take)">Previous</button>
      <button class="btn-secondary px-2.5 py-1" :disabled="skip + take >= total" @click="go(skip + take)">Next</button>
    </div>
  </div>
</template>
