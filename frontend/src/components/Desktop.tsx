import React, { useEffect } from 'react'
import styled from 'styled-components'
import { useNavigate } from 'react-router-dom'
import Dock from './Dock'
import Window from './Window'
import { useWindowContext } from '../hooks/useWindowContext'
import { useCase } from '../hooks/useCaseContext'
import { useAuth } from '../hooks/useAuthContext'
import { useTimeContext } from '../hooks/useTimeContext'
import { useLanguage } from '../hooks/useLanguageContext'
import { caseSessionApi } from '../services/api'

const DesktopContainer = styled.div`
  width: 100vw;
  height: 100vh;
  background-image: 
    radial-gradient(circle at 20% 80%, rgba(52, 152, 219, 0.1) 0%, transparent 50%),
    radial-gradient(circle at 80% 20%, rgba(74, 158, 255, 0.08) 0%, transparent 50%),
    repeating-linear-gradient(90deg, transparent, rgba(52, 152, 219, 0.03) 2px, transparent 4px),
    repeating-linear-gradient(0deg, transparent, rgba(52, 152, 219, 0.03) 2px, transparent 4px),
    linear-gradient(135deg, #0a0f23 0%, #1a2140 25%, #2a3458 50%, #1a2140 75%, #0a0f23 100%);
  position: relative;
  overflow: hidden;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  
  &::before {
    content: '';
    position: absolute;
    top: 20px;
    left: 20px;
    width: 80px;
    height: 80px;
    background: rgba(52, 152, 219, 0.1);
    border: 2px solid rgba(52, 152, 219, 0.3);
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 32px;
    color: rgba(52, 152, 219, 0.6);
    z-index: 1;
  }
  
  &::after {
    content: '🏛️';
    position: absolute;
    top: 20px;
    left: 20px;
    width: 80px;
    height: 80px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 32px;
    z-index: 2;
  }
`



const PoliceBadge = styled.div`
  position: absolute;
  top: 20px;
  right: 20px;
  background: rgba(0, 0, 0, 0.8);
  border: 2px solid rgba(52, 152, 219, 0.7);
  border-radius: 8px;
  padding: 10px 15px;
  color: rgba(255, 255, 255, 0.95);
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 1px;
  backdrop-filter: blur(15px);
  box-shadow: 0 4px 15px rgba(0, 0, 0, 0.4);
  z-index: 1;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.8);
  
  &::before {
    content: '👮';
    margin-right: 8px;
    font-size: 16px;
    filter: drop-shadow(0 1px 2px rgba(0, 0, 0, 0.8));
  }
`

const SystemInfo = styled.div`
  position: absolute;
  bottom: 100px;
  left: 20px;
  background: rgba(0, 0, 0, 0.8);
  border: 1px solid rgba(52, 152, 219, 0.5);
  border-radius: 6px;
  padding: 8px 12px;
  color: rgba(255, 255, 255, 0.9);
  font-size: 11px;
  font-family: 'Courier New', monospace;
  backdrop-filter: blur(10px);
  z-index: 1;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.8);
  
  div {
    margin-bottom: 2px;
    
    &:last-child {
      margin-bottom: 0;
    }
  }
  
  .label {
    color: rgba(52, 152, 219, 0.9);
    display: inline-block;
    width: 60px;
    font-weight: 600;
  }
`

const DesktopArea = styled.div`
  width: 100%;
  height: calc(100vh - 80px); /* Reserve space for dock */
  position: relative;
  overflow: hidden;
`

const Desktop: React.FC = () => {
  const navigate = useNavigate()
  const { user } = useAuth()
  const { gameTime } = useTimeContext()
  const { t, language } = useLanguage()
  const {
    windows,
    openWindow,
    closeWindow,
    bringToFront,
    updateWindowPosition,
    updateWindowSize,
    maximizeWindow,
    minimizeWindow
  } = useWindowContext()

  const { currentCase } = useCase()

  // Add desktop-mode class when component mounts, remove when it unmounts
  useEffect(() => {
    document.body.classList.add('desktop-mode')
    return () => {
      document.body.classList.remove('desktop-mode')
    }
  }, [])

  const handleCaseDisconnect = async () => {
    try {
      // Save current session time if we have a case and game time
      if (currentCase && gameTime) {
        await caseSessionApi.endSession(currentCase, {
          gameTimeAtEnd: gameTime.toISOString()
        })
        console.log('Session saved successfully for case:', currentCase)
      }
      
      // Navigate to dashboard without logging out (keep user authenticated)
      navigate('/dashboard')
    } catch (error) {
      console.error('Error during case disconnect:', error)
      // Still navigate to dashboard even if session save fails
      navigate('/dashboard')
    }
  }

  return (
    <DesktopContainer>
      <PoliceBadge>
        Metropolitan Police Dept
      </PoliceBadge>
      
      <SystemInfo>
        <div><span className="label">User:</span> {user?.firstName} {user?.lastName}</div>
        <div><span className="label">Unit:</span> {user?.department || 'Investigation Division'}</div>
        <div><span className="label">Badge:</span> #{user?.badgeNumber || '4729'}</div>
        <div><span className="label">{t('currentCase')}:</span> {currentCase || 'No Case'}</div>
        <div><span className="label">{t('currentLanguage')}:</span> {language.flag} {language.code}</div>
        <div><span className="label">Status:</span> Active</div>
      </SystemInfo>
      
      <DesktopArea>
        {windows.map(window => (
          <Window
            key={window.id}
            window={window}
            onClose={() => closeWindow(window.id)}
            onFocus={() => bringToFront(window.id)}
            onPositionChange={(position) => updateWindowPosition(window.id, position)}
            onSizeChange={(size) => updateWindowSize(window.id, size)}
            onMaximize={() => maximizeWindow(window.id)}
            onMinimize={() => minimizeWindow(window.id)}
          />
        ))}
      </DesktopArea>
      <Dock onOpenWindow={openWindow} onCaseDisconnect={handleCaseDisconnect} />
    </DesktopContainer>
  )
}

export default Desktop