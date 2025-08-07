import React, { useState, useRef, useEffect } from 'react'
import styled from 'styled-components'

export interface WindowData {
  id: string
  title: string
  component: React.ComponentType
  isOpen: boolean
  position: { x: number; y: number }
  size: { width: number; height: number }
  zIndex: number
  isMaximized: boolean
  originalPosition?: { x: number; y: number }
  originalSize?: { width: number; height: number }
}

const WindowContainer = styled.div<{ $x: number; $y: number; $width: number; $height: number; $zIndex: number; $isMaximized: boolean }>`
  position: ${props => props.$isMaximized ? 'fixed' : 'absolute'};
  left: ${props => props.$x}px;
  top: ${props => props.$y}px;
  width: ${props => props.$width}px;
  height: ${props => props.$height}px;
  z-index: ${props => props.$zIndex};
  background: rgba(30, 30, 45, 0.95);
  backdrop-filter: blur(20px);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: ${props => props.$isMaximized ? '0' : '12px'};
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  min-width: ${props => props.$isMaximized ? 'unset' : '500px'};
  min-height: ${props => props.$isMaximized ? 'unset' : '400px'};
  max-width: ${props => props.$isMaximized ? 'unset' : 'calc(100vw - 40px)'};
  max-height: ${props => props.$isMaximized ? 'unset' : 'calc(100vh - 120px)'};
`

const WindowHeader = styled.div`
  height: 40px;
  background: rgba(0, 0, 0, 0.3);
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 1rem;
  cursor: move;
  user-select: none;
`

const WindowTitle = styled.h3`
  margin: 0;
  color: white;
  font-size: 14px;
  font-weight: 500;
`

const WindowControls = styled.div`
  display: flex;
  gap: 0.5rem;
`

const WindowControl = styled.button`
  width: 18px;
  height: 18px;
  border: none;
  border-radius: 50%;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
  transition: all 0.2s ease;

  &.close {
    background: #ff5f56;
    color: white;
    
    &:hover {
      background: #ff3b30;
    }
  }

  &.minimize {
    background: #ffbd2e;
    color: white;
    
    &:hover {
      background: #ff9500;
    }
  }

  &.maximize {
    background: #28ca42;
    color: white;
    
    &:hover {
      background: #30d158;
    }
  }
`

const WindowContent = styled.div<{ $isResizing: boolean }>`
  flex: 1;
  overflow: auto;
  padding: 1rem;
  color: white;
  user-select: ${props => props.$isResizing ? 'none' : 'auto'};
`

const ResizeHandle = styled.div`
  position: absolute;
  bottom: 0;
  right: 0;
  width: 20px;
  height: 20px;
  cursor: se-resize;
  background: linear-gradient(-45deg, transparent 30%, rgba(255, 255, 255, 0.1) 30%, rgba(255, 255, 255, 0.1) 70%, transparent 70%);
  
  &::after {
    content: '';
    position: absolute;
    bottom: 2px;
    right: 2px;
    width: 10px;
    height: 10px;
    background: linear-gradient(-45deg, transparent 30%, rgba(255, 255, 255, 0.2) 30%);
  }
`

interface WindowProps {
  window: WindowData
  onClose: () => void
  onFocus: () => void
  onPositionChange: (position: { x: number; y: number }) => void
  onSizeChange: (size: { width: number; height: number }) => void
  onMaximize: () => void
  onMinimize: () => void
}

const Window: React.FC<WindowProps> = ({
  window,
  onClose,
  onFocus,
  onPositionChange,
  onSizeChange,
  onMaximize,
  onMinimize
}) => {
  const [isDragging, setIsDragging] = useState(false)
  const [isResizing, setIsResizing] = useState(false)
  const [dragOffset, setDragOffset] = useState({ x: 0, y: 0 })
  const [resizeStart, setResizeStart] = useState({ x: 0, y: 0, width: 0, height: 0 })
  const windowRef = useRef<HTMLDivElement>(null)

  const handleMouseDown = (e: React.MouseEvent) => {
    onFocus()
    e.preventDefault()
  }

  const handleHeaderMouseDown = (e: React.MouseEvent) => {
    if (!window.isMaximized && (e.target === e.currentTarget || (e.target as HTMLElement).tagName === 'H3')) {
      setIsDragging(true)
      setDragOffset({
        x: e.clientX - window.position.x,
        y: e.clientY - window.position.y
      })
    }
  }

  const handleResizeMouseDown = (e: React.MouseEvent) => {
    if (!window.isMaximized) {
      e.stopPropagation()
      setIsResizing(true)
      setResizeStart({
        x: e.clientX,
        y: e.clientY,
        width: window.size.width,
        height: window.size.height
      })
    }
  }

  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      if (isDragging) {
        const newX = Math.max(0, Math.min(e.clientX - dragOffset.x, globalThis.innerWidth - window.size.width))
        const newY = Math.max(0, Math.min(e.clientY - dragOffset.y, globalThis.innerHeight - window.size.height - 80))
        onPositionChange({ x: newX, y: newY })
      } else if (isResizing) {
        const deltaX = e.clientX - resizeStart.x
        const deltaY = e.clientY - resizeStart.y
        const newWidth = Math.max(500, Math.min(resizeStart.width + deltaX, globalThis.innerWidth - window.position.x - 20))
        const newHeight = Math.max(400, Math.min(resizeStart.height + deltaY, globalThis.innerHeight - window.position.y - 100))
        onSizeChange({ width: newWidth, height: newHeight })
      }
    }

    const handleMouseUp = () => {
      setIsDragging(false)
      setIsResizing(false)
    }

    if (isDragging || isResizing) {
      document.addEventListener('mousemove', handleMouseMove)
      document.addEventListener('mouseup', handleMouseUp)
      return () => {
        document.removeEventListener('mousemove', handleMouseMove)
        document.removeEventListener('mouseup', handleMouseUp)
      }
    }
  }, [isDragging, isResizing, dragOffset, resizeStart, window, onPositionChange, onSizeChange])

  const Component = window.component

  return (
    <WindowContainer
      ref={windowRef}
      $x={window.position.x}
      $y={window.position.y}
      $width={window.size.width}
      $height={window.size.height}
      $zIndex={window.zIndex}
      $isMaximized={window.isMaximized}
      onMouseDown={handleMouseDown}
    >
      <WindowHeader onMouseDown={handleHeaderMouseDown}>
        <WindowTitle>{window.title}</WindowTitle>
        <WindowControls>
          <WindowControl className="minimize" onClick={onMinimize}>−</WindowControl>
          <WindowControl className="maximize" onClick={onMaximize}>
            {window.isMaximized ? '⧉' : '□'}
          </WindowControl>
          <WindowControl className="close" onClick={onClose}>×</WindowControl>
        </WindowControls>
      </WindowHeader>
      <WindowContent $isResizing={isResizing}>
        <Component />
      </WindowContent>
      {!window.isMaximized && <ResizeHandle onMouseDown={handleResizeMouseDown} />}
    </WindowContainer>
  )
}

export default Window