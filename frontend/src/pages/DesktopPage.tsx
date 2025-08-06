import { useParams } from 'react-router-dom'
import Desktop from '../components/Desktop'

const DesktopPage = () => {
  const { caseId } = useParams()
  
  // TODO: Use caseId to load specific case data when backend is implemented
  console.log('Loading desktop for case:', caseId)
  
  return <Desktop />
}

export default DesktopPage