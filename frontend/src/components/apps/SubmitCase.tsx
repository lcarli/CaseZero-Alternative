import React, { useMemo, useState } from 'react'
import styled from 'styled-components'
import { useCase, useAssets, useSuspects, useSubmission } from '../../contexts/CaseContext'
import { useLanguage } from '../../hooks/useLanguageContext'
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

const Select = styled.select`
  padding: 0.6rem;
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 4px;
  color: white;
  font-size: 13px;

  option {
    background: #1a1a2e;
    color: white;
  }
`

const TextArea = styled.textarea`
  padding: 0.6rem;
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 4px;
  color: white;
  font-size: 13px;
  min-height: 80px;
  resize: vertical;
  font-family: inherit;
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

const BreakdownItem = styled.div<{ $ok: boolean }>`
  padding: 0.5rem;
  border-radius: 4px;
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid ${p => (p.$ok ? 'rgba(46, 213, 115, 0.4)' : 'rgba(255, 71, 87, 0.4)')};
  color: rgba(255, 255, 255, 0.9);
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

  const questions = useMemo(() => state.case?.solution.questions ?? [], [state.case])
  const attemptsRemaining = submission.maxAttempts - submission.attemptsUsed
  const lastResult = submission.lastResult
  const isExhausted = attemptsRemaining <= 0

  const [suspectId, setSuspectId] = useState<string>('')
  const [evidenceIds, setEvidenceIds] = useState<string[]>([])
  const [analysisText, setAnalysisText] = useState<string>('')
  const [answers, setAnswers] = useState<Record<string, string>>({})
  const [submitting, setSubmitting] = useState(false)

  const toggleEvidence = (id: string) => {
    setEvidenceIds(prev => (prev.includes(id) ? prev.filter(e => e !== id) : [...prev, id]))
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
      analysisIds: analysisText
        .split(/[\n,]/)
        .map(s => s.trim())
        .filter(Boolean),
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

  return (
    <Container>
      <Header>
        <HeaderTitle>⚖️ {t('submitCaseTitle')}</HeaderTitle>
        <HeaderSubtitle>{attemptsLabel}</HeaderSubtitle>
      </Header>

      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
        <Section>
          <SectionTitle>{t('submitCaseSuspectLabel')}</SectionTitle>
          <Select
            value={suspectId}
            onChange={e => setSuspectId(e.target.value)}
            disabled={isExhausted}
          >
            <option value="">—</option>
            {suspects.map(s => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </Select>
        </Section>

        <Section>
          <SectionTitle>{t('submitCaseEvidenceLabel')}</SectionTitle>
          <CheckboxGrid>
            {assets.map(asset => (
              <CheckboxItem key={asset.id}>
                <input
                  type="checkbox"
                  checked={evidenceIds.includes(asset.id)}
                  onChange={() => toggleEvidence(asset.id)}
                  disabled={isExhausted}
                />
                <span>{asset.title}</span>
              </CheckboxItem>
            ))}
          </CheckboxGrid>
        </Section>

        <Section>
          <SectionTitle>{t('submitCaseAnalysisLabel')}</SectionTitle>
          <TextArea
            placeholder="asset.id:AnalysisType (one per line or comma-separated)"
            value={analysisText}
            onChange={e => setAnalysisText(e.target.value)}
            disabled={isExhausted}
          />
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
            <BreakdownItem $ok={lastResult.breakdown.culprit}>
              {t('submitCaseBreakdownCulprit')}: {lastResult.breakdown.culprit ? '✓' : '✗'}
            </BreakdownItem>
            <BreakdownItem $ok={lastResult.breakdown.evidence}>
              {t('submitCaseBreakdownEvidence')}: {lastResult.breakdown.evidence ? '✓' : '✗'}
            </BreakdownItem>
            <BreakdownItem $ok={lastResult.breakdown.analysis}>
              {t('submitCaseBreakdownAnalysis')}: {lastResult.breakdown.analysis ? '✓' : '✗'}
            </BreakdownItem>
            <BreakdownItem $ok={lastResult.breakdown.questions}>
              {t('submitCaseBreakdownQuestions')}: {lastResult.breakdown.questions ? '✓' : '✗'}
            </BreakdownItem>
          </BreakdownGrid>
          <ScoreLine>
            {t('submitCaseAttemptsRemaining').replace('{n}', String(lastResult.attemptsRemaining))}
          </ScoreLine>
          {lastResult.feedbackText && (
            <ScoreLine style={{ marginTop: '0.25rem' }}>{lastResult.feedbackText}</ScoreLine>
          )}
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
