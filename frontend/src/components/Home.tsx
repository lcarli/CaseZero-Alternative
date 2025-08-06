import { useState } from 'react'
import styled from 'styled-components'
import reactLogo from '../assets/react.svg'

const HomeContainer = styled.div`
  text-align: center;
  padding: 2rem 0;
`

const LogoContainer = styled.div`
  display: flex;
  justify-content: center;
  gap: 2rem;
  margin-bottom: 2rem;
`

const Logo = styled.img`
  height: 6em;
  padding: 1.5em;
  transition: filter 300ms;

  &:hover {
    filter: drop-shadow(0 0 2em #646cffaa);
  }

  &.react:hover {
    filter: drop-shadow(0 0 2em #61dafbaa);
  }
`

const Title = styled.h1`
  font-size: 3.2em;
  line-height: 1.1;
  margin: 0 0 2rem 0;
  text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.3);
`

const Card = styled.div`
  padding: 2em;
  background: rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(10px);
  border-radius: 1rem;
  border: 1px solid rgba(255, 255, 255, 0.2);
  max-width: 600px;
  margin: 0 auto;
`

const Button = styled.button`
  border-radius: 8px;
  border: 1px solid transparent;
  padding: 0.6em 1.2em;
  font-size: 1em;
  font-weight: 500;
  font-family: inherit;
  background-color: #1a1a1a;
  color: white;
  cursor: pointer;
  transition: all 0.25s ease;

  &:hover {
    border-color: #646cff;
    background-color: #2a2a2a;
    transform: translateY(-2px);
  }

  &:focus,
  &:focus-visible {
    outline: 4px auto -webkit-focus-ring-color;
  }
`

const Description = styled.p`
  color: rgba(255, 255, 255, 0.8);
  margin-top: 1rem;
  line-height: 1.6;
`

const Home = () => {
  const [count, setCount] = useState(0)

  return (
    <HomeContainer>
      <LogoContainer>
        <a href="https://react.dev" target="_blank" rel="noopener noreferrer">
          <Logo src={reactLogo} className="react" alt="React logo" />
        </a>
      </LogoContainer>
      
      <Title>CaseZero Alternative</Title>
      
      <Card>
        <Button onClick={() => setCount((count) => count + 1)}>
          count is {count}
        </Button>
        <Description>
          Welcome to CaseZero Alternative - a React application built with Vite, TypeScript, styled-components, and React Router!
        </Description>
        <Description>
          Edit <code>src/components/Home.tsx</code> and save to test HMR
        </Description>
      </Card>
    </HomeContainer>
  )
}

export default Home