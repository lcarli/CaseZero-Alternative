import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import styled, { keyframes } from 'styled-components'
import { ArrowLeft, Star as Sparkles, AlertCircle, CheckCircle as CheckCircle2, Loader as Loader2 } from 'react-feather'
import {
  caseGenerationApi,
  type GenerateCaseRequest,
  type GenerateCaseStatus,
  type GenerationStageProgress
} from '../services/api'
import { useLanguage } from '../contexts/LanguageContext'
import type { Translations } from '../types/i18n'

const STAGE_IDS = [
  'caseDesign', 'graphConstruction', 'evidenceProduction', 'forensicWorkflow',
  'solutionDesign', 'deterministicValidation', 'solutionWitness', 'advisoryReview',
  'targetedRepair', 'finalValidation', 'finalization'
]

const STAGE_KEYS: Record<string, keyof Translations> = {
  caseDesign: 'stageCaseDesign',
  graphConstruction: 'stageGraphConstruction',
  evidenceProduction: 'stageEvidenceProduction',
  forensicWorkflow: 'stageForensicWorkflow',
  solutionDesign: 'stageSolutionDesign',
  deterministicValidation: 'stageDeterministicValidation',
  solutionWitness: 'stageSolutionWitness',
  advisoryReview: 'stageAdvisoryReview',
  targetedRepair: 'stageTargetedRepair',
  finalValidation: 'stageFinalValidation',
  finalization: 'stageFinalization'
}

interface DiagnosticPhase {
  id: string
  label: keyof Translations
  aliases?: string[]
}

const DIAGNOSTIC_PHASES: DiagnosticPhase[] = [
  { id: 'caseBible', label: 'generationPhaseCaseBible' },
  { id: 'plotOutline', label: 'generationPhasePlot' },
  { id: 'suspectCards', label: 'generationPhaseSuspects' },
  { id: 'assetPlan', label: 'generationPhaseAssetPlan' },
  { id: 'assetsAndTimelineAndBriefing', label: 'generationPhaseAssets' },
  { id: 'forensicsPlan', label: 'generationPhaseForensicsPlan' },
  { id: 'outcomesAndInitialEmails', label: 'generationPhaseOutcomes' },
  { id: 'mechanicalRules', label: 'generationPhaseMechanicalRules' },
  { id: 'rulesAndSolutionSkeleton', label: 'generationPhaseSolution' },
  { id: 'questionsAndExplanation', label: 'generationPhaseQuestions' },
  { id: 'consistency', label: 'generationPhaseConsistency' },
  { id: 'autoFixSchema', label: 'generationPhaseAutoFix' },
  { id: 'redTeamAndSolver', label: 'generationPhaseReview' },
  { id: 'prepareMaterials', label: 'generationPhasePrepareMaterials' },
  { id: 'playerInvestigation', label: 'generationPhasePlayerInvestigation' },
  { id: 'blockingReview', label: 'generationPhaseBlockingReview' },
  { id: 'refineCase', label: 'generationPhaseRefine', aliases: ['targetedRepair'] },
  { id: 'revalidateRepair', label: 'generationPhaseRepairReview' },
  { id: 'qualityGate', label: 'generationPhaseQualityGate' },
  { id: 'renderAssets', label: 'generationPhaseRender' },
  { id: 'verifyBundle', label: 'generationPhaseVerifyBundle' },
  { id: 'preparePublication', label: 'generationPhasePreparePublication' },
  { id: 'publishToBlob', label: 'generationPhasePublish' },
  { id: 'persistCase', label: 'generationPhasePersist', aliases: ['persist'] }
]

const CASE_DESIGN_SUBPHASES = ['caseBible', 'plotOutline', 'suspectCards'] as const

const PHASE_KEYS: Record<string, keyof Translations> = {
  ...Object.fromEntries(DIAGNOSTIC_PHASES.flatMap(phase =>
    [phase.id, ...(phase.aliases ?? [])].map(id => [id, phase.label]))),
  caseBible: 'phaseCaseBible',
  plotOutline: 'phasePlotOutline',
  suspectCards: 'phaseSuspectCards'
}

