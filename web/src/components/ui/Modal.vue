<script setup lang="ts">
defineProps<{ title: string; open: boolean }>()
const emit = defineEmits<{ close: [] }>()
</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="fixed inset-0 z-40 flex items-center justify-center bg-slate-900/40 p-4" @click.self="emit('close')">
      <div class="w-full max-w-md rounded-xl bg-white shadow-xl" role="dialog" :aria-label="title">
        <header class="flex items-center justify-between border-b border-slate-100 px-5 py-3">
          <h3 class="text-sm font-semibold">{{ title }}</h3>
          <button class="btn-ghost -mr-2 px-2 py-1" aria-label="Close" @click="emit('close')">✕</button>
        </header>
        <div class="space-y-4 px-5 py-4"><slot /></div>
        <footer v-if="$slots.footer" class="flex justify-end gap-2 border-t border-slate-100 px-5 py-3"><slot name="footer" /></footer>
      </div>
    </div>
  </Teleport>
</template>
