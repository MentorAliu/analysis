import { useEffect, useState, type FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Field,
  FieldDescription,
  FieldError,
  FieldGroup,
  FieldLabel,
  FieldLegend,
  FieldSet,
} from '@/components/ui/field'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import type { RankingsProblem } from '@/lib/api/generated/types.gen'
import { rankingsSearchSchema, searchErrors, type RankingsSelection, type SelectionErrors } from '../search'

type Props = {
  selection: RankingsSelection
  onSubmit: (selection: RankingsSelection) => void
  initialErrors?: SelectionErrors
  problem?: RankingsProblem
  busy?: boolean
}
const focusField = (errors: SelectionErrors) =>
  document
    .getElementById(
      errors.modelId ? 'rankings-model-id' : errors.asOfUtc ? 'rankings-hour' : 'rankings-model-id',
    )
    ?.focus()

export function RankingsSelectionForm({
  selection,
  onSubmit,
  initialErrors = {},
  problem,
  busy = false,
}: Props) {
  const [modelId, setModelId] = useState(selection.modelId)
  const [mode, setMode] = useState(selection.asOfUtc ? 'exact' : 'latest')
  const [hour, setHour] = useState(selection.asOfUtc ?? '')
  const [errors, setErrors] = useState<SelectionErrors>(initialErrors)
  const serverErrors: SelectionErrors =
    problem?.status === 400
      ? {
          ...(problem.errors?.modelId ? { modelId: problem.errors.modelId.join(' ') } : {}),
          ...(problem.errors?.asOfUtc ? { asOfUtc: problem.errors.asOfUtc.join(' ') } : {}),
        }
      : {}
  const shown = { ...serverErrors, ...errors }
  useEffect(() => {
    if (problem?.status === 400 && problem.errors)
      focusField(problem.errors.modelId ? { modelId: 'invalid' } : { asOfUtc: 'invalid' })
  }, [problem])

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const parsed = rankingsSearchSchema.safeParse({ modelId, ...(mode === 'exact' ? { asOfUtc: hour } : {}) })
    if (!parsed.success) {
      const next = searchErrors(parsed.error)
      setErrors(next)
      requestAnimationFrame(() => focusField(next))
      return
    }
    setErrors({})
    onSubmit({
      modelId: parsed.data.modelId,
      ...(parsed.data.asOfUtc ? { asOfUtc: parsed.data.asOfUtc } : {}),
    })
  }
  return (
    <form
      noValidate
      onSubmit={submit}
      aria-labelledby="selection-heading"
      className="ranking-panel flex flex-col gap-6"
    >
      <h2 id="selection-heading" className="ranking-heading">
        Selection
      </h2>
      {Object.keys(errors).length > 0 && (
        <Alert variant="destructive">
          <AlertTitle>Check the selection</AlertTitle>
          <AlertDescription>
            {errors.form ?? 'Correct the marked fields before loading rankings.'}
          </AlertDescription>
        </Alert>
      )}
      <FieldGroup className="gap-6">
        <Field data-invalid={!!shown.modelId}>
          <FieldLabel htmlFor="rankings-model-id">Model ID</FieldLabel>
          <Input
            id="rankings-model-id"
            name="modelId"
            value={modelId}
            onChange={(event) => {
              setModelId(event.target.value)
              setErrors({})
            }}
            autoCapitalize="none"
            autoCorrect="off"
            spellCheck={false}
            aria-invalid={!!shown.modelId}
            aria-describedby="model-help model-error"
            className="max-w-md"
          />
          <FieldDescription id="model-help">
            1–64 lowercase letters, digits, dots, underscores or hyphens; start with a letter or digit. Exact
            ID, default slice1-v1.
          </FieldDescription>
          <FieldError role="none" id="model-error">
            {shown.modelId}
          </FieldError>
        </Field>
        <FieldSet>
          <FieldLegend variant="label">Stored snapshot</FieldLegend>
          <RadioGroup
            value={mode}
            onValueChange={setMode}
            className="flex flex-wrap gap-x-6 gap-y-2"
            aria-label="Stored snapshot"
          >
            <Field orientation="horizontal" className="min-h-11 w-auto items-center gap-3">
              <RadioGroupItem value="latest" id="selection-latest" />
              <FieldLabel className="min-h-11 cursor-pointer items-center" htmlFor="selection-latest">
                Latest stored
              </FieldLabel>
            </Field>
            <Field orientation="horizontal" className="min-h-11 w-auto items-center gap-3">
              <RadioGroupItem value="exact" id="selection-exact" />
              <FieldLabel className="min-h-11 cursor-pointer items-center" htmlFor="selection-exact">
                Exact UTC hour
              </FieldLabel>
            </Field>
          </RadioGroup>
        </FieldSet>
        {mode === 'exact' && (
          <Field data-invalid={!!shown.asOfUtc}>
            <FieldLabel htmlFor="rankings-hour">Exact UTC hour</FieldLabel>
            <Input
              id="rankings-hour"
              name="asOfUtc"
              value={hour}
              onChange={(event) => {
                setHour(event.target.value)
                setErrors({})
              }}
              placeholder="YYYY-MM-DDTHH:00:00Z"
              autoCapitalize="none"
              spellCheck={false}
              aria-invalid={!!shown.asOfUtc}
              aria-describedby="hour-help hour-error"
              className="max-w-md"
            />
            <FieldDescription id="hour-help">
              Enter a real UTC hour, no later than now. A stored batch must match exactly.
            </FieldDescription>
            <FieldError role="none" id="hour-error">
              {shown.asOfUtc}
            </FieldError>
          </Field>
        )}
      </FieldGroup>
      <div className="flex flex-wrap items-center gap-4">
        <Button id="rankings-load" type="submit" size="touch">
          Load rankings
        </Button>
        <p className="text-sm leading-5 text-muted-foreground">
          {busy
            ? 'A request is in progress. You can load a different selection.'
            : 'Changes apply when you load. Results update only when requested.'}
        </p>
      </div>
    </form>
  )
}
