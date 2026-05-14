import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import styled from 'styled-components'
import { useLanguage } from '../hooks/useLanguageContext'
import LanguageSelector from '../components/LanguageSelector'

// Types for the case generation
interface CaseGenerationRequest {
  title: string
  location: string
  difficulty: string
  targetDurationMinutes: number
  generateImages: boolean
  constraints: string[]
  timezone: string
}

interface CaseGenerationStatus {
  caseId: string
  status: string
  currentStep: string
  completedSteps: string[]
  totalSteps: number
  progress: number
  startTime: string
  estimatedCompletion?: string
  error?: string
  output?: {
    bundlePath: string
    caseId: string
    files: Array<{
      path: string
      type: string
      size: number
      createdAt: string
    }>
  }
}

const PageContainer = styled.div`
  min-height: 100vh;
  background: linear-gradient(135deg, #0a0f23 0%, #1a2140 25%, #2a3458 50%, #1a2140 75%, #0a0f23 100%);
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  color: white;
  padding: 1rem;
  box-sizing: border-box;
`

const Header = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
  padding: 1rem;
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 1rem;
  backdrop-filter: blur(10px);
`

const Title = styled.h1`
  margin: 0;
  font-size: 2rem;
  background: linear-gradient(135deg, #3498db, #2980b9);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
`

const BackButton = styled.button`
  padding: 0.5rem 1rem;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.3);
  border-radius: 0.5rem;
  color: white;
  cursor: pointer;
  font-size: 0.9rem;
  transition: all 0.3s ease;
  
  &:hover {
    background: rgba(255, 255, 255, 0.2);
  }
`

const ContentContainer = styled.div`
  max-width: 1200px;
  margin: 0 auto;
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 2rem;
  
  @media (max-width: 768px) {
    grid-template-columns: 1fr;
    gap: 1rem;
  }
`

const Card = styled.div`
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 1rem;
  padding: 1.5rem;
  backdrop-filter: blur(10px);
`

const CardTitle = styled.h2`
  margin: 0 0 1rem 0;
  color: white;
  font-size: 1.3rem;
`

const FormGroup = styled.div`
  margin-bottom: 1.5rem;
`

const Label = styled.label`
  display: block;
  margin-bottom: 0.5rem;
  color: rgba(255, 255, 255, 0.9);
  font-weight: 500;
`

const Select = styled.select`
  width: 100%;
  padding: 0.75rem;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.3);
  border-radius: 0.5rem;
  color: white;
  font-size: 1rem;
  
  &:focus {
    outline: none;
    border-color: rgba(52, 152, 219, 0.7);
    box-shadow: 0 0 0 2px rgba(52, 152, 219, 0.2);
  }
  
  option {
    background: #1a2140;
    color: white;
  }
`

const Input = styled.input`
  width: 100%;
  padding: 0.75rem;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.3);
  border-radius: 0.5rem;
  color: white;
  font-size: 1rem;
  
  &:focus {
    outline: none;
    border-color: rgba(52, 152, 219, 0.7);
    box-shadow: 0 0 0 2px rgba(52, 152, 219, 0.2);
  }
  
  &::placeholder {
    color: rgba(255, 255, 255, 0.5);
  }
`

const Checkbox = styled.input`
  margin-right: 0.5rem;
  transform: scale(1.2);
