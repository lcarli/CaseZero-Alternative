import React from 'react'
import styled from 'styled-components'
import FileViewer from './apps/FileViewer'
import Email from './apps/Email'
import Notebook from './apps/Notebook'
import SubmitCase from './apps/SubmitCase'
import Logs from './apps/Logs'
import ForensicModule from './apps/ForensicModule'
import Clock from './Clock'

const DockContainer = styled.div`
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  height: 80px;
  background: rgba(0, 0, 0, 0.8);
  backdrop-filter: blur(20px);
  border-top: 1px solid rgba(255, 255, 255, 0.1);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 2rem;
  z-index: 10000;
`

const DockCenter = styled.div`
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 2rem;
  flex: 1;
`

const DockRight = styled.div`
  display: flex;
  align-items: center;
  margin-left: 2rem;
`

const DockIcon = styled.button`
  width: 60px;
  height: 60px;
  border: 2px solid rgba(52, 152, 219, 0.4);
  background: rgba(0, 0, 0, 0.6);
  border-radius: 12px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.3s ease;
  color: white;
  font-size: 24px;
  position: relative;
  backdrop-filter: blur(10px);
  text-shadow: 0 2px 4px rgba(0, 0, 0, 0.5);

  &:hover {
    background: rgba(52, 152, 219, 0.3);
    border-color: rgba(52, 152, 219, 0.8);
    transform: translateY(-4px);
    box-shadow: 0 8px 25px rgba(52, 152, 219, 0.3);
  }

  &:active {
    transform: translateY(-2px);
  }

  &::after {
    content: attr(data-title);
    position: absolute;
    bottom: -30px;
    left: 50%;
    transform: translateX(-50%);
    font-size: 10px;
    color: rgba(255, 255, 255, 0.9);
    white-space: nowrap;
    pointer-events: none;
    background: rgba(0, 0, 0, 0.8);
    padding: 2px 6px;
    border-radius: 4px;
    text-shadow: none;
  }

  @media (max-width: 768px) {
    width: 50px;
    height: 50px;
    font-size: 20px;
    
    &::after {
      font-size: 8px;
      bottom: -25px;
      padding: 1px 4px;
    }
  }
`

const LogoutIcon = styled(DockIcon)`
  border-color: rgba(231, 76, 60, 0.4);
  margin-left: 1rem;

  &:hover {
    background: rgba(231, 76, 60, 0.3);
    border-color: rgba(231, 76, 60, 0.8);
    box-shadow: 0 8px 25px rgba(231, 76, 60, 0.3);
  }
`

const ResponsiveDockContainer = styled(DockContainer)`
  @media (max-width: 768px) {
    height: 70px;
    padding: 0 1rem;
    
    ${DockCenter} {
      gap: 1rem;
    }
    
    ${DockRight} {
      margin-left: 1rem;
    }
  }

  @media (max-width: 480px) {
    padding: 0 0.5rem;
    
    ${DockCenter} {
      gap: 0.5rem;
    }
    
    ${DockRight} {
      margin-left: 0.5rem;
    }
  }
`

interface DockProps {
  onOpenWindow: (id: string, title: string, component: React.ComponentType) => void
  onLogout: () => void
}

const Dock: React.FC<DockProps> = ({ onOpenWindow, onLogout }) => {
  const dockItems = [
    {
      id: 'file-viewer',
      title: 'File Viewer',
      icon: '📁',
      component: FileViewer
    },
    {
      id: 'email',
      title: 'Email',
      icon: '📧',
      component: Email
    },
    {
      id: 'forensic',
      title: 'Forensic Module',
      icon: '🔬',
      component: ForensicModule
    },
    {
      id: 'logs',
      title: 'Logs',
      icon: '📋',
      component: Logs
    },
    {
      id: 'notebook',
      title: 'Notebook',
      icon: '📝',
      component: Notebook
    },
    {
      id: 'submit-case',
      title: 'Submit Case',
      icon: '⚖️',
      component: SubmitCase
    }
  ]

  return (
    <ResponsiveDockContainer>
      <DockCenter>
        {dockItems.map(item => (
          <DockIcon
            key={item.id}
            data-title={item.title}
            onClick={() => onOpenWindow(item.id, item.title, item.component)}
          >
            {item.icon}
          </DockIcon>
        ))}
      </DockCenter>
      <DockRight>
        <Clock />
        <LogoutIcon
          data-title="Sair do Sistema"
          onClick={onLogout}
          title="Sair do Sistema"
        >
          🚪
        </LogoutIcon>
      </DockRight>
    </ResponsiveDockContainer>
  )
}

export default Dock