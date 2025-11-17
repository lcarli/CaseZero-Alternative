import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import styled from 'styled-components'
import { useLanguage } from '../hooks/useLanguageContext'
import { authApi, ApiError } from '../services/api'
import { LoadingButton } from '../components/ui/LoadingComponents'
import departmentBadge from '../assets/LogoMetroPolice_transparent.png'

const PageContainer = styled.div`
  min-height: 100vh;
  background: radial-gradient(circle at top, rgba(56, 189, 248, 0.18), transparent 45%),
    radial-gradient(circle at 20% 80%, rgba(59, 130, 246, 0.25), transparent 40%),
    #050914;
  display: flex;
  align-items: center;
  justify-content: center;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  padding: clamp(1rem, 4vh, 2.25rem);
  box-sizing: border-box;
  position: relative;
  overflow: hidden;
  color: #e2e8f0;
`

const BackgroundGrid = styled.div`
  position: absolute;
  inset: 0;
  opacity: 0.25;
  background-image: linear-gradient(90deg, rgba(148, 197, 255, 0.08) 1px, transparent 0),
    linear-gradient(0deg, rgba(148, 197, 255, 0.08) 1px, transparent 0);
  background-size: 120px 120px;
  pointer-events: none;
`

const BackButton = styled(Link)`
  position: absolute;
  top: 1.5rem;
  left: 1.5rem;
  z-index: 3;
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.55rem 1rem;
  border-radius: 999px;
  background: rgba(5, 10, 23, 0.65);
  border: 1px solid rgba(148, 197, 255, 0.35);
  color: rgba(226, 232, 240, 0.95);
  text-decoration: none;
  font-size: 0.9rem;
  letter-spacing: 0.05em;
  transition: transform 0.2s ease, border-color 0.2s ease;

  &:hover {
    transform: translateY(-1px);
    border-color: rgba(96, 165, 250, 0.8);
  }

  @media (max-width: 600px) {
    top: 1rem;
    left: 1rem;
  }
`

const ContentGrid = styled.div`
  position: relative;
  width: 100%;
  max-width: 1050px;
  display: grid;
  grid-template-columns: minmax(320px, 1fr) minmax(320px, 420px);
  gap: clamp(1.5rem, 3vw, 2.25rem);
  z-index: 1;
  align-items: center;
  
  @media (max-width: 1024px) {
    grid-template-columns: 1fr;
  }
`

const BrandPanel = styled.section`
  background: rgba(6, 12, 28, 0.85);
  border: 1px solid rgba(59, 130, 246, 0.25);
  border-radius: 1.5rem;
  padding: clamp(1.5rem, 3vw, 2.25rem);
  backdrop-filter: blur(18px);
  box-shadow: 0 30px 80px rgba(2, 6, 23, 0.6);
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  position: relative;
  overflow: hidden;
  isolation: isolate;
  
  &::after {
    content: '';
    position: absolute;
    inset: 0;
    background: radial-gradient(circle at 20% -10%, rgba(59, 130, 246, 0.25), transparent 45%);
    z-index: -1;
  }
`

const BrandHeader = styled.div`
  display: flex;
  flex-direction: column;
  gap: 1rem;
`

const DepartmentRow = styled.div`
  display: flex;
  align-items: center;
  gap: 1rem;
`

const DepartmentBadge = styled.img`
  width: 64px;
  height: 64px;
  object-fit: contain;
  border-radius: 50%;
  background: rgba(15, 23, 42, 0.85);
  padding: 0.6rem;
  border: 1px solid rgba(148, 197, 255, 0.35);
`

const BrandText = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
`

const BrandTitle = styled.h1`
  font-size: 1.5rem;
  margin: 0;
  color: #f8fafc;
  letter-spacing: 0.12em;
  text-transform: uppercase;
`

const UnitTag = styled.span`
  padding: 0.35rem 0.85rem;
  border-radius: 999px;
  background: rgba(15, 23, 42, 0.8);
  border: 1px solid rgba(148, 197, 255, 0.35);
  font-size: 0.85rem;
  color: rgba(148, 197, 255, 0.95);
  letter-spacing: 0.08em;
  text-transform: uppercase;