`

const Button = styled.button`
  padding: 1rem 2rem;
  background: linear-gradient(135deg, #3498db, #2980b9);
  border: none;
  border-radius: 0.75rem;
  color: white;
  cursor: pointer;
  font-size: 1.1rem;
  font-weight: 600;
  transition: all 0.3s ease;
  width: 100%;
  
  &:hover:not(:disabled) {
    transform: translateY(-2px);
    box-shadow: 0 6px 20px rgba(52, 152, 219, 0.4);
  }
  
  &:disabled {
    opacity: 0.6;
    cursor: not-allowed;
    transform: none;
  }
`

const ProgressCard = styled(Card)`
  ${props => props.hidden && 'display: none;'}
`

const ProgressBar = styled.div`
  background: rgba(255, 255, 255, 0.1);
  border-radius: 0.5rem;
  height: 0.5rem;
  overflow: hidden;
  margin: 1rem 0;
`

const ProgressFill = styled.div<{ progress: number }>`
  background: linear-gradient(135deg, #3498db, #2980b9);
  height: 100%;
  width: ${props => props.progress}%;
  transition: width 0.5s ease;
`

const StepList = styled.ul`
  list-style: none;
  padding: 0;
  margin: 1rem 0;
`

const StepItem = styled.li<{ completed: boolean; current: boolean }>`
  padding: 0.5rem 0;
  display: flex;
  align-items: center;
  color: ${props => 
    props.completed ? 'rgba(46, 204, 113, 0.9)' : 
    props.current ? 'rgba(52, 152, 219, 0.9)' : 
    'rgba(255, 255, 255, 0.6)'
  };
  
  &::before {
    content: ${props => 
      props.completed ? '"✓"' : 
      props.current ? '"🔄"' : 
      '"○"'
    };
    margin-right: 0.5rem;
    font-weight: bold;
  }
`

const ErrorMessage = styled.div`
  color: rgba(231, 76, 60, 0.9);
  background: rgba(231, 76, 60, 0.1);
  border: 1px solid rgba(231, 76, 60, 0.3);
  border-radius: 0.5rem;
  padding: 1rem;
  margin: 1rem 0;
`

const SuccessMessage = styled.div`
  color: rgba(46, 204, 113, 0.9);
  background: rgba(46, 204, 113, 0.1);
  border: 1px solid rgba(46, 204, 113, 0.3);
  border-radius: 0.5rem;
  padding: 1rem;
  margin: 1rem 0;
`

const CaseGeneratorAIPage = () => {
  const navigate = useNavigate()
  const { t } = useLanguage()
  
  const [request, setRequest] = useState<CaseGenerationRequest>({
    title: '',
    location: '',
    difficulty: 'Iniciante',
    targetDurationMinutes: 60,
    generateImages: true,
    constraints: [],
    timezone: 'America/Sao_Paulo'
  })
  
  const [isGenerating, setIsGenerating] = useState(false)
  const [status, setStatus] = useState<CaseGenerationStatus | null>(null)
  const [error, setError] = useState('')

  // Mock function to simulate API call (replace with actual API call)
  const startGeneration = async () => {
    setIsGenerating(true)
    setError('')
    
    try {
      // This would call the Azure Function
      // const response = await fetch('/api/StartCaseGeneration', {
      //   method: 'POST',
      //   headers: { 'Content-Type': 'application/json' },
      //   body: JSON.stringify(request)
      // })
      
      // Mock implementation
      const mockInstanceId = `instance-${Date.now()}`
      
      // Start polling for status
      pollStatus(mockInstanceId)
    } catch (err) {
      setError(t('caseGenerationError'))
      setIsGenerating(false)
    }
  }

  const pollStatus = async (_instanceId: string) => {
    const steps = ['Plan', 'Expand', 'Design', 'GenDocs', 'GenMedia', 'Normalize', 'Index', 'RuleValidate', 'RedTeam', 'Package']
    let currentStepIndex = 0
    
    const poll = setInterval(() => {
      if (currentStepIndex < steps.length) {
        const mockStatus: CaseGenerationStatus = {
          caseId: `CASE-${Date.now()}`,
          status: 'Running',
          currentStep: steps[currentStepIndex],
          completedSteps: steps.slice(0, currentStepIndex),
          totalSteps: steps.length,
          progress: (currentStepIndex / steps.length) * 100,
          startTime: new Date().toISOString()
        }
        setStatus(mockStatus)
        currentStepIndex++
      } else {
        // Complete
        const finalStatus: CaseGenerationStatus = {
          caseId: `CASE-${Date.now()}`,
          status: 'Completed',
          currentStep: 'Completed',
          completedSteps: steps,
          totalSteps: steps.length,
          progress: 100,
          startTime: new Date().toISOString(),
          estimatedCompletion: new Date().toISOString(),
          output: {
            bundlePath: 'cases/CASE-123456',
            caseId: 'CASE-123456',
            files: [
              { path: 'case.json', type: 'json', size: 2048, createdAt: new Date().toISOString() },
              { path: 'metadata.json', type: 'json', size: 512, createdAt: new Date().toISOString() }
            ]
          }
        }
        setStatus(finalStatus)
        setIsGenerating(false)
        clearInterval(poll)
      }
    }, 3000) // Update every 3 seconds for demo
  }

  const getStepName = (step: string) => {
    switch (step) {
      case 'Plan': return t('stepPlan')
      case 'Expand': return t('stepExpand')
      case 'Design': return t('stepDesign')
      case 'GenDocs': return t('stepGenDocs')
      case 'GenMedia': return t('stepGenMedia')
      case 'Normalize': return t('stepNormalize')
      case 'Index': return t('stepIndex')
      case 'RuleValidate': return t('stepRuleValidate')
      case 'RedTeam': return t('stepRedTeam')
      case 'Package': return t('stepPackage')
      default: return step
    }
  }

  const handleBack = () => {
    navigate('/dashboard')
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!request.title.trim() || !request.location.trim()) {
      setError(t('fillRequiredFields'))
      return
    }
    startGeneration()
  }

  return (
    <PageContainer>
      <Header>
        <Title>🤖 {t('caseGeneratorTitle')}</Title>
        <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
          <LanguageSelector />
          <BackButton onClick={handleBack}>
            {t('backToDashboard')}
          </BackButton>
        </div>
      </Header>

      <ContentContainer>
        <Card>
          <CardTitle>{t('caseGeneratorTitle')}</CardTitle>
          <p style={{ color: 'rgba(255, 255, 255, 0.8)', marginBottom: '1.5rem' }}>
            {t('caseGeneratorDescription')}
          </p>

          {error && <ErrorMessage>{error}</ErrorMessage>}

          <form onSubmit={handleSubmit}>
            <FormGroup>
              <Label htmlFor="title">{t('caseGeneration')} *</Label>
              <Input
                id="title"
                type="text"
                value={request.title}
                onChange={(e) => setRequest(prev => ({ ...prev, title: e.target.value }))}
                placeholder="Ex: Roubo na Empresa de Tecnologia"
                disabled={isGenerating}
              />
            </FormGroup>

            <FormGroup>
              <Label htmlFor="location">{t('caseLocation')} *</Label>
              <Input
                id="location"
                type="text"
                value={request.location}
                onChange={(e) => setRequest(prev => ({ ...prev, location: e.target.value }))}
                placeholder="Ex: São Paulo, SP"
                disabled={isGenerating}
              />
            </FormGroup>

            <FormGroup>
              <Label htmlFor="difficulty">{t('caseDifficulty')}</Label>
              <Select
                id="difficulty"
                value={request.difficulty}
                onChange={(e) => setRequest(prev => ({ ...prev, difficulty: e.target.value }))}
                disabled={isGenerating}
              >
                <option value="Iniciante">{t('easy')}</option>
                <option value="Médio">{t('medium')}</option>
                <option value="Difícil">{t('hard')}</option>
              </Select>
            </FormGroup>

            <FormGroup>
              <Label htmlFor="duration">{t('targetDuration')}</Label>
              <Input
                id="duration"
                type="number"
                min="15"
                max="240"
                value={request.targetDurationMinutes}
                onChange={(e) => setRequest(prev => ({ ...prev, targetDurationMinutes: parseInt(e.target.value) || 60 }))}
                disabled={isGenerating}
              />
            </FormGroup>

            <FormGroup>
              <Label>
                <Checkbox
                  type="checkbox"
                  checked={request.generateImages}
                  onChange={(e) => setRequest(prev => ({ ...prev, generateImages: e.target.checked }))}
                  disabled={isGenerating}
                />
                {t('generateImages')}
              </Label>
            </FormGroup>

            <Button type="submit" disabled={isGenerating}>
              {isGenerating ? t('generatingCase') : t('startGeneration')}
            </Button>
          </form>
        </Card>

        <ProgressCard hidden={!status}>
          <CardTitle>{t('generationProgress')}</CardTitle>
          
          {status && (
            <>
              <div style={{ marginBottom: '1rem' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '0.5rem' }}>
                  <span>{t('currentStep')}: {getStepName(status.currentStep)}</span>
                  <span>{Math.round(status.progress)}%</span>
                </div>
                <ProgressBar>
                  <ProgressFill progress={status.progress} />
                </ProgressBar>
              </div>

              <div style={{ marginBottom: '1rem' }}>
                <strong>Status: </strong>
                <span style={{ 
                  color: status.status === 'Completed' ? 'rgba(46, 204, 113, 0.9)' : 
                        status.status === 'Failed' ? 'rgba(231, 76, 60, 0.9)' : 
                        'rgba(52, 152, 219, 0.9)' 
                }}>
                  {status.status === 'Completed' ? t('completed') : 
                   status.status === 'Failed' ? t('failed') : 
                   t('running')}
                </span>
              </div>

              <StepList>
                {['Plan', 'Expand', 'Design', 'GenDocs', 'GenMedia', 'Normalize', 'Index', 'RuleValidate', 'RedTeam', 'Package'].map((step) => (
                  <StepItem
                    key={step}
                    completed={status.completedSteps.includes(step)}
                    current={status.currentStep === step}
                  >
                    {getStepName(step)}
                  </StepItem>
                ))}
              </StepList>

              {status.error && (
                <ErrorMessage>{status.error}</ErrorMessage>
              )}

              {status.status === 'Completed' && status.output && (
                <SuccessMessage>
                  <div><strong>{t('caseGeneratedSuccess')}</strong></div>
                  <div>Case ID: {status.output.caseId}</div>
                  <div>Files: {status.output.files.length}</div>
                </SuccessMessage>
              )}
            </>
          )}
        </ProgressCard>
      </ContentContainer>
    </PageContainer>
  )
}

export default CaseGeneratorAIPage