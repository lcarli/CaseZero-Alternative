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
  padding-top: 5rem;
`

// Hero Section
const HeroSection = styled.section`
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 4rem 2rem;
  min-height: 100vh;
  text-align: center;
  position: relative;
`

const LogoContainer = styled.div`
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 1.5rem;
  margin-bottom: 2rem;
  
  @media (max-width: 768px) {
    flex-direction: column;
    gap: 1rem;
  }
`

const LogoIcon = styled.div`
  font-size: 5rem;
  color: rgba(52, 152, 219, 0.8);
  animation: pulse 2s infinite;
  
  @keyframes pulse {
    0%, 100% { transform: scale(1); }
    50% { transform: scale(1.05); }
  }
  
  @media (max-width: 768px) {
    font-size: 4rem;
  }
`

const Title = styled.h1`
  font-size: 4rem;
  margin: 0;
  text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.5);
  color: rgba(255, 255, 255, 0.95);
  font-weight: 700;
  letter-spacing: -0.02em;
  
  @media (max-width: 768px) {
    font-size: 2.5rem;
  }
`

const Subtitle = styled.h2`
  font-size: 1.8rem;
  margin: 1rem 0 2rem 0;
  color: rgba(52, 152, 219, 0.8);
  font-weight: 300;
  
  @media (max-width: 768px) {
    font-size: 1.4rem;
  }
`

const Description = styled.p`
  max-width: 700px;
  margin: 0 auto 3rem;
  line-height: 1.7;
  font-size: 1.2rem;
  color: rgba(255, 255, 255, 0.8);
  
  @media (max-width: 768px) {
    font-size: 1rem;
    margin-bottom: 2rem;
  }
`

const HeroButtons = styled.div`
  display: flex;
  gap: 1.5rem;
  justify-content: center;
  flex-wrap: wrap;
  margin-bottom: 4rem;
`

// Features Section
const FeaturesSection = styled.section`
  padding: 5rem 2rem;
  max-width: 1200px;
  margin: 0 auto;
`

const SectionTitle = styled.h2`
  font-size: 2.5rem;
  text-align: center;
  margin-bottom: 3rem;
  color: rgba(255, 255, 255, 0.95);
  font-weight: 600;
  
  @media (max-width: 768px) {
    font-size: 2rem;
  }
`

const FeaturesGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 2rem;
  margin-bottom: 3rem;
`

const FeatureCard = styled.div`
  background: rgba(0, 0, 0, 0.4);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 1rem;
  padding: 2rem;
  backdrop-filter: blur(15px);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
  transition: all 0.3s ease;
  
  &:hover {
    transform: translateY(-5px);
    border-color: rgba(52, 152, 219, 0.5);
    box-shadow: 0 12px 40px rgba(0, 0, 0, 0.4);
  }
`

const FeatureIcon = styled.div`
  font-size: 2.5rem;
  margin-bottom: 1rem;
  color: rgba(52, 152, 219, 0.8);
`

const FeatureTitle = styled.h3`
  font-size: 1.3rem;
  margin-bottom: 1rem;
  color: rgba(255, 255, 255, 0.95);
  font-weight: 600;
`

const FeatureDescription = styled.p`
  color: rgba(255, 255, 255, 0.8);
  line-height: 1.6;
  margin: 0;
`

// Game Mechanics Section
const GameMechanicsSection = styled.section`
  background: rgba(0, 0, 0, 0.2);
  padding: 5rem 2rem;
  margin: 3rem 0;
`

const MechanicsContainer = styled.div`
  max-width: 1000px;
  margin: 0 auto;
  text-align: center;
`

const MechanicsList = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 2rem;
  margin-top: 3rem;
`

const MechanicItem = styled.div.withConfig({
  shouldForwardProp: (prop) => prop !== 'emoji'
})<{ emoji: string }>`
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  
  &::before {
    content: '${props => props.emoji}';
    font-size: 3rem;
    margin-bottom: 1rem;
    display: block;
  }
`

const MechanicTitle = styled.h4`
  font-size: 1.1rem;
  margin-bottom: 0.5rem;
  color: rgba(52, 152, 219, 0.9);
  font-weight: 600;
`

const MechanicText = styled.p`
  color: rgba(255, 255, 255, 0.8);
  line-height: 1.5;
  margin: 0;
  font-size: 0.95rem;
`

// Call to Action Section
const CTASection = styled.section`
  padding: 5rem 2rem;
  text-align: center;
  max-width: 800px;
  margin: 0 auto;
