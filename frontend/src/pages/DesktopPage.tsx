import { useParams } from 'react-router-dom'
import { useEffect } from 'react'
import Desktop from '../components/Desktop'
import { CaseProvider } from '../contexts/CaseContext'
import { TimeProvider } from '../contexts/TimeContext'
import { caseSessionApi } from '../services/api'
import { useAuth } from '../hooks/useAuthContext'

const DesktopPage = () => {
  const { caseId } = useParams()
  const { isAuthenticated } = useAuth()
  
  // Default to CASE-2024-001 if no caseId in URL
  const activeCaseId = caseId || 'CASE-2024-001'
  
  console.log('Loading desktop for case:', activeCaseId)

  // Start session when entering case
  useEffect(() => {
    const startSession = async () => {
      if (isAuthenticated && activeCaseId) {
        try {
          const startTime = new Date()
          startTime.setHours(8, 0, 0, 0) // Set to 8 AM as game start time
          
          await caseSessionApi.startSession({
            caseId: activeCaseId,
            gameTimeAtStart: startTime.toISOString()
          })
          console.log('Session started for case:', activeCaseId)
        } catch (error) {
          console.error('Failed to start session:', error)
          // Continue even if session start fails
        }
      }
    }

    startSession()
  }, [activeCaseId, isAuthenticated])
  
  return (
    <CaseProvider caseId={activeCaseId}>
      <TimeProvider caseId={activeCaseId}>
        <Desktop />
      </TimeProvider>
    </CaseProvider>
  )
}

export default DesktopPage