import React, { useEffect, useState } from 'react'
import styled from 'styled-components'
import { useCase } from '../../hooks/useCaseContext'
import { useTimeContext } from '../../hooks/useTimeContext'
import { useLanguage } from '../../hooks/useLanguageContext'
import { useWindowContext } from '../../hooks/useWindowContext'
import { forensicsService, analysisLabel, supportsAnalysis, type ForensicRequest } from '../../services/forensicsService'
import { casesApi } from '../../services/api'
import { DocumentViewerWindow } from './DocumentViewerWindow'
import type { FileItem } from './DocumentViewer'
import type { Translations } from '../../types/i18n'

const Container = styled.div`
  height: 100%; overflow-y: auto; padding: 1rem; color: #fff;
`
const Tabs = styled.div`
  display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1rem;
`
const Button = styled.button`
  padding: 0.6rem 1rem; border: 1px solid #4a9eff; border-radius: 6px;
  background: rgba(52, 152, 219, 0.2); color: #fff; cursor: pointer;
  &:disabled { opacity: 0.5; cursor: not-allowed; }
`
const Card = styled.article`
  padding: 1rem; margin-bottom: 1rem; border: 1px solid rgba(74, 158, 255, 0.4);
  border-radius: 8px; background: rgba(0, 0, 0, 0.2);
`
const Form = styled.form`
  display: flex; flex-direction: column; gap: 0.75rem;
  select { padding: 0.6rem; color: #fff; background: #1a1a2e; }
`
const Progress = styled.progress`
  width: 100%; margin: 0.5rem 0;
`
const EmailBody = styled.pre`
  white-space: pre-wrap; font: inherit; line-height: 1.6;
`

const statusLabels: Record<ForensicRequest['status'], keyof Translations> = {
  pending: 'forensicPending', 'in-progress': 'analysisInProgress', completed: 'analysisCompleted',
  cancelled: 'forensicCancelled', failed: 'forensicFailed',
}

