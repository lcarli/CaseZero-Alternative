import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import styled from 'styled-components'
import { useLanguage } from '../hooks/useLanguageContext'
import { authApi, ApiError } from '../services/api'
import LanguageSelector from '../components/LanguageSelector'

const PageContainer = styled.div`
  min-height: 100vh;
  background: linear-gradient(135deg, #0a0f23 0%, #1a2140 25%, #2a3458 50%, #1a2140 75%, #0a0f23 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  padding: 1rem;
  box-sizing: border-box;
  
  @media (max-height: 800px) {
    align-items: flex-start;
    padding-top: 2rem;
  }
  
  @media (max-width: 480px) {
    padding: 0.5rem;
  }
`

const RegisterCard = styled.div`
  background: rgba(0, 0, 0, 0.4);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 1rem;
  padding: 2rem;
  backdrop-filter: blur(15px);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
  width: 100%;
  max-width: 500px;
  margin: 1rem 0;
  
  @media (max-width: 600px) {
    padding: 1.5rem;
    border-radius: 0.5rem;
    max-width: 100%;
    margin: 0.5rem 0;
  }
  
  @media (max-width: 480px) {
    padding: 1rem;
    margin: 0;
  }
`

const Header = styled.div`
  text-align: center;
  margin-bottom: 2rem;
`

const Logo = styled.div`
  font-size: 2.5rem;
  margin-bottom: 1rem;
  
  @media (max-width: 480px) {
    font-size: 2rem;
    margin-bottom: 0.5rem;
  }
`

const Title = styled.h1`
  color: white;
  font-size: 1.6rem;
  margin: 0 0 0.5rem 0;
  text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.5);
  
  @media (max-width: 480px) {
    font-size: 1.3rem;
  }
`

const Subtitle = styled.p`
  color: rgba(52, 152, 219, 0.8);
  margin: 0 0 1rem 0;
  font-size: 0.9rem;
`

const Form = styled.form`
  display: flex;
  flex-direction: column;
  gap: 1.2rem;
  
  @media (max-width: 480px) {
    gap: 1rem;
  }
`

const FormRow = styled.div`
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
  
  @media (max-width: 600px) {
    grid-template-columns: 1fr;
    gap: 0.8rem;
  }
`

const FormGroup = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
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
  background: rgba(255, 255, 255, 0.1);
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
  
  @media (max-width: 480px) {
    padding: 0.6rem;
    font-size: 0.9rem;
  }
`

const Button = styled.button`
  padding: 1rem;
  font-size: 1rem;
  font-weight: 600;
  border: none;
  border-radius: 0.5rem;
  background: linear-gradient(135deg, #3498db, #2980b9);
  color: white;
  cursor: pointer;
  transition: all 0.3s ease;
  box-shadow: 0 4px 15px rgba(52, 152, 219, 0.3);
  
  &:hover {
    transform: translateY(-2px);
    box-shadow: 0 6px 20px rgba(52, 152, 219, 0.4);
  }
  
  &:disabled {
    opacity: 0.6;
    cursor: not-allowed;
    transform: none;
  }
`

const Footer = styled.div`
  text-align: center;
  margin-top: 2rem;
  padding-top: 2rem;
  border-top: 1px solid rgba(52, 152, 219, 0.2);
`

const FooterText = styled.p`
  color: rgba(255, 255, 255, 0.7);
  margin: 0 0 1rem 0;
  font-size: 0.9rem;
`

const FooterLink = styled(Link)`
  color: rgba(52, 152, 219, 0.8);
  text-decoration: none;
  font-weight: 500;
  
  &:hover {
    color: rgba(52, 152, 219, 1);
    text-decoration: underline;
  }
`

const BackLink = styled(Link)`
  color: rgba(255, 255, 255, 0.7);
  text-decoration: none;
  font-size: 0.9rem;
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1rem;
  
  &:hover {
    color: white;
  }
`

const InfoBox = styled.div`
  background: rgba(52, 152, 219, 0.1);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 0.5rem;
  padding: 1rem;
  margin-bottom: 1.5rem;
  font-size: 0.9rem;
  color: rgba(255, 255, 255, 0.8);
  
  strong {
    color: rgba(52, 152, 219, 0.9);
  }
  
  @media (max-width: 480px) {
    padding: 0.8rem;
    font-size: 0.8rem;
    margin-bottom: 1rem;
  }
`

const EmailPreview = styled.div`
  background: rgba(46, 204, 113, 0.1);
  border: 1px solid rgba(46, 204, 113, 0.3);
  border-radius: 0.5rem;
  padding: 1rem;
  margin: 1rem 0;
  text-align: center;
  
  .label {
    color: rgba(46, 204, 113, 0.8);
    font-size: 0.9rem;
    margin-bottom: 0.5rem;
  }
  
  .email {
    color: rgba(46, 204, 113, 1);
    font-weight: 600;
    font-size: 1rem;
    font-family: monospace;
    word-break: break-all;
  }
  
  @media (max-width: 480px) {
    padding: 0.8rem;
    
    .label {
      font-size: 0.8rem;
    }
    
    .email {
      font-size: 0.9rem;
    }
  }
`

const ErrorMessage = styled.div`
  background: rgba(231, 76, 60, 0.1);
  border: 1px solid rgba(231, 76, 60, 0.3);
  border-radius: 0.5rem;
  padding: 1rem;
  margin-bottom: 1rem;
  color: rgba(231, 76, 60, 0.9);
  font-size: 0.9rem;
  text-align: center;
`

const SuccessMessage = styled.div`
  background: rgba(46, 204, 113, 0.1);
  border: 1px solid rgba(46, 204, 113, 0.3);
  border-radius: 0.5rem;
  padding: 1rem;
  margin-bottom: 1rem;
  color: rgba(46, 204, 113, 0.9);
  font-size: 0.9rem;
  text-align: center;
`

const LanguageSelectorWrapper = styled.div`
  position: absolute;
  top: 1rem;
  right: 1rem;
  min-width: 150px;
  z-index: 10;
  
  @media (max-width: 480px) {
    top: 0.5rem;
    right: 0.5rem;
    min-width: 130px;
  }
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

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
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
      <LanguageSelectorWrapper>
        <LanguageSelector />
      </LanguageSelectorWrapper>
      
      <RegisterCard>
        <BackLink to="/">
          ← {t('backToHome')}
        </BackLink>
        
        <Header>
          <Logo>🏛️</Logo>
          <Title>{t('registrationRequest')}</Title>
          <Subtitle>{t('metropolitanPoliceDept')}</Subtitle>
        </Header>

        <InfoBox>
          <strong>{t('importantNote')}</strong> {t('useInstitutionalLogin')}
        </InfoBox>

        <Form onSubmit={handleSubmit}>
          {error && <ErrorMessage>{error}</ErrorMessage>}
          {success && <SuccessMessage>{success}</SuccessMessage>}
          
          <FormRow>
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
          </FormRow>

          {generatedPoliceEmail && (
            <EmailPreview>
              <div className="label">{t('institutionalEmailPreview')}</div>
              <div className="email">{generatedPoliceEmail}</div>
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

          <FormRow>
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
          </FormRow>

          <Button type="submit" disabled={isLoading}>
            {isLoading ? t('submitting') : t('requestRegistrationBtn2')}
          </Button>
        </Form>

        <Footer>
          <FooterText>{t('alreadyHaveAccess')}</FooterText>
          <FooterLink to="/login">{t('doLogin')}</FooterLink>
        </Footer>
      </RegisterCard>
    </PageContainer>
  )
}

export default RegisterPage