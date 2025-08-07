import { useState, useEffect } from 'react'
import { useSearchParams, useNavigate, Link } from 'react-router-dom'
import styled from 'styled-components'
import { authApi, ApiError } from '../services/api'

const PageContainer = styled.div`
  min-height: 100vh;
  background: linear-gradient(135deg, #0a0f23 0%, #1a2140 25%, #2a3458 50%, #1a2140 75%, #0a0f23 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  padding: 2rem;
`

const Card = styled.div`
  background: rgba(0, 0, 0, 0.4);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 1rem;
  padding: 3rem;
  backdrop-filter: blur(15px);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
  width: 100%;
  max-width: 500px;
  text-align: center;
`

const Header = styled.div`
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

const Message = styled.div`
  padding: 1.5rem;
  border-radius: 0.5rem;
  margin: 1.5rem 0;
  font-size: 1.1rem;
  line-height: 1.6;
`

const SuccessMessage = styled(Message)`
  background: rgba(46, 204, 113, 0.1);
  border: 1px solid rgba(46, 204, 113, 0.3);
  color: rgba(46, 204, 113, 0.9);
`

const ErrorMessage = styled(Message)`
  background: rgba(231, 76, 60, 0.1);
  border: 1px solid rgba(231, 76, 60, 0.3);
  color: rgba(231, 76, 60, 0.9);
`

const LoadingMessage = styled(Message)`
  background: rgba(52, 152, 219, 0.1);
  border: 1px solid rgba(52, 152, 219, 0.3);
  color: rgba(52, 152, 219, 0.9);
`

const Button = styled.button`
  padding: 1rem 2rem;
  font-size: 1rem;
  font-weight: 600;
  border: none;
  border-radius: 0.5rem;
  background: linear-gradient(135deg, #3498db, #2980b9);
  color: white;
  cursor: pointer;
  transition: all 0.3s ease;
  box-shadow: 0 4px 15px rgba(52, 152, 219, 0.3);
  margin: 0.5rem;
  
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

const FooterLink = styled(Link)`
  color: rgba(52, 152, 219, 0.8);
  text-decoration: none;
  font-weight: 500;
  
  &:hover {
    color: rgba(52, 152, 219, 1);
    text-decoration: underline;
  }
`

const EmailVerificationPage = () => {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const [status, setStatus] = useState<'loading' | 'success' | 'error'>('loading')
  const [message, setMessage] = useState('')
  const [isResending, setIsResending] = useState(false)

  useEffect(() => {
    const token = searchParams.get('token')
    
    if (!token) {
      setStatus('error')
      setMessage('Token de verificação não encontrado na URL.')
      return
    }

    verifyEmail(token)
  }, [searchParams])

  const verifyEmail = async (token: string) => {
    try {
      const response = await authApi.verifyEmail({ token })
      setStatus('success')
      setMessage(response.message)
      
      // Auto-redirect to login after success
      setTimeout(() => {
        navigate('/login')
      }, 3000)
    } catch (err) {
      setStatus('error')
      if (err instanceof ApiError) {
        setMessage(err.message)
      } else {
        setMessage('Erro ao verificar email. Tente novamente.')
      }
    }
  }

  const handleResendVerification = async () => {
    const email = prompt('Digite seu email institucional para reenviar a verificação:')
    if (!email) return

    setIsResending(true)
    try {
      const response = await authApi.resendVerification({ email })
      alert(response.message)
    } catch (err) {
      if (err instanceof ApiError) {
        alert(err.message)
      } else {
        alert('Erro ao reenviar email.')
      }
    } finally {
      setIsResending(false)
    }
  }

  const renderContent = () => {
    switch (status) {
      case 'loading':
        return (
          <LoadingMessage>
            🔄 Verificando seu email...
          </LoadingMessage>
        )
      
      case 'success':
        return (
          <>
            <SuccessMessage>
              ✅ {message}
              <br /><br />
              Redirecionando para o login em 3 segundos...
            </SuccessMessage>
            <Button onClick={() => navigate('/login')}>
              Ir para Login
            </Button>
          </>
        )
      
      case 'error':
        return (
          <>
            <ErrorMessage>
              ❌ {message}
            </ErrorMessage>
            <div>
              <Button onClick={handleResendVerification} disabled={isResending}>
                {isResending ? 'Reenviando...' : 'Reenviar Verificação'}
              </Button>
              <br />
              <FooterLink to="/register">Fazer novo registro</FooterLink>
              {' | '}
              <FooterLink to="/login">Voltar ao login</FooterLink>
            </div>
          </>
        )
    }
  }

  return (
    <PageContainer>
      <Card>
        <Header>
          <Logo>📧</Logo>
          <Title>Verificação de Email</Title>
          <Subtitle>Sistema CaseZero</Subtitle>
        </Header>

        {renderContent()}
      </Card>
    </PageContainer>
  )
}

export default EmailVerificationPage