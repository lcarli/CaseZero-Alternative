import React, { useEffect, useMemo, useState } from 'react'
import styled from 'styled-components'
import { useCase, useAssets, useSuspects, useSubmission } from '../../contexts/CaseContext'
import { useLanguage } from '../../hooks/useLanguageContext'
import { forensicRequestApi, type ForensicRequestDTO } from '../../services/api'
import type { SubmitCaseRequest } from '../../types/caseV2'

const Container = styled.div`
  height: 100%;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  overflow-y: auto;
  padding: 1rem;
  color: rgba(255, 255, 255, 0.9);
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
`

const Header = styled.div`
  background: rgba(0, 0, 0, 0.3);
  padding: 1rem;
  border-radius: 8px;
  border: 1px solid rgba(74, 158, 255, 0.3);
`

const HeaderTitle = styled.h3`
  margin: 0 0 0.25rem 0;
  color: #4a9eff;
  display: flex;
  align-items: center;
  gap: 0.5rem;
`

const HeaderSubtitle = styled.div`
  font-size: 12px;
  color: rgba(255, 255, 255, 0.7);
`

const Section = styled.section`
  background: rgba(255, 255, 255, 0.02);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
`

const SectionTitle = styled.h4`
  margin: 0;
  color: #4a9eff;
  font-size: 13px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  border-bottom: 1px solid rgba(74, 158, 255, 0.2);
  padding-bottom: 0.4rem;
`

// Card-based suspect picker. We deliberately avoid native <select> here:
// Chromium on Windows renders the option popup through the OS shell and
// frequently strips author CSS, producing unreadable text even with
// color-scheme: dark. A grid of styled radio cards gives us full styling
// control across platforms and matches the look of the question options.
const SuspectGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 0.5rem;
`

const SuspectCard = styled.label<{ $selected: boolean; $disabled: boolean }>`
  display: flex;
  align-items: center;
  gap: 0.6rem;
  padding: 0.7rem 0.85rem;
  border-radius: 6px;
  border: 1px solid ${props => (props.$selected ? 'rgba(74, 158, 255, 0.8)' : 'rgba(255, 255, 255, 0.12)')};
  background: ${props => (props.$selected ? 'rgba(74, 158, 255, 0.12)' : 'rgba(0, 0, 0, 0.25)')};
  color: rgba(255, 255, 255, 0.92);
  font-size: 13px;
  cursor: ${props => (props.$disabled ? 'not-allowed' : 'pointer')};
  opacity: ${props => (props.$disabled ? 0.5 : 1)};
  transition: background 120ms ease, border-color 120ms ease;

  &:hover {
    background: ${props =>
      props.$disabled
        ? props.$selected
          ? 'rgba(74, 158, 255, 0.12)'
          : 'rgba(0, 0, 0, 0.25)'
        : props.$selected
          ? 'rgba(74, 158, 255, 0.18)'
          : 'rgba(255, 255, 255, 0.05)'};
  }

  input {
    margin: 0;
    accent-color: #4a9eff;
  }
`

const ProgressGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: 0.4rem;
`

const ProgressChip = styled.div<{ $state: 'ok' | 'partial' | 'missing' }>`
  display: flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.4rem 0.6rem;
  border-radius: 4px;
  font-size: 12px;
  background: ${p =>
    p.$state === 'ok'
      ? 'rgba(46, 213, 115, 0.10)'
      : p.$state === 'partial'
        ? 'rgba(255, 193, 7, 0.10)'
        : 'rgba(255, 71, 87, 0.08)'};
  border: 1px solid ${p =>
    p.$state === 'ok'
      ? 'rgba(46, 213, 115, 0.4)'
      : p.$state === 'partial'
        ? 'rgba(255, 193, 7, 0.4)'
        : 'rgba(255, 71, 87, 0.4)'};
  color: ${p =>
    p.$state === 'ok'
      ? '#2ed573'
      : p.$state === 'partial'
        ? '#ffc107'
        : '#ff6b81'};
`

const ProgressHint = styled.div<{ $state: 'ok' | 'partial' | 'missing' }>`
  font-size: 12px;
  color: ${p => (p.$state === 'ok' ? 'rgba(46, 213, 115, 0.9)' : 'rgba(255, 193, 7, 0.9)')};
`

