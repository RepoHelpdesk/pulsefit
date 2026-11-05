import type { Permission } from '@/types'

export interface NavItem {
  to: string
  label: string
  icon: string
  /** Hidden unless the signed-in role has this. */
  needs?: Permission
}

// The sidebar, top to bottom. Add a screen here and in the router.
export const navigation: NavItem[] = [
  { to: '/', label: 'Today', icon: 'M3 12l9-9 9 9M5 10v10h14V10' },
  { to: '/front-desk', label: 'Front desk', icon: 'M4 7h16M4 12h16M4 17h10', needs: 'CheckIn' },
  { to: '/members', label: 'Members', icon: 'M16 11a4 4 0 10-8 0 4 4 0 008 0zM4 21a8 8 0 0116 0', needs: 'ViewMembers' },
  { to: '/classes', label: 'Classes', icon: 'M8 7V3m8 4V3M4 11h16M5 5h14a1 1 0 011 1v14a1 1 0 01-1 1H5a1 1 0 01-1-1V6a1 1 0 011-1z' },
  { to: '/billing', label: 'Billing', icon: 'M3 7h18v10H3zM3 11h18', needs: 'ViewMembers' },
  { to: '/plans', label: 'Plans & pricing', icon: 'M7 7h10M7 12h10M7 17h6' },
  { to: '/reports', label: 'Reports', icon: 'M5 20V10m7 10V4m7 16v-7', needs: 'ViewReports' },
  { to: '/staff', label: 'Staff', icon: 'M17 20a5 5 0 00-10 0M12 12a4 4 0 100-8 4 4 0 000 8z' },
  { to: '/audit', label: 'Audit log', icon: 'M9 5h10M9 12h10M9 19h10M5 5h.01M5 12h.01M5 19h.01', needs: 'ViewAudit' },
  { to: '/jobs', label: 'Background jobs', icon: 'M12 6v6l4 2M21 12a9 9 0 11-18 0 9 9 0 0118 0z', needs: 'RunJobs' },
]
