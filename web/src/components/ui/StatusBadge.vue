<script setup lang="ts">
// One badge for every status in the app, so "Failed" or "Suspended" looks the same everywhere.
import { computed } from 'vue'
import Badge, { type Tone } from './Badge.vue'
import { humanize } from '@/utils/format'

const props = defineProps<{ status: string }>()

const tone = computed<Tone>(() => {
  switch (props.status) {
    case 'Active': case 'Paid': case 'Attended': case 'Approved': case 'Booked': return 'green'
    case 'PastDue': case 'Waitlisted': case 'Pending': case 'PartiallyRefunded': case 'LateCancelled': return 'amber'
    case 'Suspended': case 'Failed': case 'NoShow': case 'Rejected': case 'Denied': return 'red'
    case 'Frozen': case 'Waived': return 'blue'
    case 'Refunded': return 'violet'
    default: return 'gray'
  }
})
</script>

<template>
  <Badge :tone="tone">{{ humanize(status) }}</Badge>
</template>
