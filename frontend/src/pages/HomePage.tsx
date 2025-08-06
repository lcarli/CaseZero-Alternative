import { useNavigate } from 'react-router-dom'
import styled from 'styled-components'

const PageContainer = styled.div`
  min-height: 100vh;
  background: linear-gradient(135deg, #0a0f23 0%, #1a2140 25%, #2a3458 50%, #1a2140 75%, #0a0f23 100%);
  background-image: 
    radial-gradient(circle at 20% 80%, rgba(52, 152, 219, 0.1) 0%, transparent 50%),
    radial-gradient(circle at 80% 20%, rgba(74, 158, 255, 0.08) 0%, transparent 50%);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  color: white;
  padding: 2rem;
`

const LogoContainer = styled.div`
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  margin-bottom: 3rem;
`

const LogoIcon = styled.div`
  font-size: 4rem;
  color: rgba(52, 152, 219, 0.8);
`

const Title = styled.h1`
  font-size: 3.5rem;
  margin: 0;
  text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.5);
  color: rgba(255, 255, 255, 0.95);
`

const Subtitle = styled.h2`
  font-size: 1.5rem;
  margin: 0 0 2rem 0;
  color: rgba(52, 152, 219, 0.8);
  font-weight: 300;
`

const Description = styled.div`
  max-width: 800px;
  text-align: center;
  margin-bottom: 3rem;
  line-height: 1.6;
  font-size: 1.1rem;
  color: rgba(255, 255, 255, 0.8);
`

const GameInfo = styled.div`
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 1rem;
  padding: 2rem;
  margin-bottom: 3rem;
  backdrop-filter: blur(10px);
  max-width: 600px;
  text-align: left;
`

const FeatureList = styled.ul`
  list-style: none;
  padding: 0;
  margin: 1rem 0;
  
  li {
    padding: 0.5rem 0;
    color: rgba(255, 255, 255, 0.9);
    
    &::before {
      content: '🔍 ';
      margin-right: 0.5rem;
    }
  }
`

const ButtonContainer = styled.div`
  display: flex;
  gap: 1.5rem;
  justify-content: center;
  flex-wrap: wrap;
`

const Button = styled.button`
  padding: 1rem 2rem;
  font-size: 1.1rem;
  font-weight: 500;
  border: none;
  border-radius: 0.5rem;
  cursor: pointer;
  transition: all 0.3s ease;
  font-family: inherit;
  min-width: 150px;
  
  &.primary {
    background: linear-gradient(135deg, #3498db, #2980b9);
    color: white;
    box-shadow: 0 4px 15px rgba(52, 152, 219, 0.3);
    
    &:hover {
      transform: translateY(-2px);
      box-shadow: 0 6px 20px rgba(52, 152, 219, 0.4);
    }
  }
  
  &.secondary {
    background: rgba(255, 255, 255, 0.1);
    color: white;
    border: 1px solid rgba(255, 255, 255, 0.3);
    backdrop-filter: blur(10px);
    
    &:hover {
      background: rgba(255, 255, 255, 0.2);
      transform: translateY(-2px);
    }
  }
`

const HomePage = () => {
  const navigate = useNavigate()

  return (
    <PageContainer>
      <LogoContainer>
        <LogoIcon>🕵️‍♂️</LogoIcon>
        <div>
          <Title>CaseZero</Title>
          <Subtitle>Detective Investigation System</Subtitle>
        </div>
      </LogoContainer>

      <Description>
        Bem-vindo ao CaseZero, um jogo imersivo de investigação detetivesca onde você assume o papel 
        de um detetive experiente resolvendo casos complexos. Use suas habilidades analíticas, 
        examine evidências, entreviste suspeitos e desvende mistérios intrigantes.
      </Description>

      <GameInfo>
        <h3 style={{ color: 'rgba(52, 152, 219, 0.9)', marginTop: 0 }}>🎮 Características do Jogo</h3>
        <FeatureList>
          <li>Casos investigativos realistas e envolventes</li>
          <li>Sistema de evidências detalhado</li>
          <li>Interface de computador policial autêntica</li>
          <li>Múltiplos casos para resolver</li>
          <li>Progressão do detetive baseada em performance</li>
          <li>Análise forense e coleta de pistas</li>
        </FeatureList>
      </GameInfo>

      <ButtonContainer>
        <Button 
          className="primary" 
          onClick={() => navigate('/login')}
        >
          Entrar no Sistema
        </Button>
        <Button 
          className="secondary" 
          onClick={() => navigate('/register')}
        >
          Registrar-se
        </Button>
      </ButtonContainer>
    </PageContainer>
  )
}

export default HomePage