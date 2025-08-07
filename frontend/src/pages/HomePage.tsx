import { useNavigate } from 'react-router-dom'
import styled from 'styled-components'
import { useLanguage } from '../hooks/useLanguageContext'
import Navigation from '../components/Navigation'

const PageContainer = styled.div`
  min-height: 100vh;
  background: linear-gradient(135deg, #0a0f23 0%, #1a2140 25%, #2a3458 50%, #1a2140 75%, #0a0f23 100%);
  display: flex;
  flex-direction: column;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  color: white;
`

const MainContent = styled.main`
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 6rem 2rem 2rem;
  min-height: 100vh;
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
  background: rgba(0, 0, 0, 0.4);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 1rem;
  padding: 2rem;
  margin-bottom: 3rem;
  backdrop-filter: blur(15px);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
  max-width: 600px;
  text-align: left;
`

const GameInfoTitle = styled.h3`
  color: rgba(52, 152, 219, 0.9);
  margin: 0 0 1rem 0;
  font-size: 1.2rem;
  font-weight: 600;
`

const FeatureList = styled.ul`
  list-style: none;
  padding: 0;
  margin: 1rem 0;
  
  li {
    padding: 0.5rem 0;
    color: rgba(255, 255, 255, 0.9);
    display: flex;
    align-items: center;
    gap: 0.5rem;
    
    &::before {
      content: '🔍';
      font-size: 1rem;
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
  font-weight: 600;
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
  const { t } = useLanguage()

  return (
    <PageContainer>
      <Navigation />
      
      <MainContent>
        <LogoContainer>
          <LogoIcon>🕵️‍♂️</LogoIcon>
          <div>
            <Title>CaseZero</Title>
            <Subtitle>{t('heroSubtitle')}</Subtitle>
          </div>
        </LogoContainer>

        <Description>
          {t('heroDescription')}
        </Description>

        <GameInfo>
          <GameInfoTitle>🎮 {t('featuresTitle')}</GameInfoTitle>
          <FeatureList>
            <li>{t('realisticInvestigationDesc')}</li>
            <li>{t('evidenceAnalysisDesc')}</li>
            <li>{t('authenticPoliceInterface')}</li>
            <li>{t('multipleCases')}</li>
            <li>{t('detectiveProgression')}</li>
            <li>{t('forensicAnalysis')}</li>
          </FeatureList>
        </GameInfo>

        <ButtonContainer>
          <Button 
            className="primary" 
            onClick={() => navigate('/login')}
          >
            {t('login')}
          </Button>
          <Button 
            className="secondary" 
            onClick={() => navigate('/register')}
          >
            {t('register')}
          </Button>
        </ButtonContainer>
      </MainContent>
    </PageContainer>
  )
}

export default HomePage