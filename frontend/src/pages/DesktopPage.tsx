import { useParams } from 'react-router-dom'
import { useEffect, useState } from 'react'
import Desktop from '../components/Desktop'
import { CaseProvider } from '../contexts/CaseContext'
import { TimeProvider } from '../contexts/TimeContext'
import { TimeSync } from '../components/TimeSync'
import { caseSessionApi } from '../services/api'
import { useAuth } from '../hooks/useAuthContext'

const DesktopPage = () => {
  const { caseId } = useParams()
  const { isAuthenticated } = useAuth()
  const [initialGameTime, setInitialGameTime] = useState<Date | undefined>(undefined)
  const [isLoadingSession, setIsLoadingSession] = useState(true)
  
  // Default to CASE-2024-001 if no caseId in URL
  const activeCaseId = caseId || 'CASE-2024-001'
  
  console.log('Loading desktop for case:', activeCaseId)

  // Load last session and start new session when entering case
  useEffect(() => {
    const initializeSession = async () => {
      if (isAuthenticated && activeCaseId) {
        try {
          setIsLoadingSession(true)
          
          // Try to get last session to resume from
          try {
            const lastSession = await caseSessionApi.getLastSession(activeCaseId)
            
            if (lastSession.gameTimeAtEnd) {
              // Resume from where user left off
              const resumeTime = new Date(lastSession.gameTimeAtEnd)
              setInitialGameTime(resumeTime)
              console.log('Resuming from previous session at:', resumeTime)
            }
          } catch (error) {
            // No previous session found - will use default 8:00 AM
            console.log('No previous session found, starting fresh')
          }
          
          // Start new session
          const startTime = initialGameTime || (() => {
            const defaultTime = new Date()
            defaultTime.setHours(8, 0, 0, 0)
            return defaultTime
          })()
          
          await caseSessionApi.startSession({
            caseId: activeCaseId,
            gameTimeAtStart: startTime.toISOString()
          })
          console.log('Session started for case:', activeCaseId)
        } catch (error) {
          console.error('Failed to initialize session:', error)
          // Continue even if session initialization fails
        } finally {
          setIsLoadingSession(false)
        }
      }
    }

    initializeSession()
  }, [activeCaseId, isAuthenticated])

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
    <CaseProvider caseId={activeCaseId}>
      <TimeProvider caseId={activeCaseId} initialGameTime={initialGameTime}>
        <TimeSync>
          <Desktop />
        </TimeSync>
      </TimeProvider>
    </CaseProvider>
  )
}

export default DesktopPage