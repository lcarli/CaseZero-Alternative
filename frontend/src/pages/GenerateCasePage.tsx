import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import styled from 'styled-components'
import { useLanguage } from '../hooks/useLanguageContext'
import { caseGenerationApi } from '../services/api'
import type { GenerateCaseRequest } from '../services/api'
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
`

const FormGrid = styled.div`
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.5rem;
  margin-bottom: 2rem;
  
  @media (max-width: 768px) {
    grid-template-columns: 1fr;
    gap: 1rem;
  }
`

const FormField = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  
  &.full-width {
    grid-column: 1 / -1;
  }
`

const Label = styled.label`
  color: rgba(52, 152, 219, 0.9);
  font-weight: 500;
  font-size: 0.9rem;
`

const Input = styled.input`
  padding: 0.75rem;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.3);
  border-radius: 0.5rem;
  color: white;
  font-size: 0.9rem;
  transition: all 0.3s ease;
  
  &:focus {
    outline: none;
    border-color: rgba(52, 152, 219, 0.7);
    background: rgba(255, 255, 255, 0.15);
  }
  
  &::placeholder {
    color: rgba(255, 255, 255, 0.5);
  }
`

const TextArea = styled.textarea`
  padding: 0.75rem;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.3);
  border-radius: 0.5rem;
  color: white;
  font-size: 0.9rem;
  min-height: 100px;
  resize: vertical;
  font-family: inherit;
  transition: all 0.3s ease;
  
  &:focus {
    outline: none;
    border-color: rgba(52, 152, 219, 0.7);
    background: rgba(255, 255, 255, 0.15);
  }
  
  &::placeholder {
    color: rgba(255, 255, 255, 0.5);
  }
`

const Select = styled.select`
  padding: 0.75rem;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.3);
  border-radius: 0.5rem;
  color: white;
  font-size: 0.9rem;
  transition: all 0.3s ease;
  
  &:focus {
    outline: none;
    border-color: rgba(52, 152, 219, 0.7);
    background: rgba(255, 255, 255, 0.15);
  }
  
  option {
    background: #1a2140;
    color: white;
  }
`

const CheckboxField = styled.div`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  
  input[type="checkbox"] {
    width: 18px;
    height: 18px;
    accent-color: rgba(52, 152, 219, 0.9);
  }
`

const Button = styled.button`
  padding: 0.75rem 2rem;
  background: linear-gradient(135deg, #3498db, #2980b9);
  border: none;
  border-radius: 0.5rem;
  color: white;
  cursor: pointer;
  font-size: 1rem;
  font-weight: 500;
  transition: all 0.3s ease;
  width: 100%;
  
  &:hover:not(:disabled) {
    transform: translateY(-2px);
    box-shadow: 0 4px 15px rgba(52, 152, 219, 0.3);
  }
  
  &:disabled {
    opacity: 0.6;
    cursor: not-allowed;
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

  const [formData, setFormData] = useState<GenerateCaseRequest>({
    title: '',
    location: '',
    incidentDateTime: '',
    pitch: '',
    twist: '',
    difficulty: 'Medium',
    targetDurationMinutes: 60,
    constraints: '',
    timezone: 'America/Toronto',
    generateImages: true
  })

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) => {
    const { name, value, type } = e.target
    
    setFormData(prev => ({
      ...prev,
      [name]: type === 'checkbox' ? (e.target as HTMLInputElement).checked : 
              name === 'targetDurationMinutes' ? parseInt(value) || 0 : 
              value
    }))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    setSuccessMessage('')

    // Validate required fields
    if (!formData.title || !formData.location || !formData.incidentDateTime || !formData.pitch || !formData.twist) {
      setError(t('fillRequiredFields'))
      return
    }

    setIsLoading(true)

    try {
      await caseGenerationApi.generateCase(formData)
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
          {t('caseGenerationDesc')}
        </FormDescription>

        {error && <ErrorMessage>{error}</ErrorMessage>}
        {successMessage && <SuccessMessage>{successMessage}</SuccessMessage>}

        <form onSubmit={handleSubmit}>
          <FormGrid>
            <FormField>
              <Label>{t('caseTitle')} *</Label>
              <Input
                type="text"
                name="title"
                value={formData.title}
                onChange={handleInputChange}
                placeholder={t('caseTitle')}
                required
              />
            </FormField>

            <FormField>
              <Label>{t('caseLocation')} *</Label>
              <Input
                type="text"
                name="location"
                value={formData.location}
                onChange={handleInputChange}
                placeholder={t('caseLocation')}
                required
              />
            </FormField>

            <FormField>
              <Label>{t('incidentDateTime')} *</Label>
              <Input
                type="datetime-local"
                name="incidentDateTime"
                value={formData.incidentDateTime}
                onChange={handleInputChange}
                required
              />
            </FormField>

            <FormField>
              <Label>{t('caseDifficulty')}</Label>
              <Select
                name="difficulty"
                value={formData.difficulty}
                onChange={handleInputChange}
              >
                <option value="Easy">{t('easy')}</option>
                <option value="Medium">{t('medium')}</option>
                <option value="Hard">{t('hard')}</option>
              </Select>
            </FormField>

            <FormField>
              <Label>{t('targetDuration')}</Label>
              <Input
                type="number"
                name="targetDurationMinutes"
                value={formData.targetDurationMinutes}
                onChange={handleInputChange}
                min="30"
                max="240"
                step="15"
              />
            </FormField>

            <FormField>
              <Label>{t('timezone')}</Label>
              <Input
                type="text"
                name="timezone"
                value={formData.timezone}
                onChange={handleInputChange}
                placeholder="America/Toronto"
              />
            </FormField>

            <FormField className="full-width">
              <Label>{t('casePitch')} *</Label>
              <TextArea
                name="pitch"
                value={formData.pitch}
                onChange={handleInputChange}
                placeholder={t('casePitch')}
                required
              />
            </FormField>

            <FormField className="full-width">
              <Label>{t('caseTwist')} *</Label>
              <TextArea
                name="twist"
                value={formData.twist}
                onChange={handleInputChange}
                placeholder={t('caseTwist')}
                required
              />
            </FormField>

            <FormField className="full-width">
              <Label>{t('constraints')}</Label>
              <TextArea
                name="constraints"
                value={formData.constraints}
                onChange={handleInputChange}
                placeholder={t('constraints')}
              />
            </FormField>

            <FormField className="full-width">
              <CheckboxField>
                <input
                  type="checkbox"
                  name="generateImages"
                  checked={formData.generateImages}
                  onChange={handleInputChange}
                />
                <Label>{t('generateImages')}</Label>
              </CheckboxField>
            </FormField>
          </FormGrid>

          <Button type="submit" disabled={isLoading}>
            {isLoading ? t('generatingCase') : t('generateCaseBtn')}
          </Button>
        </form>
      </FormContainer>
    </PageContainer>
  )
}

export default GenerateCasePage