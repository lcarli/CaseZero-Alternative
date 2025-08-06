import { useParams } from 'react-router-dom'
import Desktop from '../components/Desktop'
import { CaseProvider } from '../contexts/CaseContext'

const DesktopPage = () => {
  const { caseId } = useParams()
  
  // Default to CASE-2024-001 if no caseId in URL
  const activeCaseId = caseId || 'CASE-2024-001'
  
  console.log('Loading desktop for case:', activeCaseId)
  
  return (
    <CaseProvider caseId={activeCaseId}>
      <Desktop />
    </CaseProvider>
  )
}

export default DesktopPage