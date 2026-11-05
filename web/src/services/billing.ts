import { http, query } from '@/helpers/http'
import type { Invoice, InvoiceKind, InvoiceStatus, Paged, Refund, RefundStatus } from '@/types'

export const billingService = {
  invoices: (status?: InvoiceStatus | '', kind?: InvoiceKind | '', skip = 0, take = 25) =>
    http.get<Paged<Invoice>>(`/billing/invoices${query({ status, kind, skip, take })}`),
  waive: (invoiceId: number, reason: string) => http.post<void>(`/billing/invoices/${invoiceId}/waive`, { reason }),
  refund: (invoiceId: number, amount: number, reason: string) =>
    http.post<{ id: number; status: RefundStatus }>(`/billing/invoices/${invoiceId}/refund`, { amount, reason }),
  refunds: (status?: RefundStatus | '') => http.get<Refund[]>(`/billing/refunds${query({ status })}`),
  decide: (refundId: number, approve: boolean) => http.post<void>(`/billing/refunds/${refundId}/decide`, { approve }),
}
