import { useParams, useNavigate } from 'react-router-dom'
import { useEffect, useState } from 'react'
import Desktop from '../components/Desktop'
import { CaseProvider } from '../contexts/CaseContext'
import { TimeProvider } from '../contexts/TimeContext'
import { TimeSync } from '../components/TimeSync'
import { caseSessionApi, type GameClockSnapshot } from '../services/api'
import { useAuth } from '../hooks/useAuthContext'
import { useLanguage } from '../hooks/useLanguageContext'

const DesktopPage = () => {
  const { caseId } = useParams()
  const navigate = useNavigate()
  const { isAuthenticated } = useAuth()
  const { t } = useLanguage()
  const [initialGameClock, setInitialGameClock] = useState<GameClockSnapshot>()
  const [isLoadingSession, setIsLoadingSession] = useState(true)

  // If no caseId in URL, redirect to dashboard
  useEffect(() => {
    if (!caseId) {
      navigate('/dashboard', { replace: true })
    }
  }, [caseId, navigate])

  useEffect(() => {
    let alive = true
    const initializeSession = async () => {
      if (isAuthenticated && caseId) {
        try {
          setIsLoadingSession(true)

          const session = await caseSessionApi.startSession({ caseId })
          if (!session.gameClock) throw new Error('Missing authoritative session clock')
          if (alive) setInitialGameClock(session.gameClock)
        } catch (error) {
          console.error('Failed to initialize session:', error)
          // If session start fails, redirect back to dashboard
          if (alive) navigate('/dashboard', { replace: true })
          return
        } finally {
          if (alive) setIsLoadingSession(false)
        }
      }
    }

    void initializeSession()
    return () => { alive = false }
  }, [caseId, isAuthenticated, navigate])

  if (!caseId) {
    return null
  }

  // Wait for session to load before rendering
  if (isLoadingSession) {
    return (
      <div style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        height: '100vh',
        background: 'linear-gradient(135deg, #0a0f23 0%, #1a2140 50%, #0a0f23 100%)',
        color: 'white',
        fontSize: '18px'
      }}>
        {t('loadingCaseSession')}
      </div>
    )
  }

  return (
    <CaseProvider caseId={caseId}>
      <TimeProvider key={caseId} caseId={caseId} initialGameClock={initialGameClock}>
        <TimeSync>
          <Desktop />
        </TimeSync>
      </TimeProvider>
    </CaseProvider>
  )
}

export default DesktopPage