import { Link } from 'react-router-dom'
import styled from 'styled-components'

const NavContainer = styled.nav`
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  background: rgba(0, 0, 0, 0.3);
  backdrop-filter: blur(15px);
  border-bottom: 1px solid rgba(52, 152, 219, 0.2);
  padding: 1rem 2rem;
  z-index: 1000;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
`

const NavContent = styled.div`
  max-width: 1200px;
  margin: 0 auto;
  display: flex;
  justify-content: space-between;
  align-items: center;
`

const Logo = styled(Link)`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  text-decoration: none;
  color: white;
  font-size: 1.2rem;
  font-weight: 600;
  
  &:hover {
    color: rgba(52, 152, 219, 0.8);
  }
`

const LogoIcon = styled.span`
  font-size: 1.5rem;
`

const NavMenu = styled.div`
  display: flex;
  gap: 1rem;
  align-items: center;
`

const NavButton = styled(Link)`
  padding: 0.6rem 1.2rem;
  border-radius: 0.5rem;
  text-decoration: none;
  font-weight: 500;
  font-size: 0.9rem;
  transition: all 0.3s ease;
  display: inline-flex;
  align-items: center;
  
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
    
    &:hover {
      background: rgba(255, 255, 255, 0.2);
      transform: translateY(-2px);
    }
  }
`

interface NavigationProps {
  showAuth?: boolean
}

const Navigation: React.FC<NavigationProps> = ({ showAuth = true }) => {
  return (
    <NavContainer>
      <NavContent>
        <Logo to="/">
          <LogoIcon>🏛️</LogoIcon>
          CaseZero
        </Logo>
        
        {showAuth && (
          <NavMenu>
            <NavButton to="/login" className="secondary">
              Entrar
            </NavButton>
            <NavButton to="/register" className="primary">
              Registrar
            </NavButton>
          </NavMenu>
        )}
      </NavContent>
    </NavContainer>
  )
}

export default Navigation