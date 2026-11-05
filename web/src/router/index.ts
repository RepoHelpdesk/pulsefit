import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import AppShell from '@/components/layout/AppShell.vue'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/sign-in', name: 'sign-in', component: () => import('@/views/SignInView.vue'), meta: { public: true } },
    {
      path: '/',
      component: AppShell,
      children: [
        { path: '', name: 'today', component: () => import('@/views/TodayView.vue') },
        { path: 'front-desk', name: 'front-desk', component: () => import('@/views/FrontDeskView.vue') },
        { path: 'members', name: 'members', component: () => import('@/views/members/MembersView.vue') },
        { path: 'members/new', name: 'member-new', component: () => import('@/views/members/NewMemberView.vue') },
        { path: 'members/:id(\\d+)', name: 'member', component: () => import('@/views/members/MemberDetailView.vue'), props: (r) => ({ id: Number(r.params.id) }) },
        { path: 'classes', name: 'classes', component: () => import('@/views/classes/ScheduleView.vue') },
        { path: 'classes/:id(\\d+)', name: 'class', component: () => import('@/views/classes/SessionView.vue'), props: (r) => ({ id: Number(r.params.id) }) },
        { path: 'billing', name: 'billing', component: () => import('@/views/BillingView.vue') },
        { path: 'plans', name: 'plans', component: () => import('@/views/PlansView.vue') },
        { path: 'reports', name: 'reports', component: () => import('@/views/ReportsView.vue') },
        { path: 'staff', name: 'staff', component: () => import('@/views/StaffView.vue') },
        { path: 'audit', name: 'audit', component: () => import('@/views/AuditView.vue') },
        { path: 'jobs', name: 'jobs', component: () => import('@/views/JobsView.vue') },
      ],
    },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()
  if (!auth.loaded) await auth.load()
  if (!to.meta.public && !auth.me) return { name: 'sign-in', query: to.fullPath === '/' ? {} : { next: to.fullPath } }
})
