import { useState } from 'react'
import { cyclesApi } from '../../api/endpoints'
import type { Cycle } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { Textarea } from '../../components/ui/Input'
import { useAppMutation } from '../../lib/useAppMutation'

const MAX_NOTES = 4000

interface CompleteCycleDialogProps {
  cycle: Cycle
  open: boolean
  onClose: () => void
}

export function CompleteCycleDialog({ cycle, open, onClose }: CompleteCycleDialogProps) {
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)
  const complete = useAppMutation((reviewNotes: string) => cyclesApi.complete(cycle.id, reviewNotes), {
    successMessage: 'Ciclo encerrado. Hora de planejar o próximo!',
    onSuccess: () => {
      setNotes('')
      onClose()
    },
  })

  function submit() {
    if (notes.length > MAX_NOTES) {
      setError(`Use até ${MAX_NOTES} caracteres.`)
      return
    }
    setError(null)
    complete.mutate(notes.trim())
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="Encerrar ciclo"
      description={
        <>
          Feche “{cycle.name}” com uma revisão. <span className="font-semibold text-xp">+100 XP</span>
        </>
      }
      footer={
        <>
          <Button variant="secondary" block onClick={onClose}>
            Cancelar
          </Button>
          <Button block loading={complete.isPending} onClick={submit}>
            Encerrar ciclo
          </Button>
        </>
      }
    >
      <Textarea
        label="Revisão"
        value={notes}
        onChange={(e) => setNotes(e.target.value)}
        placeholder="O que funcionou? O que não funcionou? O que levar para o próximo ciclo?"
        rows={6}
        maxLength={MAX_NOTES}
        hint="Opcional, mas vale muito para o próximo ciclo."
        error={error}
        autoFocus
      />
    </Dialog>
  )
}
