import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import styled from 'styled-components'
import { useLanguage } from '../hooks/useLanguageContext'
import { caseGenerationApi } from '../services/api'
import type { SimpleCaseRequest } from '../services/api'
import LanguageSelector from '../components/LanguageSelector'

const PageContainer = styled.div`
  min-height: 100vh;
  background: linear-gradient(135deg, #0a0f23 0%, #1a2140 25%, #2a3458 50%, #1a2140 75%, #0a0f23 100%);
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  color: white;
  padding: 1rem;
  box-sizing: border-box;
  
  @media (max-width: 768px) {
    padding: 0.5rem;
  }
`

const Header = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.5rem;
  padding: 1rem;
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 1rem;
  backdrop-filter: blur(10px);
  
  @media (max-width: 768px) {
    flex-direction: column;
    gap: 1rem;
    padding: 0.75rem;
    margin-bottom: 1rem;
  }
`

const Title = styled.h1`
  margin: 0;
  font-size: 1.8rem;
  color: white;
  
  @media (max-width: 768px) {
    font-size: 1.4rem;
    text-align: center;
  }
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

const FormContainer = styled.div`
  max-width: 800px;
  margin: 0 auto;
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 1rem;
  padding: 2rem;
  backdrop-filter: blur(10px);
  
  @media (max-width: 768px) {
    padding: 1rem;
    border-radius: 0.5rem;
  }
`

const FormDescription = styled.p`
  color: rgba(255, 255, 255, 0.8);
  margin-bottom: 2rem;
  line-height: 1.6;
  text-align: center;
  font-size: 1.1rem;
`

const FormGroup = styled.div`
  margin-bottom: 2rem;
`

const FormField = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.8rem;
  margin-bottom: 1.5rem;
`

const Label = styled.label`
  color: rgba(52, 152, 219, 0.9);
  font-weight: 500;
  font-size: 1rem;
`

const HelpText = styled.p`
  color: rgba(255, 255, 255, 0.6);
  font-size: 0.9rem;
  margin: 0;
  line-height: 1.4;
`

const DifficultyOption = styled.div`
  padding: 1rem;
  margin: 0.5rem 0;
  background: rgba(255, 255, 255, 0.1);
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-radius: 0.75rem;
  cursor: pointer;
  transition: all 0.3s ease;
  
  &:hover {
    background: rgba(255, 255, 255, 0.15);
    border-color: rgba(52, 152, 219, 0.5);
  }
  
  &.selected {
    background: rgba(52, 152, 219, 0.2);
    border-color: rgba(52, 152, 219, 0.8);
  }
`

const DifficultyTitle = styled.h3`
  margin: 0 0 0.5rem 0;
  color: white;
  font-size: 1.1rem;
`

const DifficultyDescription = styled.p`
  margin: 0;
  color: rgba(255, 255, 255, 0.7);
  font-size: 0.9rem;
  line-height: 1.4;
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
  margin-top: 1rem;
  
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

const ErrorMessage = styled.div`
  color: rgba(231, 76, 60, 0.9);
  background: rgba(231, 76, 60, 0.1);
  border: 1px solid rgba(231, 76, 60, 0.3);
  border-radius: 0.5rem;
  padding: 0.75rem;
  margin-bottom: 1rem;
  text-align: center;
`

const SuccessMessage = styled.div`
  color: rgba(46, 204, 113, 0.9);
  background: rgba(46, 204, 113, 0.1);
  border: 1px solid rgba(46, 204, 113, 0.3);
  border-radius: 0.5rem;
  padding: 0.75rem;
  margin-bottom: 1rem;
  text-align: center;
`

const GenerateCasePage = () => {
  const navigate = useNavigate()
  const { t } = useLanguage()
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')
  const [successMessage, setSuccessMessage] = useState('')
  const [selectedDifficulty, setSelectedDifficulty] = useState<string>('Medium')

  const difficultyOptions = [
    {
      value: 'Easy',
      title: t('easy'),
      description: 'Perfect for beginners. Clear evidence, straightforward investigation. Duration: ~45 minutes.'
    },
    {
      value: 'Medium', 
      title: t('medium'),
      description: 'Balanced complexity. Multiple suspects, some red herrings. Duration: ~75 minutes.'
    },
    {
      value: 'Hard',
      title: t('hard'),
      description: 'Advanced investigation. Complex motives, scarce evidence. Duration: ~120 minutes.'
    }
  ]

  const handleDifficultySelect = (difficulty: string) => {
    setSelectedDifficulty(difficulty)
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    setSuccessMessage('')

    if (!selectedDifficulty) {
      setError(t('fillRequiredFields'))
      return
    }

    setIsLoading(true)

    try {
      const request: SimpleCaseRequest = {
        difficulty: selectedDifficulty
      }
      
      await caseGenerationApi.generateSimpleCase(request)
      setSuccessMessage(t('caseGeneratedSuccess'))
      
      // After successful generation, redirect back to dashboard
      setTimeout(() => {
        navigate('/dashboard')
      }, 2000)
    } catch (err: any) {
      setError(err.message || t('caseGenerationError'))
    } finally {
      setIsLoading(false)
    }
  }

  const handleBack = () => {
    navigate('/dashboard')
  }

  return (
    <PageContainer>
      <Header>
        <Title>{t('caseGeneration')}</Title>
        <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
          <LanguageSelector />
          <BackButton onClick={handleBack}>
            {t('backToDashboard')}
          </BackButton>
        </div>
      </Header>

      <FormContainer>
        <FormDescription>
          {t('caseGenerationSimpleDesc')}
        </FormDescription>

        {error && <ErrorMessage>{error}</ErrorMessage>}
        {successMessage && <SuccessMessage>{successMessage}</SuccessMessage>}

        <form onSubmit={handleSubmit}>
          <FormGroup>
            <FormField>
              <Label>{t('selectDifficulty')}</Label>
              <HelpText>{t('difficultyHelpText')}</HelpText>
              
              {difficultyOptions.map((option) => (
                <DifficultyOption 
                  key={option.value}
                  className={selectedDifficulty === option.value ? 'selected' : ''}
                  onClick={() => handleDifficultySelect(option.value)}
                >
                  <DifficultyTitle>{option.title}</DifficultyTitle>
                  <DifficultyDescription>{option.description}</DifficultyDescription>
                </DifficultyOption>
              ))}
            </FormField>
          </FormGroup>

          <Button type="submit" disabled={isLoading}>
            {isLoading ? t('generatingCase') : t('generateCaseBtn')}
          </Button>
        </form>
      </FormContainer>
    </PageContainer>
  )
}

export default GenerateCasePage