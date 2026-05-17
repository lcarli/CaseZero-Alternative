import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import styled, { keyframes } from 'styled-components'
import { ArrowLeft, Star as Sparkles, AlertCircle, CheckCircle as CheckCircle2, Loader as Loader2 } from 'react-feather'
import { caseGenerationApi, type GenerateCaseRequest, type GenerateCaseStatus } from '../services/api'

const PHASES: { id: string; label: string }[] = [
  { id: 'plotOutline',                  label: 'Plot outline' },
  { id: 'suspectCards',                 label: 'Suspect cards' },
  { id: 'assetPlan',                    label: 'Asset plan' },
  { id: 'assetsAndTimelineAndBriefing', label: 'Assets + timeline + briefing' },
  { id: 'forensicsPlan',                label: 'Forensics plan' },
  { id: 'outcomesAndInitialEmails',     label: 'Forensic outcomes + initial emails' },
  { id: 'mechanicalRules',              label: 'Mechanical rules (deterministic)' },
  { id: 'rulesAndSolutionSkeleton',     label: 'Rules + solution skeleton' },
  { id: 'questionsAndExplanation',      label: 'Questions + explanation' },
  { id: 'consistency',                  label: 'Consistency check (deterministic)' },
  { id: 'autoFixSchema',                label: 'Auto-fix schema (deterministic, conditional)' },
  { id: 'redTeamAndSolver',             label: 'Red-team + solver' },
  { id: 'refineCase',                   label: 'Refine (LLM, conditional)' },
  { id: 'renderAssets',                 label: 'Render PDFs + images' },
  { id: 'publishToBlob',                label: 'Publish to blob storage' }
]

const Page = styled.div`
  min-height: 100vh;
  background: linear-gradient(140deg, #020617 0%, #0f172a 60%, #111827 100%);
  color: #e2e8f0;
  padding: 2rem clamp(1rem, 4vw, 4rem);
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
`

const TopBar = styled.div`
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
`

const BackButton = styled.button`
  background: transparent;
  border: 1px solid rgba(148, 163, 184, 0.3);
  color: #cbd5e1;
  padding: 0.55rem 1rem;
  border-radius: 999px;
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.85rem;
  cursor: pointer;
  transition: all 0.2s ease;
  &:hover { background: rgba(30, 41, 59, 0.7); color: #fff; }
`

const Title = styled.h1`
  font-size: clamp(1.4rem, 2.4vw, 2rem);
  margin: 0;
  display: flex;
  align-items: center;
  gap: 0.6rem;
`

const Card = styled.section`
  background: rgba(15, 23, 42, 0.7);
  border: 1px solid rgba(148, 163, 184, 0.18);
  border-radius: 18px;
  padding: 1.75rem;
`

const Form = styled.form`
  display: grid;
  gap: 1rem;
`

const Row = styled.div`
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
  @media (max-width: 720px) { grid-template-columns: 1fr; }
`

const Label = styled.label`
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  font-size: 0.85rem;
  color: #94a3b8;
`

const Input = styled.input`
  background: rgba(2, 6, 23, 0.85);
  border: 1px solid rgba(148, 163, 184, 0.25);
  color: #f1f5f9;
  padding: 0.6rem 0.8rem;
  border-radius: 10px;
  font-size: 0.95rem;
  &:focus { outline: 2px solid #38bdf8; outline-offset: 0; border-color: transparent; }
`

const Select = styled.select`
  background: rgba(2, 6, 23, 0.85);
  border: 1px solid rgba(148, 163, 184, 0.25);
  color: #f1f5f9;
  padding: 0.6rem 0.8rem;
  border-radius: 10px;
  font-size: 0.95rem;
`

const Button = styled.button<{ $variant?: 'primary' | 'ghost' }>`
  align-self: start;
  background: ${p => p.$variant === 'ghost' ? 'transparent' : 'linear-gradient(135deg, #6366f1, #2563eb)'};
  border: ${p => p.$variant === 'ghost' ? '1px solid rgba(148, 163, 184, 0.3)' : 'none'};
  color: #fff;
  padding: 0.7rem 1.4rem;
  border-radius: 999px;
  font-size: 0.95rem;
  font-weight: 600;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  transition: transform 0.2s ease, opacity 0.2s ease;
  &:hover:not(:disabled) { transform: translateY(-1px); }
  &:disabled { opacity: 0.5; cursor: not-allowed; }
`

