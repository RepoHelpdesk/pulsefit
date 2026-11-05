// Shapes the API sends. Names match the C# DTOs (src/PulseFit.Api/Dtos/Dtos.cs).

export type StaffRole = 'FrontDesk' | 'Trainer' | 'Manager' | 'Owner'
export type Permission =
  | 'ViewMembers' | 'EditMembers' | 'CheckIn' | 'IssueGuestPass' | 'BookClasses' | 'MarkAttendance'
  | 'ManageClasses' | 'FreezeMembership' | 'CancelMembership' | 'ChangePlan' | 'WaiveFee' | 'RequestRefund'
  | 'ApproveRefund' | 'ManagePlans' | 'ViewReports' | 'RunJobs' | 'ViewAudit'

export type PlanTier = 'Basic' | 'Plus' | 'Premium'
export type BillingCycle = 'Monthly' | 'Yearly'
export type MemberStatus = 'Active' | 'Frozen' | 'PastDue' | 'Suspended' | 'Cancelled'
export type DiscountType = 'None' | 'Student' | 'Corporate'
export type CardStatus = 'Valid' | 'Expired' | 'Declined'
export type BookingStatus = 'Booked' | 'Waitlisted' | 'Cancelled' | 'LateCancelled' | 'Attended' | 'NoShow'
export type InvoiceKind = 'Membership' | 'Proration' | 'LateCancelFee' | 'NoShowFee' | 'FreezeFee' | 'EarlyExitFee' | 'DayPass'
export type InvoiceStatus = 'Open' | 'Paid' | 'Failed' | 'Waived' | 'Refunded' | 'PartiallyRefunded'
export type RefundStatus = 'Pending' | 'Approved' | 'Rejected'

export interface Staff { id: number; name: string; email: string; role: StaffRole; location: string | null }
export interface Me { staff: Staff; permissions: Permission[] }

export interface Plan {
  id: number; tier: PlanTier; name: string; monthlyPrice: number; yearlyPrice: number; allLocations: boolean
  classesPerMonth: number | null; guestPassesPerMonth: number; active: boolean
}
export interface PromoCode { id: number; code: string; percentOff: number; months: number; validUntil: string; active: boolean }
export interface Location { id: number; name: string; address: string; opensAt: string; closesAt: string }

export interface Paged<T> { items: T[]; total: number }

export interface MemberRow {
  id: number; name: string; email: string; plan: string; billingCycle: BillingCycle; status: MemberStatus
  homeLocation: string; nextBillingDate: string; cancelEffectiveOn: string | null; bookingBlocked: boolean
}

export interface Invoice {
  id: number; memberId: number; memberName: string; kind: InvoiceKind; description: string; amount: number
  refundedAmount: number; status: InvoiceStatus; issuedAt: string; failureReason: string | null
}
export interface MemberBooking { id: number; sessionId: number; className: string; startsAt: string; location: string; status: BookingStatus; promotedAt: string | null }
export interface CheckIn { id: number; memberId: number; memberName: string; location: string; at: string; allowed: boolean; deniedReason: string | null }
export interface AuditEntry { id: number; at: string; actor: string; action: string; memberId: number | null; memberName: string | null; details: string }

export interface MemberDetail {
  id: number; firstName: string; lastName: string; email: string; phone: string; homeLocationId: number; homeLocation: string
  plan: Plan; pendingPlan: Plan | null; billingCycle: BillingCycle; status: MemberStatus; joinedOn: string
  nextBillingDate: string; contractEndsOn: string | null; discount: DiscountType; promoCode: string | null; promoMonthsLeft: number
  cardStatus: CardStatus; cardLast4: string; freezeUntil: string | null; freezeMonthsLeftThisYear: number; pastDueSince: string | null
  cancelRequestedOn: string | null; cancelEffectiveOn: string | null; bookingBlockedUntil: string | null; outstanding: number; notes: string
  classesThisMonth: number; guestPassesLeftThisMonth: number
  invoices: Invoice[]; bookings: MemberBooking[]; visits: CheckIn[]; audit: AuditEntry[]
}

export interface NewMember {
  firstName: string; lastName: string; email: string; phone: string; homeLocationId: number
  planId: number; billingCycle: BillingCycle; discount: DiscountType; cardLast4: string
}

export interface SessionRow {
  id: number; name: string; trainer: string; trainerId: number; location: string; locationId: number; room: string
  startsAt: string; durationMinutes: number; capacity: number; booked: number; waitlisted: number; cancelled: boolean
}
export interface RosterEntry { bookingId: number; memberId: number; memberName: string; plan: string; status: BookingStatus; createdAt: string; promotedAt: string | null }
export interface SessionDetail { session: SessionRow; roster: RosterEntry[] }

export interface CheckInResult { allowed: boolean; message: string; classesMarked: number }

export interface Refund {
  id: number; invoiceId: number; invoiceDescription: string; memberId: number; memberName: string; amount: number
  reason: string; status: RefundStatus; requestedBy: string; createdAt: string; decidedAt: string | null
}

export interface Job { name: string; description: string; lastRunAt: string | null; lastSummary: string | null; lastFailed: boolean }

export interface CountBy { label: string; count: number }
export interface MoneyBy { label: string; amount: number }
export interface GymReport {
  activeMembers: number; newLast30Days: number; cancelledLast30Days: number; leaving: number; churnPercent: number
  byStatus: CountBy[]; byPlan: CountBy[]; revenueByLocation: MoneyBy[]; revenueLast30Days: number
  failedOutstanding: number; classFillPercent: number; checkInsByDay: CountBy[]
}

export interface AppConfig { gymName: string; widgetScriptUrl: string | null; widgetKey: string | null; dashboardUrl: string | null; repoUrl: string | null }
