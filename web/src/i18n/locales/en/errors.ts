import type errors from '../pt-BR/errors'

const errors_en: typeof errors = {
  invalid_password: 'Incorrect password.',
  invalid_credentials: 'Incorrect email or password.',
  email_taken: 'An account with this email already exists.',
  invalid_token: 'Invalid or expired link. Request a new one.',
  unauthorized: 'Your session has expired. Please sign in again.',
  rate_limited: 'Too many attempts. Wait a minute and try again.',
  not_found: 'Not found. It may have been removed already.',
  active_cycle_exists: 'There is already an active cycle. End it before starting another.',
  conflict: 'This action was already done.',
  validation: 'Invalid data. Check the fields.',
  internal_error: 'Server error. Please try again shortly.',
  network: 'Cannot reach the server. Check your internet connection.',
  offline: 'You are offline. Without internet, only check-ins work.',
  unavailable: 'Server unavailable right now. Try again shortly.',
  plan_limit: 'Your plan has reached this limit. Upgrade to Pro to continue.',
  slug_taken: 'This address is already taken.',
  two_factor_required: 'Enter the two-factor verification code.',
  invalid_two_factor_code: 'Invalid code. Try again.',
  stripe_disabled: 'Payments are not available on this server.',
  generic: 'Something went wrong.',
  genericRetry: 'Something went wrong. Please try again.',
}
export default errors_en
