import type ns from '../pt-BR/auth'

const en: typeof ns = {
  email: 'Email',
  password: 'Password',
  minPassword: 'At least 8 characters.',
  passwordTooShort: 'The password must have at least 8 characters.',
  forgot: {
    title: 'Forgot password',
    typeEmail: 'Enter your email.',
    sent: 'If the email is registered and verified, we sent you a link to reset your password. It is valid for 1 hour.',
    submit: 'Send link',
    back: 'Back to login',
  },
  register: {
    title: 'Create account',
    subtitle: 'Start your first cycle in Arco.',
    name: 'Name (optional)',
    invalidEmail: 'Enter a valid email.',
    submit: 'Create account',
    haveAccount: 'Already have an account?',
    login: 'Sign in',
  },
  reset: {
    title: 'New password',
    invalidLink: 'Invalid link. Request a new one from "Forgot password".',
    done: 'Password changed. You can sign in now.',
    newPassword: 'New password',
    submit: 'Reset password',
    toLogin: 'Go to login',
  },
  verify: {
    title: 'Email confirmation',
    invalidLink: 'Invalid link.',
    ok: 'Email confirmed. All set!',
    continue: 'Continue to Arco',
  },
}
export default en
