import { useNavigate } from 'react-router-dom'
import styled from 'styled-components'
import Navigation from '../components/Navigation'
import { useLanguage } from '../hooks/useLanguageContext'
import LogoWithName from '../assets/Logo-With-Name-Transparent2.png'

const PageContainer = styled.div`
  min-height: 100vh;
  background: radial-gradient(circle at top, #182642 0%, #0c1222 45%, #070b14 100%);
  color: #f8fafc;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  position: relative;
`

const Texture = styled.div`
  position: absolute;
  inset: 0;
  background-image: linear-gradient(120deg, rgba(255,255,255,0.04) 0%, transparent 60%);
  pointer-events: none;
`

const MainContent = styled.main`
  position: relative;
  width: min(1200px, 92vw);
  margin: 0 auto;
  padding: 5rem 0 6rem;
  display: flex;
  flex-direction: column;
  gap: 5rem;
`

const HeroSection = styled.section`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 3rem;
  align-items: center;
  padding-top: 2rem;
`

const HeroText = styled.div`
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
`

const HeroEyebrow = styled.span`
  text-transform: uppercase;
  letter-spacing: 0.2em;
  font-size: 0.85rem;
  color: rgba(148, 197, 255, 0.9);
`

const HeroTitle = styled.h1`
  font-size: clamp(2.5rem, 4vw, 3.75rem);
  margin: 0;
  line-height: 1.1;
  color: #f1f5f9;
`

const HeroDescription = styled.p`
  font-size: 1.1rem;
  color: rgba(241, 245, 249, 0.78);
  line-height: 1.7;
  margin: 0;
`

const BadgeRow = styled.div`
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
`

const Badge = styled.span`
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.4rem 0.85rem;
  border-radius: 999px;
  border: 1px solid rgba(148, 197, 255, 0.35);
  color: rgba(226, 232, 240, 0.9);
  font-size: 0.85rem;
`

const HeroActions = styled.div`
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  margin-top: 0.5rem;
`

const LogoCard = styled.div`
  background: rgba(11, 16, 31, 0.8);
  border: 1px solid rgba(148, 197, 255, 0.15);
  border-radius: 1.5rem;
  padding: 2.5rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  box-shadow: 0 25px 60px rgba(3, 4, 16, 0.8);
  backdrop-filter: blur(18px);
  align-items: center;
`

const LogoImage = styled.img`
  width: 260px;
  max-width: 100%;
  object-fit: contain;
`

const StatusList = styled.div`
  width: 100%;
  display: grid;
  gap: 0.75rem;
`

const StatusItem = styled.div`
  display: flex;
  justify-content: space-between;
  border-radius: 0.9rem;
  background: rgba(23, 32, 52, 0.85);
  padding: 0.85rem 1rem;
  font-size: 0.9rem;
  color: rgba(203, 213, 225, 0.95);
  border: 1px solid rgba(148, 197, 255, 0.12);
`

const ActionButton = styled.button`
  padding: 0.95rem 1.75rem;
  border-radius: 999px;
  border: none;
  font-weight: 600;
  font-size: 1rem;
  cursor: pointer;
  transition: transform 0.2s ease, box-shadow 0.2s ease;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 180px;
  font-family: inherit;

  &.primary {
    background: linear-gradient(120deg, #3b82f6, #2563eb);
    color: #f8fafc;
    box-shadow: 0 10px 30px rgba(37, 99, 235, 0.35);
  }

  &.secondary {
    background: transparent;
    border: 1px solid rgba(148, 197, 255, 0.5);
    color: rgba(226, 232, 240, 0.9);
  }

  &:hover {
    transform: translateY(-2px);
    box-shadow: 0 18px 35px rgba(15, 23, 42, 0.45);
  }
`

const HighlightsSection = styled.section`
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
`

const SectionTitle = styled.h2`
  font-size: 1.9rem;
  margin: 0;
  color: #e0f2fe;
`

const HighlightsGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1.25rem;
`

const HighlightCard = styled.div`
  background: rgba(13, 20, 38, 0.9);
  border-radius: 1rem;
  border: 1px solid rgba(148, 197, 255, 0.15);
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  box-shadow: 0 18px 35px rgba(3, 6, 18, 0.6);
`

const HighlightTitle = styled.h3`
  margin: 0;
  font-size: 1.1rem;
  color: #f1f5f9;
`

const HighlightText = styled.p`
  margin: 0;
  color: rgba(226, 232, 240, 0.85);
  line-height: 1.5;
  font-size: 0.95rem;
`

const FeaturesSection = styled.section`
  display: flex;
  flex-direction: column;
  gap: 2rem;
