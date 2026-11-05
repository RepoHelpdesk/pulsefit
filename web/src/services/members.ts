import { http, query } from '@/helpers/http'
import type { MemberDetail, MemberRow, MemberStatus, NewMember, Paged } from '@/types'

export interface MemberSearch {
  q?: string
  status?: MemberStatus | ''
  planId?: number | null
  locationId?: number | null
  skip?: number
  take?: number
}

export const membersService = {
  search: (s: MemberSearch) => http.get<Paged<MemberRow>>(`/members${query({ ...s })}`),
  get: (id: number) => http.get<MemberDetail>(`/members/${id}`),
  join: (m: NewMember) => http.post<MemberDetail>('/members', m),
  update: (id: number, phone: string, homeLocationId: number, notes: string) =>
    http.put<MemberDetail>(`/members/${id}`, { phone, homeLocationId, notes }),
  freeze: (id: number, months: number) => http.post<MemberDetail>(`/members/${id}/freeze`, { months }),
  unfreeze: (id: number) => http.post<MemberDetail>(`/members/${id}/unfreeze`),
  changePlan: (id: number, planId: number) => http.post<MemberDetail>(`/members/${id}/plan`, { planId }),
  cancel: (id: number, reason: string) => http.post<MemberDetail>(`/members/${id}/cancel`, { reason }),
  undoCancel: (id: number) => http.post<MemberDetail>(`/members/${id}/undo-cancel`),
  applyPromo: (id: number, code: string) => http.post<MemberDetail>(`/members/${id}/promo`, { code }),
  updateCard: (id: number, last4: string) => http.post<MemberDetail>(`/members/${id}/card`, { last4 }),
}
