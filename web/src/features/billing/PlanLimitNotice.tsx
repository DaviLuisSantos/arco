import { Link } from 'react-router'
import { useTranslation } from 'react-i18next'

/** Aviso inline de limite do plano, com link para a seção Plano do Perfil. */
export function PlanLimitNotice() {
  const { t } = useTranslation('billing')
  return (
    <p role="alert" className="text-sm text-danger">
      {t('limitMessage')}{' '}
      <Link to="/profile" className="font-semibold underline">
        {t('limitLink')}
      </Link>
    </p>
  )
}
