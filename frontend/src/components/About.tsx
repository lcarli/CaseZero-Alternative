import styled from 'styled-components'

const AboutContainer = styled.div`
  max-width: 800px;
  margin: 0 auto;
  padding: 2rem 0;
`

const Title = styled.h1`
  font-size: 2.5em;
  margin-bottom: 1.5rem;
  text-align: center;
  text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.3);
`

const Card = styled.div`
  background: rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(10px);
  border-radius: 1rem;
  border: 1px solid rgba(255, 255, 255, 0.2);
  padding: 2rem;
  margin-bottom: 2rem;
`

const Section = styled.section`
  margin-bottom: 2rem;
`

const SectionTitle = styled.h2`
  font-size: 1.8em;
  margin-bottom: 1rem;
  color: #61dafb;
`

const Text = styled.p`
  line-height: 1.6;
  margin-bottom: 1rem;
  color: rgba(255, 255, 255, 0.9);
`

const TechList = styled.ul`
  list-style: none;
  padding: 0;
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 1rem;
`

const TechItem = styled.li`
  background: rgba(255, 255, 255, 0.05);
  padding: 1rem;
  border-radius: 0.5rem;
  border-left: 4px solid #61dafb;
`

const TechName = styled.strong`
  display: block;
  color: #61dafb;
  margin-bottom: 0.5rem;
`

const About = () => {
  return (
    <AboutContainer>
      <Title>About CaseZero Alternative</Title>
      
      <Card>
        <Section>
          <SectionTitle>Project Overview</SectionTitle>
          <Text>
            CaseZero Alternative is a modern web application built with cutting-edge technologies 
            to provide a robust foundation for case studies and alternative solutions.
          </Text>
          <Text>
            This frontend application demonstrates the integration of React with TypeScript, 
            showcasing best practices in modern web development.
          </Text>
        </Section>

        <Section>
          <SectionTitle>Technologies Used</SectionTitle>
          <TechList>
            <TechItem>
              <TechName>React 19</TechName>
              A powerful JavaScript library for building user interfaces with the latest features and optimizations.
            </TechItem>
            <TechItem>
              <TechName>TypeScript</TechName>
              Adds static type definitions to JavaScript, enhancing code quality and developer experience.
            </TechItem>
            <TechItem>
              <TechName>Vite</TechName>
              Next generation frontend tooling for fast development and optimized builds.
            </TechItem>
            <TechItem>
              <TechName>Styled Components</TechName>
              CSS-in-JS library for styling React components with dynamic styling capabilities.
            </TechItem>
            <TechItem>
              <TechName>React Router</TechName>
              Declarative routing for React applications with modern navigation patterns.
            </TechItem>
            <TechItem>
              <TechName>ESLint</TechName>
              Static code analysis tool for identifying and fixing code quality issues.
            </TechItem>
          </TechList>
        </Section>

        <Section>
          <SectionTitle>Features</SectionTitle>
          <Text>✨ Modern React 19 with TypeScript support</Text>
          <Text>🎨 Styled Components for dynamic and maintainable CSS</Text>
          <Text>🚀 Vite for lightning-fast development and builds</Text>
          <Text>🛣️ React Router for seamless navigation</Text>
          <Text>📱 Responsive design with mobile-first approach</Text>
          <Text>🔧 ESLint configuration for code quality</Text>
        </Section>
      </Card>
    </AboutContainer>
  )
}

export default About