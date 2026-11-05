import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import { router } from './router'
import { setSignedOutHandler } from './helpers/http'
import { installRepoHelpdeskWidget } from './helpers/repoHelpdesk'
import { useAuthStore } from './stores/auth'
import { useGymStore } from './stores/gym'
import './style.css'

const app = createApp(App).use(createPinia()).use(router)

const auth = useAuthStore()
setSignedOutHandler(() => {
  auth.forget()
  if (router.currentRoute.value.name !== 'sign-in') router.push({ name: 'sign-in' })
})

const gym = useGymStore()
gym.loadConfig().then(() => installRepoHelpdeskWidget(gym.config)).catch(() => {})

app.mount('#app')
