import { useParams, useNavigate } from 'react-router-dom'
import { useEffect, useState } from 'react'
import Desktop from '../components/Desktop'
import { CaseProvider } from '../contexts/CaseContext'
import { TimeProvider } from '../contexts/TimeContext'
import { TimeSync } from '../components/TimeSync'
import { caseSessionApi } from '../services/api'
import { useAuth } from '../hooks/useAuthContext'

const DesktopPage = () => {
  const { caseId } = useParams()
  const navigate = useNavigate()
  const { isAuthenticated } = useAuth()
  const [initialGameTime, setInitialGameTime] = useState<Date | undefined>(undefined)
  const [isLoadingSession, setIsLoadingSession] = useState(true)

  // If no caseId in URL, redirect to dashboard
  useEffect(() => {
    if (!caseId) {
      navigate('/dashboard', { replace: true })
    }
  }, [caseId, navigate])

  // Load last session and start new session when entering case
  useEffect(() => {
    const initializeSession = async () => {
      if (isAuthenticated && caseId) {
        try {
          setIsLoadingSession(true)

          // Try to get last session to resume from
          try {
            const lastSession = await caseSessionApi.getLastSession(caseId)

            if (lastSession.gameTimeAtEnd) {
              const resumeTime = new Date(lastSession.gameTimeAtEnd)
              setInitialGameTime(resumeTime)
              console.log('Resuming from previous session at:', resumeTime)
            }
          } catch {
            console.log('No previous session found, starting fresh')
          }

          const startTime = initialGameTime || (() => {
            const defaultTime = new Date()
            defaultTime.setHours(8, 0, 0, 0)
            return defaultTime
          })()

          await caseSessionApi.startSession({
            caseId,
            gameTimeAtStart: startTime.toISOString()
          })
          console.log('Session started for case:', caseId)
        } catch (error) {
          console.error('Failed to initialize session:', error)
          // If session start fails, redirect back to dashboard
          navigate('/dashboard', { replace: true })
          return
        } finally {
          setIsLoadingSession(false)
        }
      }
    }

    initializeSession()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [caseId, isAuthenticated])

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
        Loading case session...
      </div>
    )
  }

  return (
    <CaseProvider caseId={caseId}>
      <TimeProvider caseId={caseId} initialGameTime={initialGameTime}>
        <TimeSync>
          <Desktop />
        </TimeSync>
      </TimeProvider>
    </CaseProvider>
  )
}

export default DesktopPage