const ForensicModule: React.FC<{ queueOnly?: boolean }> = ({ queueOnly = false }) => {
  const { caseId, state, refreshCase, viewAsset, openEmail } = useCase()
  const { gameTime, isRunning, error: clockError } = useTimeContext()
  const { t, currentLanguage } = useLanguage()
  const { openWindow } = useWindowContext()
  const [tab, setTab] = useState<'pending' | 'completed' | 'request'>('pending')
  const [requests, setRequests] = useState<ForensicRequest[]>([])
  const [evidenceId, setEvidenceId] = useState('')
  const [analysisType, setAnalysisType] = useState('')
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selectedEmailId, setSelectedEmailId] = useState<string | null>(null)
  const configs = state.case?.forensicsDefaults?.analysisTypes ?? []
  const evidence = state.visibleAssets.filter(a => configs.some(c => supportsAnalysis(c, a)))
  const selected = evidence.find(a => a.id === evidenceId)
  const analyses = configs.filter(c => selected && supportsAnalysis(c, selected))
  const resultEmail = state.visibleEmails.find(e => e.id === selectedEmailId)

  useEffect(() => {
    let alive = true
    setRequests([])
    setEvidenceId('')
    setAnalysisType('')
    setSelectedEmailId(null)
    setLoading(true)
    setError(null)
    if (!caseId) { setLoading(false); return }
    const load = async () => {
      try {
        const rows = await forensicsService.getForensicRequests(caseId)
        if (alive) { setRequests(rows); setError(null) }
      } catch (err) {
        console.error('Loading forensic requests failed:', err)
        if (alive) setError(t('forensicLoadFailed'))
      } finally {
        if (alive) setLoading(false)
      }
    }
    void load()
    const timer = setInterval(() => { void load() }, 5000)
    return () => { alive = false; clearInterval(timer) }
  }, [caseId, t])

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    if (!caseId || !selected || !analyses.some(a => a.type === analysisType)) return
    setSubmitting(true)
    setError(null)
    try {
      const request = await forensicsService.requestForensicAnalysis(caseId, evidenceId, analysisType)
      setRequests(prev => [request, ...prev.filter(r => r.id !== request.id)])
      setTab(request.status === 'completed' ? 'completed' : 'pending')
      setEvidenceId('')
      setAnalysisType('')
    } catch (err) {
      console.error('Requesting forensic analysis failed:', err)
      setError(t('forensicRequestFailed'))
    } finally { setSubmitting(false) }
  }

  const showDocument = async (request: ForensicRequest) => {
    const asset = state.visibleAssets.find(a => a.id === request.resultDocumentId)
    if (!caseId || !asset) { setError(t('forensicResultUnavailable')); return }
    try {
      await viewAsset(asset.id)
      const fileData: FileItem = {
        id: asset.id, evidenceId: asset.id, name: asset.title, type: 'pdf', icon: '📄',
        size: typeof asset.metadata?.sizeBytes === 'number' ? `${asset.metadata.sizeBytes} B` : '',
        modified: request.completedAt?.toLocaleString(currentLanguage) ?? '',
        content: '', category: asset.category ?? '', mediaUrl: casesApi.getAssetUrl(caseId, asset.id),
      }
      openWindow(`document-${asset.id}`, asset.title, DocumentViewerWindow, { fileData, caseId })
    } catch (err) {
      console.error('Opening forensic document failed:', err)
      setError(t('forensicResultUnavailable'))
    }
  }

  const showEmail = async (request: ForensicRequest) => {
    if (!request.resultEmailId) return
    try {
      await openEmail(request.resultEmailId)
      await refreshCase()
      setSelectedEmailId(request.resultEmailId)
    } catch (err) {
      console.error('Opening forensic email failed:', err)
      setError(t('forensicResultUnavailable'))
    }
  }

  const pending = requests.filter(r => r.status === 'pending' || r.status === 'in-progress')
  const finished = requests.filter(r => r.status !== 'pending' && r.status !== 'in-progress')
  const renderRequest = (request: ForensicRequest) => {
    const remaining = forensicsService.getTimeRemaining(request, gameTime)
    const duration = request.estimatedCompletionTime.getTime() - request.requestedAt.getTime()
    const progress = duration > 0 ? Math.min(100, Math.max(0, (gameTime.getTime() - request.requestedAt.getTime()) / duration * 100)) : 0
    return (
      <Card key={request.id}>
        <h4>{analysisLabel(request.analysisType, t)} — {request.evidenceName}</h4>
        <p>{t(statusLabels[request.status])}</p>
        <p>{t('estimatedCompletion')}: {request.estimatedCompletionTime.toLocaleString(currentLanguage)}</p>
        {(request.status === 'pending' || request.status === 'in-progress') && <>
          <Progress aria-label={t('analysisInProgress')} value={progress} max={100} />
          <p>{remaining > 0 ? t('forensicTimeRemaining').replace('{minutes}', String(remaining)) : t('forensicAwaitingLab')}</p>
        </>}
        {request.status === 'failed' && <p role="alert">{t('forensicConfigurationFailed')}</p>}
        {request.status === 'completed' && <>
          {request.resultDocumentId && <Button onClick={() => { void showDocument(request) }}>{t('forensicOpenReport')}</Button>}
          {' '}
          {request.resultEmailId && <Button onClick={() => { void showEmail(request) }}>{t('forensicOpenEmail')}</Button>}
          {!request.resultDocumentId && !request.resultEmailId && <p>{t('forensicNoResultMaterials')}</p>}
          <p>{t('forensicAnalysisId')}: <code>{request.evidenceId}:{request.analysisType}</code></p>
        </>}
      </Card>
    )
  }

  return (
    <Container>
      <h3>{t('forensicAnalysisTitle')}</h3>
      <p>{t('forensicClockPolicy')}</p>
      {(error || clockError) && <p role="alert">{error ?? clockError}</p>}
      {!caseId && <p>{t('forensicSelectCase')}</p>}
      {loading && <p role="status">{t('forensicLoading')}</p>}
      {!queueOnly && <Tabs>
        <Button onClick={() => setTab('pending')}>{t('analysisInProgress')} ({pending.length})</Button>
        <Button onClick={() => setTab('completed')}>{t('analysisResults')} ({finished.length})</Button>
        <Button onClick={() => setTab('request')}>{t('requestAnalysis')}</Button>
      </Tabs>}
      {(queueOnly || tab === 'pending') && (pending.length ? pending.map(renderRequest) : <p>{t('forensicNoPending')}</p>)}
      {(queueOnly || tab === 'completed') && (finished.length ? finished.map(renderRequest) : <p>{t('forensicNoCompleted')}</p>)}
      {!queueOnly && tab === 'request' && <Form onSubmit={event => { void submit(event) }}>
        <label htmlFor="forensic-evidence">{t('forensicSelectEvidence')}</label>
        <select id="forensic-evidence" value={evidenceId} onChange={event => { setEvidenceId(event.target.value); setAnalysisType('') }}>
          <option value="">{t('forensicChooseEvidence')}</option>
          {evidence.map(a => <option key={a.id} value={a.id}>{a.title}</option>)}
        </select>
        {!evidence.length && <p>{t('forensicNoCompatibleEvidence')}</p>}
        <label htmlFor="forensic-analysis">{t('analysisType')}</label>
        <select id="forensic-analysis" value={analysisType} onChange={event => setAnalysisType(event.target.value)}>
          <option value="">{t('selectAnalysisType')}</option>
          {analyses.map(a => <option key={a.type} value={a.type}>
            {analysisLabel(a.type, t)} — {t('forensicDuration').replace('{minutes}', String(a.durationMinutes))}
          </option>)}
        </select>
        <Button type="submit" disabled={submitting || !isRunning || !selected || !analyses.some(a => a.type === analysisType)}>
          {submitting ? t('forensicSubmitting') : t('requestAnalysis')}
        </Button>
      </Form>}
      {resultEmail && <Card>
        <h4>{resultEmail.subject}</h4>
        <p>{resultEmail.from}</p>
        <EmailBody>{resultEmail.body}</EmailBody>
        <Button onClick={() => setSelectedEmailId(null)}>{t('forensicCloseEmail')}</Button>
      </Card>}
    </Container>
  )
}

export default ForensicModule
