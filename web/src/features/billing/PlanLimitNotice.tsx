import { Link } from 'react-router'
import { useTranslation } from 'react-i18next'

type Feature = 'habits' | 'apiKeys' | 'profileTheme'

/** Aviso inline de limite do plano, com o benefício específico e link para a seção Plano do Perfil. */
export function PlanLimitNotice({ feature }: { feature?: Feature }) {
  const { t } = useTranslation('billing')
  return (
    <p role="alert" className="text-sm text-danger">
      {t('limitMessage')} {feature && <>{t(`limitBenefit.${feature}`)} </>}
      <Link to="/profile" className="font-semibold underline">
        {t('limitLink')}
      </Link>
    </p>
  )
}
