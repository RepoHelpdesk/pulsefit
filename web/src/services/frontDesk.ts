import { http, query } from '@/helpers/http'
import type { CheckIn, CheckInResult } from '@/types'

export const frontDeskService = {
  checkIn: (memberId: number, locationId: number) => http.post<CheckInResult>('/front-desk/check-in', { memberId, locationId }),
  guest: (memberId: number, locationId: number, guestName: string, buyDayPass: boolean) =>
    http.post<{ message: string }>('/front-desk/guest', { memberId, locationId, guestName, buyDayPass }),
  recent: (locationId?: number | null, take = 30) => http.get<CheckIn[]>(`/front-desk/recent${query({ locationId, take })}`),
}
