import { useMutation } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { habitsApi } from '../../api/endpoints'
import { useToast } from '../../components/ui/toast-context'
import { errorMessage } from '../../lib/errors'
import { useInvalidateData } from '../../lib/useInvalidateData'
import { usePlanLimitHandler } from '../billing/usePlanLimitHandler'
import { HABIT_TEMPLATES, SHORTCUT_TEMPLATE_IDS, type TemplateId } from './templates'

/**
 * Atalhos do estado vazio de Hoje: um toque cria o hábito a partir de um modelo (dose + mínimo),
 * e a tela já mostra o primeiro check-in. Limite do plano vira o aviso padrão, não um erro genérico.
 */
export function TemplateShortcuts() {
  const { t } = useTranslation('onboarding')
  const toast = useToast()
  const invalidate = useInvalidateData()
  const handlePlanLimit = usePlanLimitHandler()

  const create = useMutation({
    mutationFn: (id: TemplateId) =>
      habitsApi.create({
        name: t(`templates.${id}.name`),
        emoji: HABIT_TEMPLATES.find((x) => x.id === id)?.emoji,
        frequencyType: 'Daily',
        levelDescription: t(`templates.${id}.dose`),
        minimumDescription: t(`templates.${id}.minimum`),
      }),
    onSuccess: (_, id) => toast.success(t('shortcuts.created', { name: t(`templates.${id}.name`) })),
    onError: (err) => {
      if (!handlePlanLimit(err)) toast.error(errorMessage(err))
    },
    onSettled: () => invalidate(),
  })

  return (
    <section aria-labelledby="template-shortcuts-title" className="mt-4">
      <h3 id="template-shortcuts-title" className="text-sm font-semibold">
        {t('shortcuts.title')}
      </h3>
      <p className="mt-1 text-sm text-muted">{t('shortcuts.hint')}</p>
      <ul className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-3">
        {SHORTCUT_TEMPLATE_IDS.map((id) => {
          const template = HABIT_TEMPLATES.find((x) => x.id === id)
          if (!template) return null
          return (
            <li key={id}>
              <button
                type="button"
                disabled={create.isPending}
                onClick={() => create.mutate(id)}
                className="flex min-h-11 w-full items-center gap-2 rounded-xl border border-border bg-surface-2 px-3 py-2 text-left text-sm font-medium hover:border-accent disabled:opacity-60"
              >
                <span aria-hidden className="text-lg">
                  {template.emoji}
                </span>
                {t(`templates.${id}.name`)}
              </button>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