const CategoryBadge = styled.span`
  font-size: 10px;
  letter-spacing: 0.4px;
  text-transform: uppercase;
  padding: 0.1rem 0.4rem;
  border-radius: 3px;
  background: rgba(74, 158, 255, 0.18);
  color: #6ab8ff;
  margin-left: 0.5rem;
`

const EmptyAnalyses = styled.div`
  font-size: 12px;
  color: rgba(255, 255, 255, 0.6);
  padding: 0.6rem;
  background: rgba(255, 255, 255, 0.03);
  border: 1px dashed rgba(255, 255, 255, 0.15);
  border-radius: 4px;
`

const CheckboxGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 0.4rem;
`

const CheckboxItem = styled.label`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.4rem;
  border-radius: 4px;
  cursor: pointer;
  font-size: 13px;
  color: rgba(255, 255, 255, 0.85);
  background: rgba(255, 255, 255, 0.02);

  &:hover {
    background: rgba(255, 255, 255, 0.05);
  }
`

const RadioRow = styled.label`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.4rem;
  border-radius: 4px;
  cursor: pointer;
  font-size: 13px;
  color: rgba(255, 255, 255, 0.85);

  &:hover {
    background: rgba(255, 255, 255, 0.04);
  }
`

const QuestionBlock = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.5rem;
  border-radius: 4px;
  background: rgba(255, 255, 255, 0.02);
`

const QuestionPrompt = styled.div`
  font-size: 13px;
  font-weight: 500;
  color: rgba(255, 255, 255, 0.95);
`

const SubmitButton = styled.button`
  padding: 0.85rem 1.5rem;
  background: #4a9eff;
  color: white;
  border: none;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.2s ease;

  &:hover:not(:disabled) {
    background: #357abd;
  }

  &:disabled {
    opacity: 0.5;
    cursor: not-allowed;
  }
`

const ResultPanel = styled.div<{ $correct: boolean }>`
  padding: 1rem;
  border-radius: 8px;
  border: 1px solid ${p => (p.$correct ? 'rgba(46, 213, 115, 0.4)' : 'rgba(255, 71, 87, 0.4)')};
  background: ${p => (p.$correct ? 'rgba(46, 213, 115, 0.08)' : 'rgba(255, 71, 87, 0.08)')};
  color: ${p => (p.$correct ? '#2ed573' : '#ff6b81')};
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
`

const ScoreLine = styled.div`
  font-size: 13px;
  color: rgba(255, 255, 255, 0.85);
`

const BreakdownGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: 0.5rem;
  font-size: 12px;
`

const BreakdownItem = styled.div<{ $ratio: number }>`
  padding: 0.5rem 0.6rem;
  border-radius: 4px;
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid ${p =>
    p.$ratio >= 0.999
      ? 'rgba(46, 213, 115, 0.5)'
      : p.$ratio >= 0.5
        ? 'rgba(255, 193, 7, 0.5)'
        : 'rgba(255, 71, 87, 0.5)'};
  color: rgba(255, 255, 255, 0.92);
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
`

const BreakdownLabel = styled.span`
  font-weight: 600;
  font-size: 12px;
`

const BreakdownValue = styled.span<{ $ratio: number }>`
  font-size: 13px;
  font-variant-numeric: tabular-nums;
  color: ${p =>
    p.$ratio >= 0.999
      ? '#5cd687'
      : p.$ratio >= 0.5
        ? '#ffc845'
        : '#ff6b7a'};
`

const BreakdownBar = styled.div`
  width: 100%;
  height: 4px;
  border-radius: 2px;
  background: rgba(255, 255, 255, 0.08);
  overflow: hidden;
`

const BreakdownBarFill = styled.div<{ $ratio: number }>`
  width: ${p => Math.max(0, Math.min(1, p.$ratio)) * 100}%;
  height: 100%;
  background: ${p =>
    p.$ratio >= 0.999
      ? '#2ed573'
      : p.$ratio >= 0.5
        ? '#ffc845'
        : '#ff4757'};
  transition: width 0.3s ease;
