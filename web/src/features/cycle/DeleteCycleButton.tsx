import { Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { cyclesApi } from '../../api/endpoints'
import type { Cycle } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { useAppMutation } from '../../lib/useAppMutation'

/** Exclui um ciclo (ativo ou encerrado) e o XP que ele e seus objetivos deram. */
export function DeleteCycleButton({ cycle }: { cycle: Cycle }) {
  const { t } = useTranslation('cycle')
  const [open, setOpen] = useState(false)
  const remove = useAppMutation(() => cyclesApi.remove(cycle.id), {
    successMessage: t('delete.success'),
    onSuccess: () => setOpen(false),
  })

  return (
    <>
      <Button variant="ghost" size="sm" onClick={() => setOpen(true)}>
        <Trash2 className="size-4" aria-hidden />
        {t('delete.button')}
      </Button>
      <ConfirmDialog
        open={open}
        onClose={() => setOpen(false)}
        onConfirm={() => remove.mutate()}
        loading={remove.isPending}
        title={t('delete.title', { name: cycle.name })}
        description={
          <>
            {t('delete.description')}
            {cycle.status === 'Active' && ` ${t('delete.activeHint')}`}
          </>
        }
        confirmLabel={t('delete.button')}
        danger
      />
    </>
  )
}