const LEGACY_STAGE: Record<string, string> = {
  caseBible: 'caseDesign', plotOutline: 'caseDesign', suspectCards: 'caseDesign',
  assetPlan: 'graphConstruction', assetsAndTimelineAndBriefing: 'evidenceProduction',
  rookieInitialEvidence: 'evidenceProduction', forensicsPlan: 'forensicWorkflow',
  outcomesAndInitialEmails: 'forensicWorkflow', mechanicalRules: 'solutionDesign',
  rulesAndSolutionSkeleton: 'solutionDesign', questionsAndExplanation: 'solutionDesign',
  consistency: 'deterministicValidation', autoFixSchema: 'deterministicValidation',
  redTeamAndSolver: 'solutionWitness', prepareMaterials: 'solutionWitness',
  playerInvestigation: 'solutionWitness', blockingReview: 'finalValidation',
  refineCase: 'targetedRepair', targetedRepair: 'targetedRepair',
  revalidateRepair: 'targetedRepair', qualityGate: 'finalValidation',
  redTeamRerun: 'advisoryReview', renderAssets: 'finalization', verifyBundle: 'finalization',
  preparePublication: 'finalization', publishToBlob: 'finalization',
  persistCase: 'finalization', persist: 'finalization'
}

const VERDICT_LABELS: Record<string, keyof Translations> = {
  ok: 'generationReviewOk',
  needs_review: 'generationReviewNeeded',
  reject: 'generationReviewRejected'
}

const SEVERITY_LABELS: Record<string, keyof Translations> = {
  low: 'generationSeverityLow', medium: 'generationSeverityMedium', high: 'generationSeverityHigh'
}

const ISSUE_LABELS: Record<string, keyof Translations> = {
  schema_invalid: 'generationIssueSchema',
  consistency_error: 'generationIssueConsistency',
  red_team_unavailable: 'generationIssueReviewUnavailable',
  red_team_invalid: 'generationIssueReviewInvalid',
  red_team_rejected: 'generationReviewRejected',
  red_team_needs_review: 'generationReviewNeeded',
  red_team_finding: 'generationIssueReviewFinding',
  refine_unverified: 'generationIssueRefine',
  repair_invalid: 'generationIssueRepair',
  repair_unavailable: 'generationIssueRepair',
  repair_rejected: 'generationIssueRepair',
  review_revision_mismatch: 'generationIssueReviewRevision',
  asset_render_failed: 'generationIssueRender',
  assets_skipped: 'generationIssueSkipped',
  asset_file_missing: 'generationIssueMissingFile',
  bundle_invalid: 'generationIssueBundle',
  asset_integrity_failed: 'generationIssueIntegrity',
  publication_conflict: 'generationIssuePublicationConflict',
  publication_failed: 'generationIssuePublish',
  persistence_failed: 'generationIssuePersist'
}

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
const PhaseRow = styled.li<{ $state: 'done' | 'current' | 'pending' | 'skipped' | 'failed' }>`
  display: grid;
  grid-template-columns: 24px 1fr auto;
  align-items: center;
  gap: 0.6rem;
  padding: 0.45rem 0.7rem;
  border-radius: 10px;
  background: ${p =>
    p.$state === 'current' ? 'rgba(56, 189, 248, 0.12)' :
    p.$state === 'failed'  ? 'rgba(239, 68, 68, 0.1)' :
    p.$state === 'done'    ? 'rgba(15, 23, 42, 0.5)' :
                             'transparent'};
  color: ${p => p.$state === 'pending' || p.$state === 'skipped' ? '#64748b' : '#cbd5e1'};
`
const SubphaseList = styled.ul`
  grid-column: 2 / -1;
  list-style: none;
  margin: 0.35rem 0 0;
  padding: 0 0 0 0.25rem;
  display: grid;
  gap: 0.3rem;
`
const SubphaseRow = styled.li<{ $state: 'done' | 'current' | 'pending' | 'skipped' | 'failed' }>`
  display: grid;
  grid-template-columns: 18px 1fr;
  align-items: center;
  gap: 0.5rem;
  padding: 0.3rem 0.5rem;
  border-left: 2px solid ${p =>
    p.$state === 'current' ? '#38bdf8' :
    p.$state === 'done' ? '#22c55e' :
    p.$state === 'failed' ? '#ef4444' :
    'rgba(100, 116, 139, 0.35)'};
  color: ${p => p.$state === 'pending' || p.$state === 'skipped' ? '#64748b' : '#cbd5e1'};
  font-size: 0.8rem;
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

const ErrorBox = styled.div.attrs({ role: 'alert' })`
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

