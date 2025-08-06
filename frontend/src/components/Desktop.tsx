import React, { useState } from 'react'
import styled from 'styled-components'
import Dock from './Dock'
import Window from './Window'
import type { WindowData } from './Window'

const DesktopContainer = styled.div`
  width: 100vw;
  height: 100vh;
  background: linear-gradient(135deg, #0a0f23 0%, #1a2140 25%, #2a3458 50%, #1a2140 75%, #0a0f23 100%);
  background-image: 
    radial-gradient(circle at 20% 80%, rgba(52, 152, 219, 0.1) 0%, transparent 50%),
    radial-gradient(circle at 80% 20%, rgba(74, 158, 255, 0.08) 0%, transparent 50%),
    repeating-linear-gradient(90deg, transparent, rgba(52, 152, 219, 0.03) 2px, transparent 4px),
    repeating-linear-gradient(0deg, transparent, rgba(52, 152, 219, 0.03) 2px, transparent 4px);
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
  background: rgba(0, 0, 0, 0.7);
  border: 2px solid rgba(52, 152, 219, 0.5);
  border-radius: 8px;
  padding: 10px 15px;
  color: rgba(255, 255, 255, 0.9);
  font-size: 12px;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 1px;
  backdrop-filter: blur(10px);
  box-shadow: 0 4px 15px rgba(0, 0, 0, 0.3);
  z-index: 1;
  
  &::before {
    content: '👮';
    margin-right: 8px;
    font-size: 16px;
  }
`

const SystemInfo = styled.div`
  position: absolute;
  bottom: 100px;
  left: 20px;
  background: rgba(0, 0, 0, 0.5);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 6px;
  padding: 8px 12px;
  color: rgba(255, 255, 255, 0.7);
  font-size: 11px;
  font-family: 'Courier New', monospace;
  backdrop-filter: blur(5px);
  z-index: 1;
  
  div {
    margin-bottom: 2px;
    
    &:last-child {
      margin-bottom: 0;
    }
  }
  
  .label {
    color: rgba(52, 152, 219, 0.8);
    display: inline-block;
    width: 60px;
  }
`

const DesktopArea = styled.div`
  width: 100%;
  height: calc(100vh - 80px); /* Reserve space for dock */
  position: relative;
  overflow: hidden;
`

const Desktop: React.FC = () => {
  const [windows, setWindows] = useState<WindowData[]>([])
  const [highestZIndex, setHighestZIndex] = useState(1000)

  const openWindow = (id: string, title: string, component: React.ComponentType) => {
    const existingWindow = windows.find(w => w.id === id)
    
    if (existingWindow) {
      // Bring existing window to front
      bringToFront(id)
      return
    }

    const newWindow: WindowData = {
      id,
      title,
      component,
      isOpen: true,
      position: { 
        x: Math.random() * 300 + 100, 
        y: Math.random() * 200 + 100 
      },
      size: { width: 600, height: 400 },
      zIndex: highestZIndex + 1
    }

    setWindows(prev => [...prev, newWindow])
    setHighestZIndex(prev => prev + 1)
  }

  const closeWindow = (id: string) => {
    setWindows(prev => prev.filter(w => w.id !== id))
  }

  const bringToFront = (id: string) => {
    const newZIndex = highestZIndex + 1
    setWindows(prev => 
      prev.map(w => 
        w.id === id ? { ...w, zIndex: newZIndex } : w
      )
    )
    setHighestZIndex(newZIndex)
  }

  const updateWindowPosition = (id: string, position: { x: number; y: number }) => {
    setWindows(prev => 
      prev.map(w => 
        w.id === id ? { ...w, position } : w
      )
    )
  }

  const updateWindowSize = (id: string, size: { width: number; height: number }) => {
    setWindows(prev => 
      prev.map(w => 
        w.id === id ? { ...w, size } : w
      )
    )
  }

  return (
    <DesktopContainer>
      <PoliceBadge>
        Metropolitan Police Dept
      </PoliceBadge>
      
      <SystemInfo>
        <div><span className="label">User:</span> Detective John Doe</div>
        <div><span className="label">Unit:</span> Investigation Division</div>
        <div><span className="label">Badge:</span> #4729</div>
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
          />
        ))}
      </DesktopArea>
      <Dock onOpenWindow={openWindow} />
    </DesktopContainer>
  )
}

export default Desktop