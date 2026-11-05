import { http, query } from '@/helpers/http'
import type { AppConfig, AuditEntry, GymReport, Job, Location, Paged, Plan, PromoCode, Staff } from '@/types'

export const gymService = {
  config: () => http.get<AppConfig>('/config'),
  locations: () => http.get<Location[]>('/locations'),
  plans: () => http.get<Plan[]>('/plans'),
  setPlanPrice: (id: number, monthlyPrice: number, yearlyPrice: number) => http.put<Plan>(`/plans/${id}/price`, { monthlyPrice, yearlyPrice }),
  promoCodes: () => http.get<PromoCode[]>('/promo-codes'),
  staff: () => http.get<Staff[]>('/staff'),
  report: () => http.get<GymReport>('/reports/summary'),
  audit: (p: { memberId?: number | null; actor?: string; action?: string; skip?: number; take?: number }) =>
    http.get<Paged<AuditEntry>>(`/audit${query(p)}`),
  jobs: () => http.get<Job[]>('/jobs'),
  runJob: (name: string) => http.post<Job>(`/jobs/${name}/run`),
}
