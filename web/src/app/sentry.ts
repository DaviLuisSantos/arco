import * as Sentry from '@sentry/react'

const dsn = import.meta.env.VITE_SENTRY_DSN as string | undefined

// Ativo só com DSN. Sem PII: nada de IP, cookies, e-mail ou corpo de requisição.
if (dsn) {
  Sentry.init({
    dsn,
    environment: import.meta.env.MODE,
    tracesSampleRate: 0,
    beforeSend(event) {
      if (event.user) delete event.user
      if (event.request) {
        delete event.request.cookies
        delete event.request.data
        delete event.request.headers
      }
      return event
    },
  })
}
