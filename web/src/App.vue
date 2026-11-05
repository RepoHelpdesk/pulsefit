<script setup lang="ts">
import { watch } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useGymStore } from '@/stores/gym'

const auth = useAuthStore()
const gym = useGymStore()

// Locations and plans are behind sign-in; fetch them once someone is signed in.
watch(() => auth.me, (me) => { if (me && !gym.locations.length) gym.loadReference() }, { immediate: true })
</script>

<template>
  <RouterView />
</template>
