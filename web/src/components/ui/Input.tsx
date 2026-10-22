import { useId, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react'
import { cn } from '../../lib/cn'

const FIELD =
  'w-full rounded-xl border border-border bg-surface-2 px-3 text-base text-text placeholder:text-muted/70 ' +
  'focus:border-accent focus:outline-none disabled:opacity-60'

interface FieldProps {
  label?: ReactNode
  hint?: ReactNode
  error?: string | null
}

function FieldShell({
  id,
  label,
  hint,
  error,
  children,
}: FieldProps & { id: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      {label && (
        <label htmlFor={id} className="text-sm font-medium text-text">
          {label}
        </label>
      )}
      {children}
      {error ? (
        <p id={`${id}-msg`} className="text-sm text-danger">
          {error}
        </p>
      ) : (
        hint && (
          <p id={`${id}-msg`} className="text-xs text-muted">
            {hint}
          </p>
        )
      )}
    </div>
  )
}

export function Input({ label, hint, error, className, id, ...rest }: FieldProps & InputHTMLAttributes<HTMLInputElement>) {
  const autoId = useId()
  const fieldId = id ?? autoId
  return (
    <FieldShell id={fieldId} label={label} hint={hint} error={error}>
      <input
        id={fieldId}
        aria-invalid={error ? true : undefined}
        aria-describedby={error || hint ? `${fieldId}-msg` : undefined}
        className={cn(FIELD, 'h-11', className)}
        {...rest}
      />
    </FieldShell>
  )
}

export function Textarea({
  label,
  hint,
  error,
  className,
  id,
  ...rest
}: FieldProps & TextareaHTMLAttributes<HTMLTextAreaElement>) {
  const autoId = useId()
  const fieldId = id ?? autoId
  return (
    <FieldShell id={fieldId} label={label} hint={hint} error={error}>
      <textarea
        id={fieldId}
        aria-invalid={error ? true : undefined}
        aria-describedby={error || hint ? `${fieldId}-msg` : undefined}
        className={cn(FIELD, 'min-h-28 py-2.5', className)}
        {...rest}
      />
    </FieldShell>
  )
}

export function Select({
  label,
  hint,
  error,
  className,
  id,
  children,
  ...rest
}: FieldProps & SelectHTMLAttributes<HTMLSelectElement>) {
  const autoId = useId()
  const fieldId = id ?? autoId
  return (
    <FieldShell id={fieldId} label={label} hint={hint} error={error}>
      <select
        id={fieldId}
        aria-invalid={error ? true : undefined}
        className={cn(FIELD, 'h-11', className)}
        {...rest}
      >
        {children}
      </select>
    </FieldShell>
  )
}