`

const BadgeRow = styled.div`
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
`

const StatusBadge = styled.span`
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.35rem 0.85rem;
  border-radius: 999px;
  background: rgba(59, 130, 246, 0.15);
  border: 1px solid rgba(148, 197, 255, 0.25);
  font-size: 0.8rem;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: rgba(226, 232, 240, 0.95);
`

const Pulse = styled.span`
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: #34d399;
  box-shadow: 0 0 0 4px rgba(52, 211, 153, 0.15);
  animation: pulse 1.8s ease infinite;
  
  @keyframes pulse {
    0% { transform: scale(1); opacity: 1; }
    70% { transform: scale(2); opacity: 0; }
    100% { opacity: 0; }
  }
`

const ChecklistSection = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
`

const ChecklistTitle = styled.h3`
  margin: 0;
  font-size: 0.95rem;
  letter-spacing: 0.2em;
  text-transform: uppercase;
  color: rgba(148, 197, 255, 0.85);
`

const ChecklistDesc = styled.p`
  margin: 0;
  color: rgba(226, 232, 240, 0.8);
  line-height: 1.5;
  font-size: 0.92rem;
`

const RequirementList = styled.ul`
  margin: 0;
  padding: 0;
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
`

const RequirementItem = styled.li`
  display: flex;
  align-items: center;
  gap: 0.6rem;
  padding: 0.65rem 0.85rem;
  border-radius: 0.85rem;
  background: rgba(15, 23, 42, 0.6);
  border: 1px dashed rgba(148, 197, 255, 0.35);
  font-size: 0.9rem;
  color: rgba(226, 232, 240, 0.9);
  line-height: 1.4;

  &::before {
    content: '•';
    color: rgba(96, 165, 250, 0.9);
    font-size: 1.2rem;
    line-height: 1;
  }
`

const SupportPanel = styled.div`
  border-top: 1px solid rgba(148, 197, 255, 0.2);
  padding-top: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
`

const SupportTitle = styled.p`
  margin: 0;
  font-size: 0.85rem;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: rgba(148, 197, 255, 0.85);
`

const SupportDesc = styled.p`
  margin: 0;
  color: rgba(226, 232, 240, 0.85);
  line-height: 1.5;
  font-size: 0.92rem;
`

const SupportButton = styled(Link)`
  margin-top: 0.4rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.4rem;
  padding: 0.65rem 1.25rem;
  border-radius: 999px;
  border: 1px solid rgba(96, 165, 250, 0.5);
  color: rgba(240, 249, 255, 0.95);
  text-decoration: none;
  font-weight: 600;
  letter-spacing: 0.04em;
  transition: background 0.2s ease, transform 0.2s ease;

  &:hover {
    background: rgba(96, 165, 250, 0.15);
    transform: translateY(-1px);
  }
`

const FormPanel = styled.section`
  background: rgba(5, 10, 23, 0.85);
  border: 1px solid rgba(59, 130, 246, 0.35);
  border-radius: 1.25rem;
  padding: clamp(1.5rem, 3vw, 2rem);
  backdrop-filter: blur(16px);
  box-shadow: 0 25px 70px rgba(2, 6, 23, 0.65);
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
  position: relative;
`

const FormHeader = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
`

const FormTitle = styled.h2`
  margin: 0;
  color: #f8fafc;
  font-size: 1.3rem;
  letter-spacing: 0.03em;
`

const FormSubtitle = styled.p`
  margin: 0;
  color: rgba(148, 197, 255, 0.9);
  font-size: 0.9rem;
`

const NoticeBox = styled.div`
  border-radius: 1rem;
  border: 1px dashed rgba(148, 197, 255, 0.4);
  background: rgba(15, 23, 42, 0.55);
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
`

const NoticeLabel = styled.span`
  font-size: 0.85rem;
  text-transform: uppercase;
  letter-spacing: 0.18em;
  color: rgba(148, 197, 255, 0.85);
`

const NoticeText = styled.p`
  margin: 0;
  color: rgba(226, 232, 240, 0.85);
  line-height: 1.5;
  font-size: 0.92rem;
`

const Form = styled.form`
  display: flex;
  flex-direction: column;
  gap: 1rem;
`

const FieldRow = styled.div`
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.85rem;
  
  @media (max-width: 640px) {
    grid-template-columns: 1fr;
  }
