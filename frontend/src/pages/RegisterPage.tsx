import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import styled from 'styled-components'

const PageContainer = styled.div`
  min-height: 100vh;
  background: linear-gradient(135deg, #0a0f23 0%, #1a2140 25%, #2a3458 50%, #1a2140 75%, #0a0f23 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  padding: 2rem;
`

const RegisterCard = styled.div`
  background: rgba(0, 0, 0, 0.4);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 1rem;
  padding: 3rem;
  backdrop-filter: blur(15px);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
  width: 100%;
  max-width: 500px;
`

const Header = styled.div`
  text-align: center;
  margin-bottom: 2rem;
`

const Logo = styled.div`
  font-size: 3rem;
  margin-bottom: 1rem;
`

const Title = styled.h1`
  color: white;
  font-size: 1.8rem;
  margin: 0 0 0.5rem 0;
  text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.5);
`

const Subtitle = styled.p`
  color: rgba(52, 152, 219, 0.8);
  margin: 0 0 1rem 0;
  font-size: 0.9rem;
`

const Form = styled.form`
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
`

const FormRow = styled.div`
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
  
  @media (max-width: 600px) {
    grid-template-columns: 1fr;
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

const Select = styled.select`
  padding: 0.8rem;
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 0.5rem;
  background: rgba(255, 255, 255, 0.1);
  color: white;
  font-size: 1rem;
  font-family: inherit;
  backdrop-filter: blur(10px);
  transition: all 0.3s ease;
  
  option {
    background: #1a2140;
    color: white;
  }
  
  &:focus {
    outline: none;
    border-color: rgba(52, 152, 219, 0.6);
    background: rgba(255, 255, 255, 0.15);
    box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.1);
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
`

const RegisterPage = () => {
  const navigate = useNavigate()
  const [formData, setFormData] = useState({
    firstName: '',
    lastName: '',
    email: '',
    phone: '',
    department: '',
    position: '',
    badgeNumber: '',
    password: '',
    confirmPassword: ''
  })
  const [isLoading, setIsLoading] = useState(false)

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value } = e.target
    setFormData(prev => ({
      ...prev,
      [name]: value
    }))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    
    if (formData.password !== formData.confirmPassword) {
      alert('As senhas não coincidem!')
      return
    }
    
    setIsLoading(true)
    
    // TODO: Implement actual registration
    // For now, simulate a registration process
    setTimeout(() => {
      setIsLoading(false)
      // Navigate to login after successful registration
      alert('Solicitação de registro enviada com sucesso! Aguarde aprovação do administrador.')
      navigate('/login')
    }, 1500)
  }

  return (
    <PageContainer>
      <RegisterCard>
        <BackLink to="/">
          ← Voltar ao início
        </BackLink>
        
        <Header>
          <Logo>🏛️</Logo>
          <Title>Solicitação de Registro</Title>
          <Subtitle>Metropolitan Police Department</Subtitle>
        </Header>

        <InfoBox>
          <strong>Importante:</strong> Este sistema é restrito a funcionários autorizados do departamento de polícia. 
          Todas as solicitações passam por verificação antes da aprovação.
        </InfoBox>

        <Form onSubmit={handleSubmit}>
          <FormRow>
            <FormGroup>
              <Label htmlFor="firstName">Nome</Label>
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
              <Label htmlFor="lastName">Sobrenome</Label>
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

          <FormGroup>
            <Label htmlFor="email">Email Institucional</Label>
            <Input
              type="email"
              id="email"
              name="email"
              placeholder="joao.silva@police.gov"
              value={formData.email}
              onChange={handleInputChange}
              required
            />
          </FormGroup>

          <FormRow>
            <FormGroup>
              <Label htmlFor="phone">Telefone</Label>
              <Input
                type="tel"
                id="phone"
                name="phone"
                placeholder="(11) 99999-9999"
                value={formData.phone}
                onChange={handleInputChange}
                required
              />
            </FormGroup>

            <FormGroup>
              <Label htmlFor="badgeNumber">Número do Distintivo</Label>
              <Input
                type="text"
                id="badgeNumber"
                name="badgeNumber"
                placeholder="4729"
                value={formData.badgeNumber}
                onChange={handleInputChange}
                required
              />
            </FormGroup>
          </FormRow>

          <FormRow>
            <FormGroup>
              <Label htmlFor="department">Departamento</Label>
              <Select
                id="department"
                name="department"
                value={formData.department}
                onChange={handleInputChange}
                required
              >
                <option value="">Selecione...</option>
                <option value="investigation">Divisão de Investigação</option>
                <option value="forensics">Perícia Criminal</option>
                <option value="cybercrime">Crimes Cibernéticos</option>
                <option value="homicide">Homicídios</option>
                <option value="fraud">Fraudes</option>
                <option value="narcotics">Narcóticos</option>
              </Select>
            </FormGroup>

            <FormGroup>
              <Label htmlFor="position">Cargo</Label>
              <Select
                id="position"
                name="position"
                value={formData.position}
                onChange={handleInputChange}
                required
              >
                <option value="">Selecione...</option>
                <option value="detective">Detetive</option>
                <option value="inspector">Inspetor</option>
                <option value="sergeant">Sargento</option>
                <option value="specialist">Especialista</option>
                <option value="analyst">Analista</option>
              </Select>
            </FormGroup>
          </FormRow>

          <FormRow>
            <FormGroup>
              <Label htmlFor="password">Senha</Label>
              <Input
                type="password"
                id="password"
                name="password"
                placeholder="Mínimo 8 caracteres"
                value={formData.password}
                onChange={handleInputChange}
                required
                minLength={8}
              />
            </FormGroup>

            <FormGroup>
              <Label htmlFor="confirmPassword">Confirmar Senha</Label>
              <Input
                type="password"
                id="confirmPassword"
                name="confirmPassword"
                placeholder="Confirme sua senha"
                value={formData.confirmPassword}
                onChange={handleInputChange}
                required
              />
            </FormGroup>
          </FormRow>

          <Button type="submit" disabled={isLoading}>
            {isLoading ? 'Enviando solicitação...' : 'Solicitar Registro'}
          </Button>
        </Form>

        <Footer>
          <FooterText>Já possui acesso ao sistema?</FooterText>
          <FooterLink to="/login">Fazer Login</FooterLink>
        </Footer>
      </RegisterCard>
    </PageContainer>
  )
}

export default RegisterPage