const RetryBox = styled.div`
  margin-top: 1rem;
  background: rgba(245, 158, 11, 0.1);
  border: 1px solid rgba(245, 158, 11, 0.3);
  border-radius: 10px;
  padding: 0.85rem 1rem;
  font-size: 0.85rem;
  color: #fcd34d;
`

const DIFFICULTIES: { value: string; label: keyof Translations }[] = [
  { value: 'Rookie', label: 'generationRankRookie' },
  { value: 'Detective', label: 'detective' },
  { value: 'Detective2', label: 'generationRankDetective2' },
  { value: 'Sergeant', label: 'sergeant' },
  { value: 'Lieutenant', label: 'generationRankLieutenant' },
  { value: 'Captain', label: 'generationRankCaptain' },
  { value: 'Commander', label: 'generationRankCommander' }
]

const LANGUAGES: { value: string; label: keyof Translations }[] = [
  { value: 'en-US', label: 'generationLanguageEnglish' },
  { value: 'pt-BR', label: 'generationLanguagePortuguese' },
  { value: 'es-ES', label: 'generationLanguageSpanish' },
  { value: 'fr-FR', label: 'generationLanguageFrench' }
]

const POLL_INTERVAL_MS = 2500

const CaseGenerationPage = () => {
  const navigate = useNavigate()
  const { currentLanguage, t } = useLanguage()
  const [form, setForm] = useState<GenerateCaseRequest>({
    difficulty: 'Rookie',
    requiredRank: 'Rookie',
    language: currentLanguage,
    writeToDisk: true
  })
  const [jobId, setJobId] = useState<string | null>(null)
  const [status, setStatus] = useState<GenerateCaseStatus | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const pollTimer = useRef<number | null>(null)
  const mounted = useRef(true)

  // Cleanup on unmount
  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
      if (pollTimer.current !== null) window.clearTimeout(pollTimer.current)
    }
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
      if (!mounted.current) return
      setStatus(next)
      setError(null)
      if (next.status === 'done' || next.status === 'failed') {
        stop()
        return
      }
      pollTimer.current = window.setTimeout(() => pollOnce(id), POLL_INTERVAL_MS)
    } catch (err) {
      if (!mounted.current) return
      const msg = err instanceof Error ? err.message : t('generationPollError')
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
        caseId: form.caseId?.trim() || undefined,
        title: form.title?.trim() || undefined,
        theme: form.theme?.trim() || undefined,
        location: form.location?.trim() || undefined
      })
      if (!mounted.current) return
      setJobId(res.jobId)
      pollOnce(res.jobId)
    } catch (err) {
      if (mounted.current) setError(err instanceof Error ? err.message : t('generationStartError'))
    } finally {
      if (mounted.current) setSubmitting(false)
    }
  }

  const terminal = status?.status === 'done' || status?.status === 'failed'
  const result = status?.result
  const report = result?.Quality
  const quality = report && Array.isArray(report.Issues) && report.Issues.every(issue => issue != null) ? report : null
  const playerVisibleEvaluation = quality?.SolverMode === 'player-visible'
  const phaseLatency = (phase: DiagnosticPhase) =>
    [phase.id, ...(phase.aliases ?? [])].map(id => result?.StageLatencyMs?.[id]).find(ms => ms !== undefined)
  const artifactFailure = quality?.Issues.find(issue => issue.Blocking &&
    ['bundle_invalid', 'asset_integrity_failed', 'asset_file_missing'].includes(issue.Code))
  const persistencePhase = result?.StageLatencyMs?.persistCase !== undefined || result?.StageLatencyMs?.persist !== undefined || result?.StageLatencyMs?.preparePublication === undefined
    ? 'persistCase' : 'preparePublication'
  const failedPhase = !result ? null
    : artifactFailure ? artifactFailure.Area === 'publication' ? 'publishToBlob'
      : artifactFailure.Area === 'persistence' ? persistencePhase
      : artifactFailure.Area === 'rendering' ? 'renderAssets' : 'verifyBundle'
    : quality?.Issues.some(issue => issue.Blocking && issue.Code === 'schema_invalid' && issue.Area === 'artifacts') ? 'verifyBundle'
    : quality?.Issues.some(issue => issue.Blocking && ['asset_render_failed', 'asset_file_missing', 'assets_skipped'].includes(issue.Code)) ? 'renderAssets'
    : quality?.Issues.some(issue => issue.Blocking && (issue.Code === 'publication_failed' || (issue.Code === 'publication_conflict' && issue.Area === 'publication'))) ? 'publishToBlob'
    : quality?.Issues.some(issue => issue.Blocking && ['persistence_failed', 'publication_conflict'].includes(issue.Code)) ? persistencePhase
    : quality?.Issues.some(issue => issue.Blocking && issue.Code === 'repair_rejected') ? 'revalidateRepair'
    : quality?.Issues.some(issue => issue.Blocking && ['repair_invalid', 'repair_unavailable'].includes(issue.Code)) ? 'refineCase'
    : status?.currentPhase ?? (quality?.Approved === false ? 'qualityGate' : null)
  const booleanLabel = (value: boolean | undefined) =>
    t(value === undefined ? 'generationUnknown' : value ? 'generationYes' : 'generationNo')

  const stages: GenerationStageProgress[] = (() => {
    if (status?.stages?.length) return status.stages
    const currentId = status?.currentStageId ?? (status?.currentPhase ? LEGACY_STAGE[status.currentPhase] ?? status.currentPhase : undefined)
    const currentIndex = currentId ? STAGE_IDS.indexOf(currentId) : -1
    const recordedStages = new Set(Object.keys(result?.StageLatencyMs ?? {}).map(phase => LEGACY_STAGE[phase] ?? phase))
    return STAGE_IDS.map((id, index) => {
      const failed = status?.status === 'failed' && id === (failedPhase ? LEGACY_STAGE[failedPhase] ?? failedPhase : currentId)
      return {
        id,
        attempt: status?.currentAttempt ?? 1,
        status: failed ? 'failed' : terminal
          ? recordedStages.has(id) ? 'completed' : 'skipped'
          : index < currentIndex ? 'completed' : index === currentIndex ? 'running' : 'pending'
      }
    })
  })()
  const pct = status?.progressPercent ?? (terminal && !status?.stages?.length
    ? status?.status === 'done' ? 100
      : DIAGNOSTIC_PHASES.filter(phase => phaseLatency(phase) !== undefined).length / DIAGNOSTIC_PHASES.length * 100
    : stages.filter(stage => stage.status === 'completed' || stage.status === 'skipped').length / stages.length * 100)
  const stageLabel = (id: string) => STAGE_KEYS[id] ? t(STAGE_KEYS[id]) : id
  const phaseLabel = (id: string) => PHASE_KEYS[id] ? t(PHASE_KEYS[id]) : stageLabel(LEGACY_STAGE[id] ?? id)
  const caseDesignSubphaseState = (
    phase: typeof CASE_DESIGN_SUBPHASES[number],
    stageStatus: GenerationStageProgress['status']
  ): 'done' | 'current' | 'pending' | 'skipped' | 'failed' => {
    if (stageStatus === 'completed') return 'done'
    if (stageStatus === 'skipped') return 'skipped'
    if (stageStatus === 'pending') return 'pending'

    const currentIndex = status?.currentPhase
      ? CASE_DESIGN_SUBPHASES.indexOf(status.currentPhase as typeof CASE_DESIGN_SUBPHASES[number])
      : -1
    const phaseIndex = CASE_DESIGN_SUBPHASES.indexOf(phase)
    if (stageStatus === 'failed') return phaseIndex === currentIndex ? 'failed' : phaseIndex < currentIndex ? 'done' : 'pending'
    if (currentIndex < 0) return 'pending'
    return phaseIndex < currentIndex ? 'done' : phaseIndex === currentIndex ? 'current' : 'pending'
  }
  const currentAttempt = status?.currentAttempt ?? status?.result?.AttemptCount ?? 1
  const maxAttempts = status?.maxAttempts ?? status?.result?.MaxAttempts ?? 1
  const previousAttempts = status?.attempts?.filter(attempt => attempt.number < currentAttempt) ?? []
  const statusLabel = (value: GenerateCaseStatus['status'] | 'skipped') => ({
    queued: t('generationQueued'),
    running: t('running'),
    done: t('completed'),
    failed: t('failed'),
    skipped: t('generationSkipped')
  })[value]

  const update = <K extends keyof GenerateCaseRequest>(k: K, v: GenerateCaseRequest[K]) =>
    setForm(prev => ({ ...prev, [k]: v }))

  return (
    <Page>
      <TopBar>
        <Title><Sparkles size={22} />{t('generateNewCase')}</Title>
        <BackButton onClick={() => navigate('/dashboard')}><ArrowLeft size={16} />{t('backToDashboard')}</BackButton>
      </TopBar>

      {!jobId && (
        <Card>
          <Form onSubmit={handleSubmit}>
            <Row>
              <Label>
                {t('caseDifficulty')} *
                <Select value={form.difficulty} onChange={e => {
                  update('difficulty', e.target.value)
                  update('requiredRank', e.target.value)
                }}>
                  {DIFFICULTIES.map(d => <option key={d.value} value={d.value}>{t(d.label)}</option>)}
                </Select>
              </Label>
              <Label>
                {t('language')}
                <Select value={form.language ?? currentLanguage} onChange={e => update('language', e.target.value)}>
                  {LANGUAGES.map(language => <option key={language.value} value={language.value}>{t(language.label)} ({language.value})</option>)}
                </Select>
              </Label>
            </Row>
            <Row>
              <Label>
                {t('generationTitle')} ({t('generationOptional')})
                <Input maxLength={200} value={form.title ?? ''} onChange={e => update('title', e.target.value)} placeholder={t('generationTitlePlaceholder')} />
              </Label>
              <Label>
                {t('caseLocation')} ({t('generationOptional')})
                <Input maxLength={300} value={form.location ?? ''} onChange={e => update('location', e.target.value)} placeholder={t('generationLocationPlaceholder')} />
              </Label>
            </Row>
            <Label>
              {t('generationTheme')} ({t('generationOptional')})
              <Input maxLength={2000} value={form.theme ?? ''} onChange={e => update('theme', e.target.value)} placeholder={t('generationThemePlaceholder')} />
            </Label>
            <Row>
              <Label>
                {t('generationCaseId')} ({t('generationOptional')})
                <Input maxLength={128} pattern="[A-Za-z0-9][A-Za-z0-9_-]*" value={form.caseId ?? ''} onChange={e => update('caseId', e.target.value)} placeholder={t('generationAuto')} />
              </Label>
              <Label>
                {t('generationSeed')} ({t('generationOptional')})
                <Input type="number" min={-2147483648} max={2147483647} step={1} value={form.seed ?? ''} onChange={e => update('seed', e.target.value === '' ? undefined : Number(e.target.value))} placeholder={t('generationRandom')} />
              </Label>
            </Row>
            <Label style={{ flexDirection: 'row', alignItems: 'center' }}>
              <input type="checkbox" checked={form.writeToDisk ?? true} onChange={e => update('writeToDisk', e.target.checked)} />
              {t('generationSaveLocally')}
            </Label>
            <div>{t('generationPreviewHelp')}</div>
            <Button type="submit" disabled={submitting}>
              {submitting ? <Spin size={16} /> : <Sparkles size={16} />}
              {submitting ? t('generationStarting') : t('generateCaseBtn')}
            </Button>
            {error && <ErrorBox><AlertCircle size={16} />{error}</ErrorBox>}
          </Form>
        </Card>
      )}

      {jobId && (
        <Card>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.75rem' }}>
            <div>
              <div style={{ fontSize: '0.78rem', color: '#64748b', textTransform: 'uppercase', letterSpacing: '0.06em' }}>{t('generationJob')}</div>
              <Mono>{jobId}</Mono>
            </div>
            <StatusBadge $status={status?.status ?? 'queued'}>
              {status?.status === 'done' && <CheckCircle2 size={14} />}
              {status?.status === 'failed' && <AlertCircle size={14} />}
              {(status?.status === 'running' || !status) && <Spin size={14} />}
              {statusLabel(status?.status ?? 'queued')}
              {(status?.currentStageId || status?.currentPhase)
                ? ` · ${status.currentPhase ? phaseLabel(status.currentPhase) : stageLabel(status.currentStageId!)}`
                : ''}
            </StatusBadge>
          </div>

          <ProgressBar><ProgressFill $pct={pct} /></ProgressBar>

          <div style={{ marginTop: '0.75rem', color: '#94a3b8', fontSize: '0.85rem' }}>
            {t('generationAttempt')} {currentAttempt} / {maxAttempts}
          </div>

          {status?.nextRetryAt && (
            <RetryBox>
              <div style={{ fontWeight: 600 }}>{t('generationRetryScheduled')}</div>
              <div>{new Date(status.nextRetryAt).toLocaleTimeString(currentLanguage)}</div>
              {status.retryReason && <div style={{ marginTop: '0.35rem' }}>{status.retryReason}</div>}
            </RetryBox>
          )}

          {previousAttempts.length > 0 && (
            <div style={{ marginTop: '1rem' }}>
              <div style={{ color: '#94a3b8', fontSize: '0.78rem', textTransform: 'uppercase', letterSpacing: '0.06em' }}>
                {t('generationAttemptHistory')}
              </div>
              <PhaseList>
                {previousAttempts.map(attempt => (
                  <PhaseRow key={attempt.number} $state={attempt.status === 'completed' ? 'done' : 'failed'}>
                    {attempt.status === 'completed'
                      ? <CheckCircle2 size={16} color="#22c55e" />
                      : <AlertCircle size={16} color="#ef4444" />}
                    <span>{t('generationAttempt')} {attempt.number} · {attempt.status === 'completed' ? t('completed') : t('failed')}</span>
                    {attempt.startedAt && attempt.completedAt && (
                      <Mono>{((new Date(attempt.completedAt).getTime() - new Date(attempt.startedAt).getTime()) / 1000).toFixed(1)}s</Mono>
                    )}
                  </PhaseRow>
                ))}
              </PhaseList>
            </div>
          )}

          <PhaseList>
            {stages.map(stage => {
              const state =
                stage.status === 'completed' ? 'done' :
                stage.status === 'running' ? 'current' :
                stage.status
              const ms = stage.durationMs
              return (
                <PhaseRow key={stage.id} $state={state}>
                  {state === 'done' && <CheckCircle2 size={16} color="#22c55e" />}
                  {state === 'current' && <Spin size={16} color="#38bdf8" />}
                  {state === 'failed' && <AlertCircle size={16} color="#ef4444" />}
                  {(state === 'pending' || state === 'skipped') && <span />}
                  <span>
                    {stageLabel(stage.id)}
                    {state === 'skipped' && ` · ${statusLabel('skipped')}`}
                    {stage.attempt > 1 && ` · ${t('generationAttempt')} ${stage.attempt}`}
                  </span>
                  {ms != null && <Mono>{(ms / 1000).toFixed(1)}s</Mono>}
                  {stage.id === 'caseDesign' && (
                    <SubphaseList>
                      {CASE_DESIGN_SUBPHASES.map(phase => {
                        const subphaseState = caseDesignSubphaseState(phase, stage.status)
                        return (
                          <SubphaseRow key={phase} $state={subphaseState}>
                            {subphaseState === 'done' && <CheckCircle2 size={13} color="#22c55e" />}
                            {subphaseState === 'current' && <Spin size={13} color="#38bdf8" />}
                            {subphaseState === 'failed' && <AlertCircle size={13} color="#ef4444" />}
                            {(subphaseState === 'pending' || subphaseState === 'skipped') && <span />}
                            <span>{phaseLabel(phase)}</span>
                          </SubphaseRow>
                        )
                      })}
                    </SubphaseList>
                  )}
                </PhaseRow>
              )
            })}
          </PhaseList>

          {terminal && result && (
            <>
              <details open>
                <summary>{t('generationTechnicalDetails')}</summary>
                <PhaseList>
                  {DIAGNOSTIC_PHASES.map(phase => {
                    const ms = phaseLatency(phase)
                    const state = status?.status === 'failed' && (phase.id === failedPhase || phase.aliases?.includes(failedPhase ?? ''))
                      ? 'failed' : ms !== undefined ? 'done' : 'skipped'
                    return (
                      <PhaseRow key={phase.id} $state={state} aria-label={`${t(phase.label)}: ${t(state === 'done' ? 'completed' : state === 'failed' ? 'failed' : 'generationNotRun')}`}>
                        {state === 'done' && <CheckCircle2 size={16} color="#22c55e" />}
                        {state === 'failed' && <AlertCircle size={16} color="#fca5a5" />}
                        {state === 'skipped' && <span />}
                        <span>{t(phase.label)}</span>
                        <span>
                          {ms !== undefined && <Mono>{(ms / 1000).toFixed(1)}s</Mono>}
                          {state === 'skipped' && t('generationNotRun')}
                          {state === 'failed' && <>{ms !== undefined && ' · '}{t('failed')}</>}
                        </span>
                      </PhaseRow>
                    )
                  })}
                </PhaseList>
              </details>
              <SummaryGrid>
                <Stat><StatLbl>{t('generationCaseId')}</StatLbl><StatVal style={{ fontSize: '0.95rem' }}><Mono>{result.CaseId}</Mono></StatVal></Stat>
                <Stat><StatLbl>{t('generationApproved')}</StatLbl><StatVal>{booleanLabel(quality?.Approved)}</StatVal></Stat>
                <Stat><StatLbl>{t('generationPersisted')}</StatLbl><StatVal>{booleanLabel(quality?.Persisted)}</StatVal></Stat>
                <Stat><StatLbl>{t('generationPublished')}</StatLbl><StatVal>{booleanLabel(quality?.Published)}</StatVal></Stat>
                <Stat><StatLbl>{t('generationOutputMode')}</StatLbl><StatVal>{t(!quality ? 'generationUnknown' : quality.Preview ? 'generationPreview' : quality.PublicationRequested ? 'generationPublishedMode' : 'generationLocalOnly')}</StatVal></Stat>
                <Stat><StatLbl>{t('generationValidationErrors')}</StatLbl><StatVal>{result.ValidationErrorsCount}</StatVal></Stat>
                <Stat><StatLbl>{t('generationPdfsRendered')}</StatLbl><StatVal>{result.AssetsRenderedPdfs}</StatVal></Stat>
                <Stat><StatLbl>{t('generationImagesRendered')}</StatLbl><StatVal>{result.AssetsRenderedImages}</StatVal></Stat>
                <Stat><StatLbl>{t('generationAssetsSkipped')}</StatLbl><StatVal>{result.AssetsSkipped ?? 0}</StatVal></Stat>
                <Stat><StatLbl>{t('generationBlobsPublished')}</StatLbl><StatVal>{result.BlobsPublished}</StatVal></Stat>
                <Stat><StatLbl>{t('generationAutoFixes')}</StatLbl><StatVal>{result.AutoFixesApplied?.length ?? 0}</StatVal></Stat>
                <Stat><StatLbl>{t('generationRefineAttempted')}</StatLbl><StatVal>{booleanLabel(result.RefineAttempted)}</StatVal></Stat>
                <Stat><StatLbl>{t('generationRevision')}</StatLbl><StatVal>{quality?.Revision ?? t('generationUnknown')}</StatVal></Stat>
                <Stat><StatLbl>{t('generationReviewedRevision')}</StatLbl><StatVal>{quality?.ReviewedRevision ?? t('generationUnknown')}</StatVal></Stat>
                <Stat><StatLbl>{t('generationBundleVerified')}</StatLbl><StatVal>{booleanLabel(result.Bundle?.Verified)}</StatVal></Stat>
                <Stat><StatLbl>{t('generationBundleRevision')}</StatLbl><StatVal>{result.Bundle?.Revision ?? t('generationUnknown')}</StatVal></Stat>
                <Stat><StatLbl>{t('generationBundleAssets')}</StatLbl><StatVal>{result.Bundle?.AssetsVerified ?? t('generationUnknown')}</StatVal></Stat>
                <Stat><StatLbl>{t('generationBundleBytes')}</StatLbl><StatVal>{result.Bundle?.TotalBytes ?? t('generationUnknown')}</StatVal></Stat>
                {result.Repair?.Attempted && <Stat><StatLbl>{t('generationRepairAccepted')}</StatLbl><StatVal>{booleanLabel(result.Repair.Accepted)}</StatVal></Stat>}
                <Stat><StatLbl>{t('generationRedTeamVerdict')}</StatLbl><StatVal style={{ fontSize: '0.95rem' }}>{t(VERDICT_LABELS[result.RedTeamVerdict ?? ''] ?? 'generationUnknown')}</StatVal></Stat>
                <Stat><StatLbl>{t('generationGraphValidation')}</StatLbl><StatVal>{booleanLabel(result.GraphValidationPassed)}</StatVal></Stat>
                <Stat><StatLbl>{t('generationSolutionWitness')}</StatLbl><StatVal>{booleanLabel(result.SolverSucceeded)}</StatVal></Stat>
                <Stat><StatLbl>{t('generationRepairOperations')}</StatLbl><StatVal>{result.RepairOperationCount ?? 0}</StatVal></Stat>
                <Stat><StatLbl>{t('generationFirstPass')}</StatLbl><StatVal>{booleanLabel(result.FirstPassSuccess)}</StatVal></Stat>
                <Stat><StatLbl>{t('generationAttempt')}</StatLbl><StatVal>{result.AttemptCount ?? currentAttempt} / {result.MaxAttempts ?? maxAttempts}</StatVal></Stat>
                <Stat><StatLbl>{t('generationSolverScore')}</StatLbl><StatVal>{result.SolverScore?.toFixed(2) ?? t('generationUnknown')}</StatVal></Stat>
                <Stat><StatLbl>{t('generationAdvisoryFindings')}</StatLbl><StatVal>{Object.values(result.SpecialistFindingsByCategory ?? {}).reduce((sum, count) => sum + count, 0)}</StatVal></Stat>
              </SummaryGrid>
              <p>{t(playerVisibleEvaluation ? 'generationPlayerQualityScope' : 'generationQualityScope')}</p>
              <p>{t(playerVisibleEvaluation ? 'generationPlayerInvestigationScope' : 'generationSolverDiagnostic')}</p>
              {result.Bundle && (
                <details>
                  <summary>{t('generationBundleId')}</summary>
                  <Mono>{result.Bundle.BundleId}</Mono>
                  <p>{t('generationBundleChecksum')}</p>
                  <Mono>{result.Bundle.ManifestChecksum}</Mono>
                </details>
              )}
              {result.Repair?.Attempted && (
                <details>
                  <summary>{t('generationRepairChanges')}</summary>
                  <Mono>{result.Repair.ChangedComponents.join(', ') || t('generationUnknown')}</Mono>
                  <p>{t('generationRepairOriginalIssues')}</p>
                  <ul>{result.Repair.OriginalBlockers.map((issue, index) => <li key={index}>{issue}</li>)}</ul>
                  {result.Repair.ErrorMessage && <p>{result.Repair.ErrorMessage}</p>}
                </details>
              )}
              {quality && quality.Issues.length > 0 && (
                <div>
                  <h2>{t('generationQualityIssues')}</h2>
                  <ul>
                    {quality.Issues.map((issue, index) => (
                      <li key={`${issue.Code}-${index}`}>
                        <strong>{t(ISSUE_LABELS[issue.Code] ?? 'generationQualityIssue')}</strong>
                        {' · '}{t(SEVERITY_LABELS[issue.Severity] ?? 'generationUnknown')}
                        {' · '}{t(issue.Blocking ? 'generationBlocking' : 'generationWarning')}
                        {issue.Reference && <> · <Mono>{issue.Reference}</Mono></>}
                        <details>
                          <summary>{t('generationTechnicalDetails')}</summary>
                          <Mono>{issue.Code}</Mono>
                          <p>{issue.Message}</p>
                        </details>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
              <Button style={{ marginTop: '1rem' }} onClick={() => navigate('/dashboard')}>
                <CheckCircle2 size={16} />{t('backToDashboard')}
              </Button>
            </>
          )}

          {status?.status === 'failed' && (
            <ErrorBox style={{ marginTop: '1rem' }}>
              <AlertCircle size={18} />
              <div>
                <div style={{ fontWeight: 600, marginBottom: '0.3rem' }}>{t('caseGenerationError')}</div>
                <details>
                  <summary>{t('generationTechnicalDetails')}</summary>
                  {status.error || result?.ErrorMessage || t('generationUnknownError')}
                </details>
              </div>
            </ErrorBox>
          )}

          {(status?.status === 'done' || status?.status === 'failed') && (
            <Button $variant="ghost" style={{ marginTop: '1rem' }} onClick={() => { setJobId(null); setStatus(null) }}>
              {t('generationAnother')}
            </Button>
          )}

          {error && <ErrorBox style={{ marginTop: '1rem' }}><AlertCircle size={16} />{error}</ErrorBox>}
        </Card>
      )}
    </Page>
  )
}

export default CaseGenerationPage
