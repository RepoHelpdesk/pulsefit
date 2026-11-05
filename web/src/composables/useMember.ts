import { ref, watch, type Ref } from 'vue'
import { membersService } from '@/services/members'
import { billingService } from '@/services/billing'
import { classesService } from '@/services/classes'
import { useAction } from './useAsync'
import type { MemberDetail } from '@/types'

/**
 * One member and everything staff can do to them. Each action returns the member as the API now sees
 * it, so the screen always shows what was saved. A rule that says no leaves its message in `error`.
 */
export function useMember(id: Ref<number>) {
  const member = ref<MemberDetail | null>(null)
  const loadError = ref<string | null>(null)
  const { busy, error, act } = useAction()

  async function load() {
    loadError.value = null
    try {
      member.value = await membersService.get(id.value)
    } catch (e) {
      loadError.value = e instanceof Error ? e.message : String(e)
    }
  }
  watch(id, load, { immediate: true })

  /** Run a change that returns the member. */
  async function change(fn: () => Promise<MemberDetail>) {
    const updated = await act(fn)
    if (updated) member.value = updated
    return updated !== null
  }

  /** Run a change elsewhere (a bill, a booking), then reload the member. */
  async function changeThenReload(fn: () => Promise<unknown>) {
    const ok = (await act(async () => { await fn(); return true })) !== null
    if (ok) await load()
    return ok
  }

  const m = () => id.value
  return {
    member, loadError, busy, error, load,
    freeze: (months: number) => change(() => membersService.freeze(m(), months)),
    unfreeze: () => change(() => membersService.unfreeze(m())),
    changePlan: (planId: number) => change(() => membersService.changePlan(m(), planId)),
    cancel: (reason: string) => change(() => membersService.cancel(m(), reason)),
    undoCancel: () => change(() => membersService.undoCancel(m())),
    applyPromo: (code: string) => change(() => membersService.applyPromo(m(), code)),
    updateCard: (last4: string) => change(() => membersService.updateCard(m(), last4)),
    updateDetails: (phone: string, homeLocationId: number, notes: string) => change(() => membersService.update(m(), phone, homeLocationId, notes)),
    waive: (invoiceId: number, reason: string) => changeThenReload(() => billingService.waive(invoiceId, reason)),
    refund: (invoiceId: number, amount: number, reason: string) => changeThenReload(() => billingService.refund(invoiceId, amount, reason)),
    cancelBooking: (bookingId: number) => changeThenReload(() => classesService.cancelBooking(bookingId)),
    clearError: () => { error.value = null },
  }
}
