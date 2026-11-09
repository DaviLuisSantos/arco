import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Copy, ExternalLink, Globe } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { billingApi, profileApi } from '../../api/endpoints'
import type { Me } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { Input, Select } from '../../components/ui/Input'
import { useToast } from '../../components/ui/toast-context'
import { errorMessage } from '../../lib/errors'
import { queryKeys } from '../../lib/queryKeys'
import { useAuthStatus } from '../../lib/useAuthStatus'
import { isPlanLimit } from '../billing/isPlanLimit'
import { PlanLimitNotice } from '../billing/PlanLimitNotice'

const THEMES = ['indigo', 'emerald', 'amber', 'rose', 'sky'] as const
const SLUG = /^[a-z0-9-]{3,30}$/

/** Liga o perfil público, escolhe o endereço e o que aparece nele. */
export function PublicProfileCard() {
  const me = useAuthStatus().data
  if (!me) return null
  return <Form key={me.id} me={me} />
}

function Form({ me }: { me: Me }) {
  const { t } = useTranslation('publicProfile')
  const toast = useToast()
  const qc = useQueryClient()
  const p = me.publicProfile
  const [enabled, setEnabled] = useState(p.enabled)
  const [slug, setSlug] = useState(p.slug ?? '')
  const [showHabits, setShowHabits] = useState(p.showHabits)
  const [theme, setTheme] = useState(p.theme)
  const [limit, setLimit] = useState(false)
  const billing = useQuery({ queryKey: ['billing'], queryFn: billingApi.get })
  const themeAllowed = billing.data?.limits.profileTheme ?? me.plan === 'pro'

  const slugInvalid = enabled && !SLUG.test(slug)
  const url = `${window.location.origin}/u/${slug}`

  const save = useMutation({
    mutationFn: () => profileApi.updatePublic({ enabled, slug, showHabits, theme }),
    onSuccess: () => {
      setLimit(false)
      qc.setQueryData<Me | null>(queryKeys.auth, (old) =>
        old ? { ...old, publicProfile: { enabled, slug, showHabits, theme } } : old,
      )
      toast.success(t('saved'))
    },
    onError: (e) => {
      if (isPlanLimit(e)) setLimit(true)
      else toast.error(errorMessage(e))
    },
  })

  async function copy() {
    try {
      await navigator.clipboard.writeText(url)
      toast.success(t('copied'))
    } catch {
      toast.error(url)
    }
  }

  const live = p.enabled && !!p.slug

  return (
    <Card className="flex flex-col gap-3">
      <div className="flex items-start gap-3">
        <Globe className="mt-0.5 size-5 shrink-0 text-accent" aria-hidden />
        <div className="min-w-0">
          <p className="font-semibold">{t('title')}</p>
          <p className="text-sm text-muted">{t('description')}</p>
        </div>
      </div>
      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} className="size-5" />
        {t('enable')}
      </label>
      {enabled && (
        <>
          <Input
            label={t('slug')}
            value={slug}
            maxLength={30}
            autoCapitalize="none"
            autoCorrect="off"
            onChange={(e) => setSlug(e.target.value.toLowerCase())}
            hint={slug ? t('preview', { url }) : t('slugHint')}
            error={slug && slugInvalid ? t('slugInvalid') : null}
          />
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={showHabits}
              onChange={(e) => setShowHabits(e.target.checked)}
              className="size-5"
            />
            {t('showHabits')}
          </label>
          <Select
            label={t('theme')}
            value={theme}
            onChange={(e) => setTheme(e.target.value)}
            hint={themeAllowed ? undefined : t('themeProOnly')}
          >
            {THEMES.map((th) => (
              <option key={th} value={th} disabled={!themeAllowed && th !== 'indigo'}>
                {t(`themes.${th}`)}
              </option>
            ))}
          </Select>
        </>
      )}
      {limit && <PlanLimitNotice feature="profileTheme" />}
      <Button loading={save.isPending} disabled={slugInvalid} onClick={() => save.mutate()}>
        {t('save')}
      </Button>
      {live && (
        <div className="flex gap-2">
          <Button variant="secondary" size="sm" className="flex-1" onClick={() => void copy()}>
            <Copy className="size-4" aria-hidden />
            {t('copy')}
          </Button>
          <a
            href={`/u/${p.slug}`}
            target="_blank"
            rel="noreferrer"
            className="inline-flex h-9 flex-1 items-center justify-center gap-1.5 rounded-xl border border-border bg-surface-2 px-3 text-sm font-semibold hover:border-accent"
          >
            <ExternalLink className="size-4" aria-hidden />
            {t('open')}
          </a>
        </div>
      )}
    </Card>
  )
}