`

const ExplanationPanel = styled.div`
  padding: 1rem;
  background: rgba(74, 158, 255, 0.08);
  border: 1px solid rgba(74, 158, 255, 0.3);
  border-radius: 6px;
  color: rgba(255, 255, 255, 0.9);
  font-size: 13px;
  white-space: pre-wrap;
`

const SubmitCase: React.FC = () => {
  const { t } = useLanguage()
  const { state, submitCase } = useCase()
  const assets = useAssets()
  const suspects = useSuspects()
  const submission = useSubmission()

  const caseId = state.case?.caseId
  const questions = useMemo(() => state.case?.solution.questions ?? [], [state.case])
  const attemptsRemaining = submission.maxAttempts - submission.attemptsUsed
  const lastResult = submission.lastResult
  const isExhausted = attemptsRemaining <= 0

  const [suspectId, setSuspectId] = useState<string>('')
  const [evidenceIds, setEvidenceIds] = useState<string[]>([])
  const [selectedAnalysisIds, setSelectedAnalysisIds] = useState<string[]>([])
  const [answers, setAnswers] = useState<Record<string, string>>({})
  const [submitting, setSubmitting] = useState(false)

  const [completedAnalyses, setCompletedAnalyses] = useState<ForensicRequestDTO[] | null>(null)
  const [loadingAnalyses, setLoadingAnalyses] = useState(false)

  // Sort assets: forensic results / analysis reports first, then originals.
  // The category names emitted by the backend vary by case template (and by language),
  // so detect "forensic" with a loose case-insensitive match instead of a fixed list.
  const sortedAssets = useMemo(() => {
    const isForensic = (cat?: string) => !!cat && /forensic|peric|forense|médico|legal/i.test(cat)
    return [...assets].sort((a, b) => {
      const ar = isForensic(a.category) ? 0 : 1
      const br = isForensic(b.category) ? 0 : 1
      if (ar !== br) return ar - br
      return a.title.localeCompare(b.title)
    })
  }, [assets])

  useEffect(() => {
    if (!caseId) return
    let cancelled = false
    setLoadingAnalyses(true)
    forensicRequestApi
      .getForensicRequests(caseId)
      .then(list => {
        if (cancelled) return
        setCompletedAnalyses(list.filter(r => r.status === 'completed'))
      })
      .catch(err => {
        if (cancelled) return
        console.error('Failed to load forensic requests:', err)
        setCompletedAnalyses([])
      })
      .finally(() => {
        if (!cancelled) setLoadingAnalyses(false)
      })
    return () => {
      cancelled = true
    }
  }, [caseId])

  const toggleEvidence = (id: string) => {
    setEvidenceIds(prev => (prev.includes(id) ? prev.filter(e => e !== id) : [...prev, id]))
  }

  const toggleAnalysis = (id: string) => {
    setSelectedAnalysisIds(prev => (prev.includes(id) ? prev.filter(e => e !== id) : [...prev, id]))
  }

  const handleAnswerChange = (questionId: string, optionId: string) => {
    setAnswers(prev => ({ ...prev, [questionId]: optionId }))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (isExhausted || submitting) return

    const payload: SubmitCaseRequest = {
      suspectId,
      evidenceIds,
      analysisIds: selectedAnalysisIds,
      answers: questions.map(q => ({
        questionId: q.id,
        optionId: answers[q.id] || '',
      })),
    }

    setSubmitting(true)
    try {
      await submitCase(payload)
    } catch (err) {
      console.error('SubmitCase failed:', err)
    } finally {
      setSubmitting(false)
    }
  }

  const attemptsLabel = isExhausted
    ? t('submitCaseAttemptsExhausted')
    : t('submitCaseAttemptsRemaining').replace('{n}', String(attemptsRemaining))

  // Progress state (advisory only — submit stays enabled as long as suspect is set,
  // because the backend permits partial submissions and just gives 0 on missing buckets).
  const answeredCount = questions.filter(q => !!answers[q.id]).length
  const suspectState: 'ok' | 'missing' = suspectId ? 'ok' : 'missing'
  const evidenceState: 'ok' | 'partial' = evidenceIds.length > 0 ? 'ok' : 'partial'
  const analysisState: 'ok' | 'partial' = selectedAnalysisIds.length > 0 ? 'ok' : 'partial'
  const questionsState: 'ok' | 'partial' | 'missing' =
    questions.length === 0
      ? 'ok'
      : answeredCount === questions.length
        ? 'ok'
        : answeredCount === 0
          ? 'missing'
          : 'partial'

  const allReady =
    suspectState === 'ok' &&
    evidenceState === 'ok' &&
    analysisState === 'ok' &&
    questionsState === 'ok'

  return (
    <Container>
      <Header>
        <HeaderTitle>⚖️ {t('submitCaseTitle')}</HeaderTitle>
        <HeaderSubtitle>{attemptsLabel}</HeaderSubtitle>
      </Header>

      <Section>
        <SectionTitle>{t('submitCaseProgressTitle')}</SectionTitle>
        <ProgressGrid>
          <ProgressChip $state={suspectState}>
            {suspectState === 'ok' ? '✓' : '○'} {t('submitCaseProgressSuspect')}
          </ProgressChip>
          <ProgressChip $state={evidenceState}>
            {evidenceState === 'ok' ? '✓' : '○'}{' '}
            {t('submitCaseProgressEvidence').replace('{n}', String(evidenceIds.length))}
          </ProgressChip>
          <ProgressChip $state={analysisState}>
            {analysisState === 'ok' ? '✓' : '○'}{' '}
            {t('submitCaseProgressAnalysis').replace('{n}', String(selectedAnalysisIds.length))}
          </ProgressChip>
          <ProgressChip $state={questionsState}>
            {questionsState === 'ok' ? '✓' : '○'}{' '}
            {t('submitCaseProgressQuestions')
              .replace('{a}', String(answeredCount))
              .replace('{b}', String(questions.length))}
          </ProgressChip>
        </ProgressGrid>
        {allReady ? (
          <ProgressHint $state="ok">✓ {t('submitCaseProgressReady')}</ProgressHint>
        ) : suspectState === 'missing' ? (
          <ProgressHint $state="missing">{t('submitCaseProgressMissingSuspect')}</ProgressHint>
        ) : questionsState !== 'ok' && questions.length > 0 ? (
          <ProgressHint $state="partial">{t('submitCaseProgressMissingQuestions')}</ProgressHint>
        ) : null}
      </Section>

      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
        <Section>
          <SectionTitle>{t('submitCaseSuspectLabel')}</SectionTitle>
          {suspects.length === 0 ? (
            <EmptyAnalyses>{t('submitCaseSuspectsEmpty')}</EmptyAnalyses>
          ) : (
            <SuspectGrid role="radiogroup" aria-label={t('submitCaseSuspectLabel')}>
              {suspects.map(s => {
                const label = s.name?.trim() || s.alias?.trim() || s.id
                const selected = suspectId === s.id
                return (
                  <SuspectCard
                    key={s.id}
                    $selected={selected}
                    $disabled={isExhausted}
                  >
                    <input
                      type="radio"
                      name="submit-case-suspect"
                      value={s.id}
                      checked={selected}
                      onChange={() => setSuspectId(s.id)}
                      disabled={isExhausted}
                    />
                    <span>{label}</span>
                  </SuspectCard>
                )
              })}
            </SuspectGrid>
          )}
        </Section>

        <Section>
          <SectionTitle>{t('submitCaseEvidenceLabel')}</SectionTitle>
          <CheckboxGrid>
            {sortedAssets.map(asset => (
              <CheckboxItem key={asset.id}>
                <input
                  type="checkbox"
                  checked={evidenceIds.includes(asset.id)}
                  onChange={() => toggleEvidence(asset.id)}
                  disabled={isExhausted}
                />
                <span>
                  {asset.title}
                  {asset.category && <CategoryBadge>{asset.category}</CategoryBadge>}
                </span>
              </CheckboxItem>
            ))}
          </CheckboxGrid>
        </Section>

        <Section>
          <SectionTitle>{t('submitCaseAnalysisLabel')}</SectionTitle>
          {loadingAnalyses ? (
            <EmptyAnalyses>{t('submitCaseAnalysisLoading')}</EmptyAnalyses>
          ) : completedAnalyses && completedAnalyses.length > 0 ? (
            <CheckboxGrid>
              {completedAnalyses.map(req => {
                const analysisId = `${req.inputAssetId}:${req.analysisType}`
                const label = req.inputAssetName
                  ? `${req.inputAssetName} — ${req.analysisType}`
                  : analysisId
                return (
                  <CheckboxItem key={analysisId}>
                    <input
                      type="checkbox"
                      checked={selectedAnalysisIds.includes(analysisId)}
                      onChange={() => toggleAnalysis(analysisId)}
                      disabled={isExhausted}
                    />
                    <span>{label}</span>
                  </CheckboxItem>
                )
              })}
            </CheckboxGrid>
          ) : (
            <EmptyAnalyses>{t('submitCaseAnalysisEmpty')}</EmptyAnalyses>
          )}
        </Section>

        {questions.length > 0 && (
          <Section>
            <SectionTitle>{t('submitCaseQuestionsHeader')}</SectionTitle>
            {questions.map(q => (
              <QuestionBlock key={q.id}>
                <QuestionPrompt>{q.prompt}</QuestionPrompt>
                {q.options.map(opt => (
                  <RadioRow key={opt.id}>
                    <input
                      type="radio"
                      name={q.id}
                      value={opt.id}
                      checked={answers[q.id] === opt.id}
                      onChange={() => handleAnswerChange(q.id, opt.id)}
                      disabled={isExhausted}
                    />
                    <span>{opt.label}</span>
                  </RadioRow>
                ))}
              </QuestionBlock>
            ))}
          </Section>
        )}

        <SubmitButton type="submit" disabled={isExhausted || submitting || !suspectId}>
          {t('submitCaseSubmitButton')}
        </SubmitButton>
      </form>

      {lastResult && (
        <ResultPanel $correct={lastResult.correct}>
          <div style={{ fontWeight: 600 }}>
            {lastResult.correct
              ? t('submitCaseResultCorrect')
              : t('submitCaseResultIncorrect')}
          </div>
          <ScoreLine>
            {t('submitCaseScore')}: {lastResult.score}
          </ScoreLine>
          <BreakdownGrid>
            {([
              { labelKey: 'submitCaseBreakdownCulprit', value: lastResult.breakdown.culpritScore, max: lastResult.maxScores.culprit },
              { labelKey: 'submitCaseBreakdownEvidence', value: lastResult.breakdown.evidenceScore, max: lastResult.maxScores.evidence },
              { labelKey: 'submitCaseBreakdownAnalysis', value: lastResult.breakdown.analysisScore, max: lastResult.maxScores.analysis },
              { labelKey: 'submitCaseBreakdownQuestions', value: lastResult.breakdown.questionsScore, max: lastResult.maxScores.questions },
            ] as const).map(({ labelKey, value, max }) => {
              const ratio = max > 0 ? value / max : 0
              const pct = Math.round(ratio * 100)
              return (
                <BreakdownItem key={labelKey} $ratio={ratio}>
                  <BreakdownLabel>{t(labelKey)}</BreakdownLabel>
                  <BreakdownValue $ratio={ratio}>{pct}% ({value.toFixed(2)} / {max.toFixed(2)})</BreakdownValue>
                  <BreakdownBar>
                    <BreakdownBarFill $ratio={ratio} />
                  </BreakdownBar>
                </BreakdownItem>
              )
            })}
          </BreakdownGrid>
          <ScoreLine>
            {t('submitCaseAttemptsRemaining').replace('{n}', String(lastResult.attemptsRemaining))}
          </ScoreLine>
          <ScoreLine style={{ marginTop: '0.25rem' }}>
            {lastResult.feedbackCode === 'correct'
              ? t('submitCaseFeedbackCorrect')
              : lastResult.feedbackCode === 'incorrect_no_attempts'
                ? t('submitCaseFeedbackIncorrectFinal')
                : t('submitCaseFeedbackIncorrectRetry').replace('{n}', String(lastResult.attemptsRemaining))}
          </ScoreLine>
        </ResultPanel>
      )}

      {isExhausted && lastResult?.explanationMarkdown && (
        <ExplanationPanel>
          <div style={{ fontWeight: 600, marginBottom: '0.5rem', color: '#4a9eff' }}>
            {t('submitCaseExplanationHeader')}
          </div>
          {lastResult.explanationMarkdown}
        </ExplanationPanel>
      )}
    </Container>
  )
}

export default SubmitCase
