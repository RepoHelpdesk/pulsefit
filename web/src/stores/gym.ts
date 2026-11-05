import { defineStore } from 'pinia'
import { ref } from 'vue'
import { gymService } from '@/services/gym'
import type { AppConfig, Location, Plan } from '@/types'

/** Reference data almost every screen needs: the gyms, the plans and the app's config. */
export const useGymStore = defineStore('gym', () => {
  const config = ref<AppConfig>({ gymName: 'PulseFit', widgetScriptUrl: null, widgetKey: null, dashboardUrl: null, repoUrl: null })
  const locations = ref<Location[]>([])
  const plans = ref<Plan[]>([])

  async function loadConfig() {
    config.value = await gymService.config()
  }

  async function loadReference() {
    const [l, p] = await Promise.all([gymService.locations(), gymService.plans()])
    locations.value = l
    plans.value = p
  }

  const locationName = (id: number) => locations.value.find((l) => l.id === id)?.name ?? ''

  return { config, locations, plans, loadConfig, loadReference, locationName }
})
