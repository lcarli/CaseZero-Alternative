import React, { useState } from 'react'
import styled from 'styled-components'
import Dock from './Dock'
import Window from './Window'
import type { WindowData } from './Window'

const DesktopContainer = styled.div`
  width: 100vw;
  height: 100vh;
  background: linear-gradient(135deg, #1a1a2e 0%, #0f0f23 50%, #16213e 100%);
  background-image: 
    radial-gradient(circle at 20% 80%, rgba(120, 119, 198, 0.1) 0%, transparent 50%),
    radial-gradient(circle at 80% 20%, rgba(255, 119, 198, 0.05) 0%, transparent 50%);
  position: relative;
  overflow: hidden;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
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