`

const FormGroup = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
`

const Label = styled.label`
  color: rgba(255, 255, 255, 0.9);
  font-size: 0.9rem;
  font-weight: 500;
`

const Input = styled.input`
  padding: 0.8rem;
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 0.5rem;
  background: rgba(255, 255, 255, 0.08);
  color: white;
  font-size: 1rem;
  font-family: inherit;
  backdrop-filter: blur(10px);
  transition: all 0.3s ease;
  width: 100%;
  box-sizing: border-box;
  
  &::placeholder {
    color: rgba(255, 255, 255, 0.5);
  }
  
  &:focus {
    outline: none;
    border-color: rgba(52, 152, 219, 0.6);
    background: rgba(255, 255, 255, 0.15);
    box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.1);
  }
`

const EmailPreview = styled.div`
  border-radius: 1rem;
  border: 1px solid rgba(96, 165, 250, 0.4);
  background: rgba(15, 118, 110, 0.1);
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
`

const PreviewLabel = styled.span`
  color: rgba(94, 234, 212, 0.9);
  font-size: 0.85rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
`

const PreviewValue = styled.span`
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', monospace;
  font-size: 1rem;
  color: #5eead4;
  word-break: break-all;
`

const ErrorMessage = styled.div`
  background: rgba(231, 76, 60, 0.1);
  border: 1px solid rgba(231, 76, 60, 0.3);
  border-radius: 0.75rem;
  padding: 0.85rem;
  color: rgba(231, 76, 60, 0.9);
  font-size: 0.9rem;
  text-align: center;
`

const SuccessMessage = styled.div`
  background: rgba(34, 197, 94, 0.12);
  border: 1px solid rgba(34, 197, 94, 0.35);
  border-radius: 0.75rem;
  padding: 0.85rem;
  color: rgba(34, 197, 94, 0.9);
  font-size: 0.9rem;
  text-align: center;
`