`

const FeaturesGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 1.5rem;
`

const FeatureCard = styled.div`
  padding: 1.75rem;
  border-radius: 1.25rem;
  background: rgba(12, 18, 35, 0.95);
  border: 1px solid rgba(148, 197, 255, 0.18);
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.05);
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
`

const IconWrapper = styled.span`
  width: 3.25rem;
  height: 3.25rem;
  border-radius: 0.75rem;
  background: rgba(59, 130, 246, 0.12);
  color: #60a5fa;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid rgba(96, 165, 250, 0.25);
  svg {
    width: 24px;
    height: 24px;
    stroke: currentColor;
  }
`

const FeatureTitle = styled.h3`
  margin: 0;
  font-size: 1.25rem;
  color: #f8fafc;
`

const FeatureDescription = styled.p`
  margin: 0;
  color: rgba(226, 232, 240, 0.85);
  line-height: 1.6;
  font-size: 0.95rem;
`

const ProcessSection = styled.section`
  background: rgba(6, 9, 18, 0.9);
  border-radius: 1.5rem;
  border: 1px solid rgba(148, 197, 255, 0.18);
  padding: 2.5rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  box-shadow: 0 25px 60px rgba(3, 4, 16, 0.6);
`

const ProcessDescription = styled.p`
  margin: 0;
  color: rgba(226, 232, 240, 0.85);
  line-height: 1.6;
`

const ProcessList = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1.5rem;
`

const ProcessStep = styled.div`
  border-radius: 1.25rem;
  padding: 1.5rem;
  background: rgba(13, 19, 39, 0.95);
  border: 1px solid rgba(96, 165, 250, 0.2);
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
`

const StepNumber = styled.span`
  width: 2.2rem;
  height: 2.2rem;
  border-radius: 0.65rem;
  background: rgba(96, 165, 250, 0.2);
  color: #bfdbfe;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-weight: 600;
  font-size: 0.95rem;
`

const ProcessTitle = styled.h4`
  margin: 0;
  font-size: 1.05rem;
  color: #f8fafc;
`

const ProcessText = styled.p`
  margin: 0;
  color: rgba(212, 223, 239, 0.9);
  line-height: 1.5;
  font-size: 0.95rem;
`

const CTASection = styled.section`
  text-align: center;
  background: rgba(30, 64, 175, 0.22);
  border: 1px solid rgba(96, 165, 250, 0.25);
  border-radius: 1.75rem;
  padding: 3rem 2rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  box-shadow: 0 30px 80px rgba(15, 23, 42, 0.65);
`

const CTATitle = styled.h2`
  margin: 0;
  font-size: clamp(2rem, 3vw, 2.75rem);
  color: #e0f2fe;
`

const CTADescription = styled.p`
  margin: 0;
  color: rgba(226, 232, 240, 0.9);
  line-height: 1.7;
  max-width: 720px;
  align-self: center;
`

const CTAButtons = styled.div`
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  justify-content: center;
  margin-top: 1rem;
