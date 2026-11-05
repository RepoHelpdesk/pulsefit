// The few house rules the screens mention in their help text. The API is the authority
// (src/PulseFit.Api/Domain/GymRules.cs); these only explain it.
export const FREEZE_FEE_PER_MONTH = 5
export const YEARLY_EARLY_EXIT_FEE = 75
export const MANAGER_REFUND_LIMIT = 100
export const WAIVABLE_KINDS = ['LateCancelFee', 'NoShowFee', 'FreezeFee', 'EarlyExitFee'] as const