const StatusBadge = styled.span<{ $status: string }>`
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.35rem 0.85rem;
  border-radius: 999px;
  font-size: 0.78rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  background: ${p =>
    p.$status === 'done'    ? 'rgba(34, 197, 94, 0.18)' :
    p.$status === 'failed'  ? 'rgba(239, 68, 68, 0.18)' :
    p.$status === 'running' ? 'rgba(56, 189, 248, 0.18)' :
                              'rgba(148, 163, 184, 0.18)'};
  color: ${p =>
    p.$status === 'done'    ? '#86efac' :
    p.$status === 'failed'  ? '#fca5a5' :
    p.$status === 'running' ? '#7dd3fc' :
                              '#cbd5e1'};
`

const ProgressBar = styled.div`
  width: 100%;
  height: 10px;
  background: rgba(15, 23, 42, 0.8);
  border-radius: 999px;
  overflow: hidden;
  margin-top: 0.6rem;
`
const ProgressFill = styled.div<{ $pct: number }>`
  height: 100%;
  width: ${p => Math.min(100, Math.max(0, p.$pct))}%;
  background: linear-gradient(90deg, #38bdf8, #6366f1);
  transition: width 0.4s ease;
`

const PhaseList = styled.ul`
  list-style: none;
  margin: 1rem 0 0;
  padding: 0;
  display: grid;
  gap: 0.4rem;
  font-size: 0.85rem;
`
const spin = keyframes`from { transform: rotate(0deg); } to { transform: rotate(360deg); }`
const Spin = styled(Loader2)`
  animation: ${spin} 1.4s linear infinite;
`
const PhaseRow = styled.li<{ $state: 'done' | 'current' | 'pending' }>`
  display: grid;
  grid-template-columns: 24px 1fr auto;
  align-items: center;
  gap: 0.6rem;
  padding: 0.45rem 0.7rem;
  border-radius: 10px;
  background: ${p =>
    p.$state === 'current' ? 'rgba(56, 189, 248, 0.12)' :
    p.$state === 'done'    ? 'rgba(15, 23, 42, 0.5)' :
                             'transparent'};
  color: ${p => p.$state === 'pending' ? '#475569' : '#cbd5e1'};
`
const Mono = styled.code`
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.78rem;
  color: #94a3b8;
`

const SummaryGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 0.75rem;
  margin-top: 1.2rem;
`
const Stat = styled.div`
  background: rgba(2, 6, 23, 0.6);
  border: 1px solid rgba(148, 163, 184, 0.15);
  border-radius: 12px;
  padding: 0.85rem 1rem;
`
const StatLbl = styled.div`
  font-size: 0.72rem;
  color: #64748b;
  text-transform: uppercase;
  letter-spacing: 0.06em;
`
const StatVal = styled.div`
  font-size: 1.15rem;
  font-weight: 600;
  color: #f1f5f9;
  margin-top: 0.2rem;