`

const IntelligenceIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
    <circle cx="12" cy="12" r="9" />
    <path d="M9 12h6" />
    <path d="M12 9v6" />
    <path d="M15.5 6.5l2-2" />
  </svg>
)

const CoordinationIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
    <circle cx="6.5" cy="6.5" r="2.5" />
    <circle cx="17.5" cy="6.5" r="2.5" />
    <circle cx="12" cy="17.5" r="2.5" />
    <path d="M8.3 8.3l2.4 6.1" />
    <path d="M15.7 8.3l-2.4 6.1" />
  </svg>
)

const EvidenceIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
    <rect x="4" y="4" width="16" height="16" rx="2" />
    <path d="M4 9h16" />
    <path d="M9 4v16" />
    <path d="M14 14l3 3" />
    <circle cx="13" cy="13" r="2" />
  </svg>
)

const TrainingIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
    <path d="M4 7l8-4 8 4-8 4-8-4z" />
    <path d="M4 12l8 4 8-4" />
    <path d="M4 17l8 4 8-4" />
  </svg>
)

const HomePage = () => {
  const navigate = useNavigate()
  const { t } = useLanguage()

  const highlights = [
    {
      title: t('landingHighlightEvidenceTitle'),
      text: t('landingHighlightEvidenceDesc')
    },
    {
      title: t('landingHighlightCollaborationTitle'),
      text: t('landingHighlightCollaborationDesc')
    },
    {
      title: t('landingHighlightAutomationTitle'),
      text: t('landingHighlightAutomationDesc')
    }
  ]

  const features = [
    {
      icon: <IntelligenceIcon />,
      title: t('landingFeatureIntelligenceTitle'),
      description: t('landingFeatureIntelligenceDesc')
    },
    {
      icon: <CoordinationIcon />,
      title: t('landingFeatureCoordinationTitle'),
      description: t('landingFeatureCoordinationDesc')
    },
    {
      icon: <EvidenceIcon />,
      title: t('landingFeatureEvidenceTitle'),
      description: t('landingFeatureEvidenceDesc')
    },
    {
      icon: <TrainingIcon />,
      title: t('landingFeatureTrainingTitle'),
      description: t('landingFeatureTrainingDesc')
    }
  ]

  const processSteps = [
    {
      title: t('landingProcessStepIntakeTitle'),
      text: t('landingProcessStepIntakeDesc')
    },
    {
      title: t('landingProcessStepAnalysisTitle'),
      text: t('landingProcessStepAnalysisDesc')
    },
    {
      title: t('landingProcessStepDecisionTitle'),
      text: t('landingProcessStepDecisionDesc')
    }
  ]

  return (
    <PageContainer>
      <Texture />
      <Navigation />
      <MainContent>
        <HeroSection>
          <HeroText>
            <HeroEyebrow>{t('heroSubtitle')}</HeroEyebrow>
            <HeroTitle>{t('heroTitle')}</HeroTitle>
            <HeroDescription>{t('heroDescription')}</HeroDescription>
            <BadgeRow>
              <Badge>{t('landingBadgeSecurity')}</Badge>
              <Badge>{t('landingBadgeCompliance')}</Badge>
              <Badge>{t('landingBadgeInternalUse')}</Badge>
            </BadgeRow>
            <HeroActions>
              <ActionButton className="primary" onClick={() => navigate('/login')}>
                {t('accessSystem')}
              </ActionButton>
              <ActionButton className="secondary" onClick={() => navigate('/register')}>
                {t('requestAccess')}
              </ActionButton>
            </HeroActions>
          </HeroText>
          <LogoCard>
            <LogoImage src={LogoWithName} alt="CaseZero" />
            <StatusList>
              <StatusItem>
                <span>{t('digitalInterface')}</span>
                <strong>v2.0</strong>
              </StatusItem>
              <StatusItem>
                <span>{t('caseManagement')}</span>
                <strong>24/7</strong>
              </StatusItem>
              <StatusItem>
                <span>{t('careerProgression')}</span>
                <strong>Live</strong>
              </StatusItem>
            </StatusList>
          </LogoCard>
        </HeroSection>

        <HighlightsSection>
          <SectionTitle>{t('landingHighlightsTitle')}</SectionTitle>
          <HighlightsGrid>
            {highlights.map((item) => (
              <HighlightCard key={item.title}>
                <HighlightTitle>{item.title}</HighlightTitle>
                <HighlightText>{item.text}</HighlightText>
              </HighlightCard>
            ))}
          </HighlightsGrid>
        </HighlightsSection>

        <FeaturesSection>
          <SectionTitle>{t('featuresTitle')}</SectionTitle>
          <FeaturesGrid>
            {features.map((feature) => (
              <FeatureCard key={feature.title}>
                <IconWrapper>{feature.icon}</IconWrapper>
                <FeatureTitle>{feature.title}</FeatureTitle>
                <FeatureDescription>{feature.description}</FeatureDescription>
              </FeatureCard>
            ))}
          </FeaturesGrid>
        </FeaturesSection>

        <ProcessSection>
          <SectionTitle>{t('landingProcessTitle')}</SectionTitle>
          <ProcessDescription>{t('landingProcessDescription')}</ProcessDescription>
          <ProcessList>
            {processSteps.map((step, index) => (
              <ProcessStep key={step.title}>
                <StepNumber>{String(index + 1).padStart(2, '0')}</StepNumber>
                <ProcessTitle>{step.title}</ProcessTitle>
                <ProcessText>{step.text}</ProcessText>
              </ProcessStep>
            ))}
          </ProcessList>
        </ProcessSection>

        <CTASection>
          <CTATitle>{t('landingCTAHeadline')}</CTATitle>
          <CTADescription>{t('landingCTADescription')}</CTADescription>
          <CTAButtons>
            <ActionButton className="primary" onClick={() => navigate('/login')}>
              {t('accessSystem')}
            </ActionButton>
            <ActionButton className="secondary" onClick={() => navigate('/register')}>
              {t('requestAccess')}
            </ActionButton>
          </CTAButtons>
        </CTASection>
      </MainContent>
    </PageContainer>
  )
}

export default HomePage