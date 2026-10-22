import { Trash2 } from 'lucide-react'
import { useState } from 'react'
import { cyclesApi } from '../../api/endpoints'
import type { Cycle } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { useAppMutation } from '../../lib/useAppMutation'

/** Exclui um ciclo (ativo ou encerrado) e o XP que ele e seus objetivos deram. */
export function DeleteCycleButton({ cycle }: { cycle: Cycle }) {
  const [open, setOpen] = useState(false)
  const remove = useAppMutation(() => cyclesApi.remove(cycle.id), {
    successMessage: 'Ciclo excluído.',
    onSuccess: () => setOpen(false),
  })

  return (
    <>
      <Button variant="ghost" size="sm" onClick={() => setOpen(true)}>
        <Trash2 className="size-4" aria-hidden />
        Excluir ciclo
      </Button>
      <ConfirmDialog
        open={open}
        onClose={() => setOpen(false)}
        onConfirm={() => remove.mutate()}
        loading={remove.isPending}
        title={`Excluir “${cycle.name}”?`}
        description={
          <>
            O ciclo, os objetivos e os marcos dele são apagados, e o XP que deram (encerramento, objetivos e marcos)
            sai do seu total. Os hábitos continuam. Não dá para desfazer.
            {cycle.status === 'Active' && ' Para fechar um ciclo que você cumpriu, use “Encerrar ciclo”.'}
          </>
        }
        confirmLabel="Excluir ciclo"
        danger
      />
    </>
  )
}