`

const CTATitle = styled.h2`
  font-size: 2.2rem;
  margin-bottom: 1rem;
  color: rgba(255, 255, 255, 0.95);
  font-weight: 600;
  
  @media (max-width: 768px) {
    font-size: 1.8rem;
  }
`

const CTADescription = styled.p`
  font-size: 1.1rem;
  color: rgba(255, 255, 255, 0.8);
  margin-bottom: 3rem;
  line-height: 1.6;
`

const CTAButtons = styled.div`
  display: flex;
  gap: 1.5rem;
  justify-content: center;
  flex-wrap: wrap;
`

// Button Styles
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
  text-decoration: none;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  
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
  
  &.outline {
    background: transparent;
    color: rgba(52, 152, 219, 0.9);
    border: 2px solid rgba(52, 152, 219, 0.6);
    
    &:hover {
      background: rgba(52, 152, 219, 0.1);
      border-color: rgba(52, 152, 219, 0.8);
      transform: translateY(-2px);
    }
  }
`

const HomePage = () => {
  const navigate = useNavigate()
  const { t } = useLanguage()

  const features = [
    {
      icon: '🔍',
      title: t('realisticInvestigation'),
      description: t('realisticInvestigationDesc')
    },
    {
      icon: '🧬',
      title: t('evidenceAnalysis'), 
      description: t('evidenceAnalysisDesc')
    },
    {
      icon: '🖥️',
      title: 'Digital Interface',
      description: t('authenticPoliceInterface')
    },
    {
      icon: '📊',
      title: t('caseManagement'),
      description: t('caseManagementDesc')
    },
    {
      icon: '🏅',
      title: 'Career Progression',
      description: t('detectiveProgression')
    },
    {
      icon: '⚖️',
      title: 'Justice System',
      description: 'Experience the complete investigation to conviction process'
    }
  ]

  const mechanics = [
    { emoji: '🕵️‍♂️', title: 'Investigation', text: 'Interview witnesses, analyze crime scenes' },
    { emoji: '🔬', title: 'Forensics', text: 'Process evidence in the lab' },
    { emoji: '📝', title: 'Documentation', text: 'Maintain detailed case files' },
    { emoji: '⚡', title: 'Real-time', text: 'Cases evolve over time' }
  ]

  return (
    <PageContainer>
      <Navigation />
      
      <MainContent>
        {/* Hero Section */}
        <HeroSection>
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

          <HeroButtons>
            <Button 
              className="primary" 
              onClick={() => navigate('/login')}
            >
              {t('login')}
            </Button>
            <Button 
              className="outline" 
              onClick={() => navigate('/register')}
            >
              {t('register')}
            </Button>
          </HeroButtons>
        </HeroSection>

        {/* Features Section */}
        <FeaturesSection>
          <SectionTitle>🎮 {t('featuresTitle')}</SectionTitle>
          <FeaturesGrid>
            {features.map((feature, index) => (
              <FeatureCard key={index}>
                <FeatureIcon>{feature.icon}</FeatureIcon>
                <FeatureTitle>{feature.title}</FeatureTitle>
                <FeatureDescription>{feature.description}</FeatureDescription>
              </FeatureCard>
            ))}
          </FeaturesGrid>
        </FeaturesSection>

        {/* Game Mechanics Section */}
        <GameMechanicsSection>
          <MechanicsContainer>
            <SectionTitle>How It Works</SectionTitle>
            <Description>
              Experience authentic police investigation procedures through immersive gameplay
            </Description>
            <MechanicsList>
              {mechanics.map((mechanic, index) => (
                <MechanicItem key={index} emoji={mechanic.emoji}>
                  <MechanicTitle>{mechanic.title}</MechanicTitle>
                  <MechanicText>{mechanic.text}</MechanicText>
                </MechanicItem>
              ))}
            </MechanicsList>
          </MechanicsContainer>
        </GameMechanicsSection>

        {/* Call to Action Section */}
        <CTASection>
          <CTATitle>Ready to Solve Your First Case?</CTATitle>
          <CTADescription>
            Join the Metropolitan Police Department's investigation system and start your career as a detective. 
            Access restricted to authorized personnel only.
          </CTADescription>
          <CTAButtons>
            <Button 
              className="primary" 
              onClick={() => navigate('/login')}
            >
              Access System
            </Button>
            <Button 
              className="secondary" 
              onClick={() => navigate('/register')}
            >
              Request Access
            </Button>
          </CTAButtons>
        </CTASection>
      </MainContent>
    </PageContainer>
  )
}

export default HomePage