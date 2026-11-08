import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router'
import { partnersApi } from '../api/endpoints'
import { Button } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { Spinner } from '../components/ui/States'
import { useToast } from '../components/ui/toast-context'
import { partnersKey } from '../features/partners/keys'
import { errorMessage } from '../lib/errors'
import { useAuthStatus } from '../lib/useAuthStatus'

export function AcceptInvitePage() {
  const { t } = useTranslation('partners')
  const { token = '' } = useParams()
  const navigate = useNavigate()
  const toast = useToast()
  const qc = useQueryClient()
  const auth = useAuthStatus()

  const accept = useMutation({
    mutationFn: () => partnersApi.accept(token),
    onSuccess: (res) => {
      void qc.invalidateQueries({ queryKey: partnersKey })
      toast.success(t('accept.accepted', { name: res.owner.displayName }))
      void navigate(`/partners/${res.id}`, { replace: true })
    },
  })

  if (auth.isPending) {
    return (
      <div className="grid min-h-dvh place-items-center">
        <Spinner />
      </div>
    )
  }

  return (
    <main className="grid min-h-dvh place-items-center px-4">
      <Card className="w-full max-w-sm space-y-4 text-center">
        <h1 className="text-xl font-extrabold">{t('accept.title')}</h1>
        <p className="text-sm text-muted">{t('accept.description')}</p>
        {accept.isError && (
          <p role="alert" className="text-sm text-danger">
            {errorMessage(accept.error) || t('accept.invalid')}
          </p>
        )}
        {auth.data ? (
          <Button block loading={accept.isPending} onClick={() => accept.mutate()}>
            {t('accept.action')}
          </Button>
        ) : (
          <>
            <p className="text-sm text-muted">{t('accept.needLogin')}</p>
            <Link to="/login" state={{ from: `/invite/${token}` }} className="block">
              <Button block>{t('accept.login')}</Button>
            </Link>
          </>
        )}
      </Card>
    </main>
  )
}
