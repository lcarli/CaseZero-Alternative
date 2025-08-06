import { useParams } from 'react-router-dom'
import Desktop from '../components/Desktop'
import { CaseProvider } from '../contexts/CaseContext'
import { TimeProvider } from '../contexts/TimeContext'

const DesktopPage = () => {
  const { caseId } = useParams()
  
  // Default to CASE-2024-001 if no caseId in URL
  const activeCaseId = caseId || 'CASE-2024-001'
  
  console.log('Loading desktop for case:', activeCaseId)
  
  return (
    <CaseProvider caseId={activeCaseId}>
      <TimeProvider caseId={activeCaseId}>
        <Desktop />
      </TimeProvider>
    </CaseProvider>
  )
}

export default DesktopPage