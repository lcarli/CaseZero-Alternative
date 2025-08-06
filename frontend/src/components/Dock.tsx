import React from 'react'
import styled from 'styled-components'
import FileViewer from './apps/FileViewer'
import Email from './apps/Email'
import Notebook from './apps/Notebook'
import SubmitCase from './apps/SubmitCase'

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
  justify-content: center;
  gap: 2rem;
  padding: 0 2rem;
  z-index: 10000;
`

const DockIcon = styled.button`
  width: 60px;
  height: 60px;
  border: none;
  background: rgba(255, 255, 255, 0.1);
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

  &:hover {
    background: rgba(255, 255, 255, 0.2);
    transform: translateY(-4px);
    box-shadow: 0 8px 25px rgba(0, 0, 0, 0.3);
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
    color: rgba(255, 255, 255, 0.8);
    white-space: nowrap;
    pointer-events: none;
  }

  @media (max-width: 768px) {
    width: 50px;
    height: 50px;
    font-size: 20px;
    
    &::after {
      font-size: 8px;
      bottom: -25px;
    }
  }
`

const ResponsiveDockContainer = styled(DockContainer)`
  @media (max-width: 768px) {
    height: 70px;
    gap: 1rem;
    padding: 0 1rem;
  }

  @media (max-width: 480px) {
    gap: 0.5rem;
    padding: 0 0.5rem;
  }
`

interface DockProps {
  onOpenWindow: (id: string, title: string, component: React.ComponentType) => void
}

const Dock: React.FC<DockProps> = ({ onOpenWindow }) => {
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
      {dockItems.map(item => (
        <DockIcon
          key={item.id}
          data-title={item.title}
          onClick={() => onOpenWindow(item.id, item.title, item.component)}
        >
          {item.icon}
        </DockIcon>
      ))}
    </ResponsiveDockContainer>
  )
}

export default Dock