const RegisterPage = () => {
  const navigate = useNavigate()
  const { t } = useLanguage()
  const [formData, setFormData] = useState({
    firstName: '',
    lastName: '',
    personalEmail: '',
    password: '',
    confirmPassword: ''
  })
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [generatedPoliceEmail, setGeneratedPoliceEmail] = useState('')

  const isFormValid =
    formData.firstName &&
    formData.lastName &&
    formData.personalEmail &&
    formData.password &&
    formData.confirmPassword

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target
    setFormData(prev => ({
      ...prev,
      [name]: value
    }))
    
    // Generate police email when first or last name changes
    if (name === 'firstName' || name === 'lastName') {
      const firstName = name === 'firstName' ? value : formData.firstName
      const lastName = name === 'lastName' ? value : formData.lastName
      
      if (firstName && lastName) {
        const policeEmail = authApi.generatePoliceEmail(firstName, lastName)
        setGeneratedPoliceEmail(policeEmail)
      } else {
        setGeneratedPoliceEmail('')
      }
    }
    
    // Clear error when user starts typing
    if (error) setError('')
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    
    if (formData.password !== formData.confirmPassword) {
      setError(t('passwordsDontMatch'))
      return
    }
    
    setIsLoading(true)
    setError('')
    
    try {
      const response = await authApi.register({
        firstName: formData.firstName,
        lastName: formData.lastName,
        personalEmail: formData.personalEmail,
        password: formData.password
      })
      
      setSuccess(`${response.message} ${t('institutionalEmailInfo')} ${response.policeEmail}`)
      
      // Navigate to login after successful registration
      setTimeout(() => {
        navigate('/login')
      }, 3000)
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError(t('unexpectedError'))
      }
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <PageContainer>
      <BackgroundGrid />
      <BackButton to="/">
        ← {t('backToHome')}
      </BackButton>

      <ContentGrid>
        <BrandPanel>
          <BrandHeader>
            <DepartmentRow>
              <DepartmentBadge src={departmentBadge} alt="Metro Police Department badge" />
              <BrandText>
                <BrandTitle>{t('metropolitanPoliceDept')}</BrandTitle>
                <UnitTag>{t('coldCaseUnit')}</UnitTag>
              </BrandText>
            </DepartmentRow>
          </BrandHeader>

          <BadgeRow>
            <StatusBadge><Pulse /> {t('loginBadgeSecure')}</StatusBadge>
            <StatusBadge>{t('loginBadgeInternal')}</StatusBadge>
            <StatusBadge>{t('loginBadgeMonitored')}</StatusBadge>
          </BadgeRow>

          <ChecklistSection>
            <ChecklistTitle>{t('registrationChecklistTitle')}</ChecklistTitle>
            <ChecklistDesc>{t('registrationChecklistDesc')}</ChecklistDesc>
            <RequirementList>
              <RequirementItem>{t('registrationChecklistItem1')}</RequirementItem>
              <RequirementItem>{t('registrationChecklistItem2')}</RequirementItem>
              <RequirementItem>{t('registrationChecklistItem3')}</RequirementItem>
              <RequirementItem>{t('registrationChecklistItem4')}</RequirementItem>
            </RequirementList>
          </ChecklistSection>

          <SupportPanel>
            <SupportTitle>{t('registrationSupportTitle')}</SupportTitle>
            <SupportDesc>{t('registrationSupportDesc')}</SupportDesc>
            <SupportButton to="/login">{t('doLogin')}</SupportButton>
          </SupportPanel>
        </BrandPanel>

        <FormPanel>
          <FormHeader>
            <FormTitle>{t('registrationRequest')}</FormTitle>
            <FormSubtitle>{t('registrationFormSubtitle')}</FormSubtitle>
          </FormHeader>

          <NoticeBox>
            <NoticeLabel>{t('importantNote')}</NoticeLabel>
            <NoticeText>{t('systemRestricted')}</NoticeText>
          </NoticeBox>

          <Form onSubmit={handleSubmit}>
            {error && <ErrorMessage>{error}</ErrorMessage>}
            {success && <SuccessMessage>{success}</SuccessMessage>}

            <FieldRow>
              <FormGroup>
                <Label htmlFor="firstName">{t('firstName')}</Label>
                <Input
                  type="text"
                  id="firstName"
                  name="firstName"
                  placeholder="João"
                  value={formData.firstName}
                  onChange={handleInputChange}
                  required
                />
              </FormGroup>

              <FormGroup>
                <Label htmlFor="lastName">{t('lastName')}</Label>
                <Input
                  type="text"
                  id="lastName"
                  name="lastName"
                  placeholder="Silva"
                  value={formData.lastName}
                  onChange={handleInputChange}
                  required
                />
              </FormGroup>
            </FieldRow>

            {generatedPoliceEmail && (
              <EmailPreview>
                <PreviewLabel>{t('institutionalEmailPreview')}</PreviewLabel>
                <PreviewValue>{generatedPoliceEmail}</PreviewValue>
              </EmailPreview>
            )}

            <FormGroup>
              <Label htmlFor="personalEmail">{t('personalEmail')}</Label>
              <Input
                type="email"
                id="personalEmail"
                name="personalEmail"
                placeholder="joao.silva@gmail.com"
                value={formData.personalEmail}
                onChange={handleInputChange}
                required
              />
            </FormGroup>

            <FieldRow>
              <FormGroup>
                <Label htmlFor="password">{t('password')}</Label>
                <Input
                  type="password"
                  id="password"
                  name="password"
                  placeholder={t('minimumChars')}
                  value={formData.password}
                  onChange={handleInputChange}
                  required
                  minLength={8}
                />
              </FormGroup>

              <FormGroup>
                <Label htmlFor="confirmPassword">{t('confirmPassword')}</Label>
                <Input
                  type="password"
                  id="confirmPassword"
                  name="confirmPassword"
                  placeholder={t('confirmYourPassword')}
                  value={formData.confirmPassword}
                  onChange={handleInputChange}
                  required
                />
              </FormGroup>
            </FieldRow>

            <LoadingButton
              type="submit"
              loading={isLoading}
              disabled={isLoading || !isFormValid}
              aria-label={isLoading ? t('sendingRequest') : t('requestRegistrationBtn2')}
            >
              {isLoading ? t('sendingRequest') : t('requestRegistrationBtn2')}
            </LoadingButton>
          </Form>
        </FormPanel>
      </ContentGrid>
    </PageContainer>
  )
}

export default RegisterPage