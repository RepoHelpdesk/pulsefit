import { http } from '@/helpers/http'
import type { Me, Staff } from '@/types'

export const authService = {
  staff: () => http.get<Staff[]>('/auth/staff'),
  signIn: (staffId: number) => http.post<Me>('/auth/sign-in', { staffId }),
  signOut: () => http.post<void>('/auth/sign-out'),
  me: () => http.get<Me>('/auth/me'),
}
