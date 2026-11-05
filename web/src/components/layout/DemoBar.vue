<script setup lang="ts">
// The strip across the top that says this is a RepoHelpdesk demo, with the way to its dashboard. The
// links come from the API's RepoHelpdesk:* settings; with none set the bar only says it's a demo.
import { useGymStore } from '@/stores/gym'

const gym = useGymStore()
</script>

<template>
  <div class="flex flex-wrap items-center justify-center gap-x-4 gap-y-1 bg-slate-900 px-4 py-2 text-center text-xs text-slate-200">
    <span>
      <strong class="font-semibold text-white">Demo gym.</strong>
      Ask the RepoHelpdesk chat (bottom right) anything about members, bills or rules — it answers from this app's code.
    </span>
    <span class="flex items-center gap-3">
      <a v-if="gym.config.dashboardUrl" :href="gym.config.dashboardUrl" target="_blank" rel="noopener" class="font-semibold text-indigo-300 hover:text-indigo-200">
        See the RepoHelpdesk dashboard →
      </a>
      <a v-if="gym.config.repoUrl" :href="gym.config.repoUrl" target="_blank" rel="noopener" class="text-slate-400 hover:text-slate-200">Source code</a>
    </span>
  </div>
</template>
