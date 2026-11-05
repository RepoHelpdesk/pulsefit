<script setup lang="ts">
// Type a name, email or phone; pick a member. Used by the front desk and when booking a class.
import { ref, watch } from 'vue'
import { membersService } from '@/services/members'
import type { MemberRow } from '@/types'
import StatusBadge from '@/components/ui/StatusBadge.vue'

defineProps<{ placeholder?: string }>()
const emit = defineEmits<{ pick: [MemberRow] }>()

const term = ref('')
const results = ref<MemberRow[]>([])
let timer: ReturnType<typeof setTimeout> | undefined

watch(term, (q) => {
  clearTimeout(timer)
  if (q.trim().length < 2) { results.value = []; return }
  timer = setTimeout(async () => { results.value = (await membersService.search({ q, take: 8 })).items }, 200)
})

function pick(m: MemberRow) {
  emit('pick', m)
  term.value = ''
  results.value = []
}
</script>

<template>
  <div class="relative">
    <input v-model="term" class="field" :placeholder="placeholder ?? 'Search members by name, email or phone…'" autocomplete="off" />
    <ul v-if="results.length" class="absolute z-20 mt-1 w-full overflow-hidden rounded-md border border-slate-200 bg-white shadow-lg">
      <li v-for="m in results" :key="m.id">
        <button class="flex w-full items-center justify-between gap-3 px-3 py-2 text-left text-sm hover:bg-slate-50" @click="pick(m)">
          <span>
            <span class="font-medium">{{ m.name }}</span>
            <span class="ml-2 text-xs text-slate-500">{{ m.plan }} · {{ m.homeLocation }}</span>
          </span>
          <StatusBadge :status="m.status" />
        </button>
      </li>
    </ul>
  </div>
</template>
