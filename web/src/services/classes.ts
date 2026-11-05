import { http, query } from '@/helpers/http'
import type { SessionDetail, SessionRow } from '@/types'

export const classesService = {
  list: (from: Date, to: Date, locationId?: number | null, mine = false) =>
    http.get<SessionRow[]>(`/classes${query({ from: from.toISOString(), to: to.toISOString(), locationId, mine })}`),
  get: (id: number) => http.get<SessionDetail>(`/classes/${id}`),
  book: (sessionId: number, memberId: number) => http.post<SessionDetail>(`/classes/${sessionId}/bookings`, { memberId }),
  cancelSession: (sessionId: number, reason: string) => http.post<SessionDetail>(`/classes/${sessionId}/cancel`, { reason }),
  cancelBooking: (bookingId: number) => http.del<void>(`/classes/bookings/${bookingId}`),
  markAttendance: (bookingId: number, attended: boolean) => http.post<void>(`/classes/bookings/${bookingId}/attendance`, { attended }),
}