`

const ErrorBox = styled.div`
  background: rgba(239, 68, 68, 0.1);
  border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 10px;
  padding: 0.85rem 1rem;
  font-size: 0.85rem;
  color: #fca5a5;
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
`

const DIFFICULTIES = ['Rookie', 'Detective', 'Detective2', 'Sergeant', 'Lieutenant', 'Captain', 'Commander']

const POLL_INTERVAL_MS = 2500

const CaseGenerationPage = () => {
  const navigate = useNavigate()
  const [form, setForm] = useState<GenerateCaseRequest>({
    difficulty: 'Rookie',
    requiredRank: 'Rookie',
    language: 'en-US',
    writeToDisk: true
  })
  const [jobId, setJobId] = useState<string | null>(null)
  const [status, setStatus] = useState<GenerateCaseStatus | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const pollTimer = useRef<number | null>(null)

  // Cleanup on unmount
  useEffect(() => () => {
    if (pollTimer.current !== null) window.clearTimeout(pollTimer.current)
  }, [])

  const stop = () => {
    if (pollTimer.current !== null) {
      window.clearTimeout(pollTimer.current)
      pollTimer.current = null
    }
  }

  const pollOnce = async (id: string) => {
    try {
      const next = await caseGenerationApi.getJob(id)
      setStatus(next)
      if (next.status === 'done' || next.status === 'failed') {
        stop()
        return
      }
      pollTimer.current = window.setTimeout(() => pollOnce(id), POLL_INTERVAL_MS)
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Polling failed'
      setError(msg)
      // Backoff once on a transient failure, then keep trying
      pollTimer.current = window.setTimeout(() => pollOnce(id), POLL_INTERVAL_MS * 2)
    }
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setStatus(null)
    setJobId(null)
    setSubmitting(true)
    try {
      const res = await caseGenerationApi.start({
        ...form,
        seed: form.seed === undefined || form.seed === null || (form.seed as unknown as string) === ''
          ? undefined
          : Number(form.seed)
      })
      setJobId(res.jobId)
      pollOnce(res.jobId)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to start generation')
    } finally {
      setSubmitting(false)
    }
  }

  const currentPhaseIndex = (() => {
    if (!status?.currentPhase) return -1
    return PHASES.findIndex(p => p.id === status.currentPhase)
  })()
  const completedCount = status?.status === 'done'
    ? PHASES.length
    : Math.max(0, currentPhaseIndex)
  const pct = (completedCount / PHASES.length) * 100

  const update = <K extends keyof GenerateCaseRequest>(k: K, v: GenerateCaseRequest[K]) =>
    setForm(prev => ({ ...prev, [k]: v }))

  return (
    <Page>
      <TopBar>
        <Title><Sparkles size={22} />Generate New Case</Title>
        <BackButton onClick={() => navigate('/dashboard')}><ArrowLeft size={16} />Dashboard</BackButton>
      </TopBar>

      {!jobId && (
        <Card>
          <Form onSubmit={handleSubmit}>
            <Row>
              <Label>
                Difficulty *
                <Select value={form.difficulty} onChange={e => {
                  update('difficulty', e.target.value)
                  update('requiredRank', e.target.value)
                }}>
                  {DIFFICULTIES.map(d => <option key={d} value={d}>{d}</option>)}
                </Select>
              </Label>
              <Label>
                Language
                <Select value={form.language ?? 'en-US'} onChange={e => update('language', e.target.value)}>
                  <option value="en-US">English (en-US)</option>
                  <option value="pt-BR">Português (pt-BR)</option>
                  <option value="es-ES">Español (es-ES)</option>
                  <option value="fr-FR">Français (fr-FR)</option>
                </Select>
              </Label>
            </Row>
            <Row>
              <Label>
                Title (optional)
                <Input value={form.title ?? ''} onChange={e => update('title', e.target.value)} placeholder="e.g. The River Drop" />
              </Label>
              <Label>
                Location (optional)
                <Input value={form.location ?? ''} onChange={e => update('location', e.target.value)} placeholder="e.g. Seattle, Washington" />
              </Label>
            </Row>
            <Label>
              Theme (optional)
              <Input value={form.theme ?? ''} onChange={e => update('theme', e.target.value)} placeholder="e.g. Body found at a dockyard under the morning fog" />
            </Label>
            <Row>
              <Label>
                Case ID (optional)
                <Input value={form.caseId ?? ''} onChange={e => update('caseId', e.target.value)} placeholder="auto" />
              </Label>
              <Label>
                Seed (optional)
                <Input type="number" value={form.seed ?? ''} onChange={e => update('seed', e.target.value === '' ? undefined : Number(e.target.value))} placeholder="random" />
              </Label>
            </Row>
            <Button type="submit" disabled={submitting}>
              {submitting ? <Spin size={16} /> : <Sparkles size={16} />}
              {submitting ? 'Starting…' : 'Generate'}
            </Button>
            {error && <ErrorBox><AlertCircle size={16} />{error}</ErrorBox>}
          </Form>
        </Card>
      )}

      {jobId && (
        <Card>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.75rem' }}>
            <div>
              <div style={{ fontSize: '0.78rem', color: '#64748b', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Job</div>
              <Mono>{jobId}</Mono>
            </div>
            <StatusBadge $status={status?.status ?? 'queued'}>
              {status?.status === 'done' && <CheckCircle2 size={14} />}
              {status?.status === 'failed' && <AlertCircle size={14} />}
              {(status?.status === 'running' || !status) && <Spin size={14} />}
              {status?.status ?? 'queued'}
              {status?.currentPhase ? ` · ${status.currentPhase}` : ''}
            </StatusBadge>
          </div>

          <ProgressBar><ProgressFill $pct={pct} /></ProgressBar>

          <PhaseList>
            {PHASES.map((p, idx) => {
              const state: 'done' | 'current' | 'pending' =
                idx < currentPhaseIndex || status?.status === 'done' ? 'done' :
                idx === currentPhaseIndex                            ? 'current' :
                                                                       'pending'
              const ms = status?.result?.StageLatencyMs?.[p.id]
              return (
                <PhaseRow key={p.id} $state={state}>
                  {state === 'done' && <CheckCircle2 size={16} color="#22c55e" />}
                  {state === 'current' && <Spin size={16} color="#38bdf8" />}
                  {state === 'pending' && <span />}
                  <span>{p.label}</span>
                  {ms !== undefined && <Mono>{(ms / 1000).toFixed(1)}s</Mono>}
                </PhaseRow>
              )
            })}
          </PhaseList>

          {status?.status === 'done' && status.result && (
            <>
              <SummaryGrid>
                <Stat><StatLbl>Case ID</StatLbl><StatVal style={{ fontSize: '0.95rem' }}><Mono>{status.result.CaseId}</Mono></StatVal></Stat>
                <Stat><StatLbl>Validation errors</StatLbl><StatVal>{status.result.ValidationErrorsCount}</StatVal></Stat>
                <Stat><StatLbl>PDFs rendered</StatLbl><StatVal>{status.result.AssetsRenderedPdfs}</StatVal></Stat>
                <Stat><StatLbl>Images rendered</StatLbl><StatVal>{status.result.AssetsRenderedImages}</StatVal></Stat>
                <Stat><StatLbl>Blobs published</StatLbl><StatVal>{status.result.BlobsPublished}</StatVal></Stat>
                <Stat><StatLbl>Auto-fixes applied</StatLbl><StatVal>{status.result.AutoFixesApplied?.length ?? 0}</StatVal></Stat>
                <Stat><StatLbl>Refine attempted</StatLbl><StatVal>{status.result.RefineAttempted ? 'yes' : 'no'}</StatVal></Stat>
                <Stat>
                  <StatLbl>Red-team verdict</StatLbl>
                  <StatVal style={{ fontSize: '0.95rem' }}>
                    {status.result.RedTeamVerdict ?? '—'}
                    {status.result.RedTeamRerun && status.result.RedTeamVerdictInitial && (
                      <span style={{ color: 'rgba(148, 197, 255, 0.7)', fontSize: '0.8rem', marginLeft: '0.4rem' }}>
                        (was {status.result.RedTeamVerdictInitial})
                      </span>
                    )}
                  </StatVal>
                </Stat>
              </SummaryGrid>
              <Button style={{ marginTop: '1rem' }} onClick={() => navigate('/dashboard')}>
                <CheckCircle2 size={16} />Back to dashboard
              </Button>
            </>
          )}

          {status?.status === 'failed' && (
            <ErrorBox style={{ marginTop: '1rem' }}>
              <AlertCircle size={18} />
              <div>
                <div style={{ fontWeight: 600, marginBottom: '0.3rem' }}>Generation failed</div>
                {status.error || status.result?.ErrorMessage || 'Unknown error.'}
              </div>
            </ErrorBox>
          )}

          {(status?.status === 'done' || status?.status === 'failed') && (
            <Button $variant="ghost" style={{ marginTop: '1rem' }} onClick={() => { setJobId(null); setStatus(null) }}>
              Generate another
            </Button>
          )}

          {error && <ErrorBox style={{ marginTop: '1rem' }}><AlertCircle size={16} />{error}</ErrorBox>}
        </Card>
      )}
    </Page>
  )
}

export default CaseGenerationPage
