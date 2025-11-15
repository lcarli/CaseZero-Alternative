import React, { createContext, useState } from 'react'
import type { ReactNode } from 'react'
import type { WindowData } from '../components/Window'

interface WindowContextType {
  windows: WindowData[]
  highestZIndex: number
  openWindow: (id: string, title: string, component: React.ComponentType<any>, componentProps?: Record<string, any>) => void
  closeWindow: (id: string) => void
  bringToFront: (id: string) => void
  updateWindowPosition: (id: string, position: { x: number; y: number }) => void
  updateWindowSize: (id: string, size: { width: number; height: number }) => void
  maximizeWindow: (id: string) => void
  minimizeWindow: (id: string) => void
  isWindowOpen: (id: string) => boolean
  getWindow: (id: string) => WindowData | undefined
}

const WindowContext = createContext<WindowContextType | undefined>(undefined)

interface WindowProviderProps {
  children: ReactNode
}

export const WindowProvider: React.FC<WindowProviderProps> = ({ children }) => {
  const [windows, setWindows] = useState<WindowData[]>([])
  const [highestZIndex, setHighestZIndex] = useState(1000)

  const openWindow = (id: string, title: string, component: React.ComponentType<any>, componentProps?: Record<string, any>) => {
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
      componentProps,
      isOpen: true,
      position: { 
        x: Math.random() * 300 + 100, 
        y: Math.random() * 200 + 100 
      },
      size: { width: 600, height: 400 },
      zIndex: highestZIndex + 1,
      isMaximized: false
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

  const maximizeWindow = (id: string) => {
    setWindows(prev => 
      prev.map(w => {
        if (w.id === id) {
          if (w.isMaximized) {
            // Restore to original size and position
            return { 
              ...w, 
              isMaximized: false,
              position: w.originalPosition || w.position,
              size: w.originalSize || w.size,
              originalPosition: undefined,
              originalSize: undefined
            }
          } else {
            // Maximize to fullscreen
            return { 
              ...w, 
              isMaximized: true,
              originalPosition: w.position,
              originalSize: w.size,
              position: { x: 0, y: 0 },
              size: { width: globalThis.innerWidth, height: globalThis.innerHeight - 80 }
            }
          }
        }
        return w
      })
    )
  }

  const minimizeWindow = (id: string) => {
    setWindows(prev => 
      prev.map(w => {
        if (w.id === id && w.isMaximized) {
          // Restore from fullscreen to original size
          return { 
            ...w, 
            isMaximized: false,
            position: w.originalPosition || { x: 100, y: 100 },
            size: w.originalSize || { width: 600, height: 400 },
            originalPosition: undefined,
            originalSize: undefined
          }
        }
        return w
      })
    )
  }

  const isWindowOpen = (id: string): boolean => {
    return windows.some(w => w.id === id)
  }

  const getWindow = (id: string): WindowData | undefined => {
    return windows.find(w => w.id === id)
  }

  const contextValue: WindowContextType = {
    windows,
    highestZIndex,
    openWindow,
    closeWindow,
    bringToFront,
    updateWindowPosition,
    updateWindowSize,
    maximizeWindow,
    minimizeWindow,
    isWindowOpen,
    getWindow
  }

  return (
    <WindowContext.Provider value={contextValue}>
      {children}
    </WindowContext.Provider>
  )
}

export default WindowContext