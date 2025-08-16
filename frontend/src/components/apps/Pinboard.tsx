import React, { useState, useRef, useEffect } from 'react'
import styled from 'styled-components'
import { useLanguage } from '../../hooks/useLanguageContext'

const PinboardContainer = styled.div`
  height: 100%;
  display: flex;
  flex-direction: column;
  background: rgba(20, 20, 20, 0.9);
`

const PinboardHeader = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
`

const PinboardTitle = styled.h3`
  margin: 0;
  color: #4a9eff;
  display: flex;
  align-items: center;
  gap: 0.5rem;
`

const PinboardControls = styled.div`
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
`

const ControlButton = styled.button`
  padding: 0.25rem 0.5rem;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px;
  color: white;
  cursor: pointer;
  font-size: 12px;
  transition: all 0.2s ease;

  &:hover {
    background: rgba(255, 255, 255, 0.2);
  }

  &.add-button {
    background: rgba(74, 158, 255, 0.2);
    border-color: rgba(74, 158, 255, 0.4);
    
    &:hover {
      background: rgba(74, 158, 255, 0.3);
    }
  }

  &.danger {
    background: rgba(231, 76, 60, 0.2);
    border-color: rgba(231, 76, 60, 0.4);
    
    &:hover {
      background: rgba(231, 76, 60, 0.3);
    }
  }
`

const BoardArea = styled.div`
  flex: 1;
  position: relative;
  background: 
    radial-gradient(circle at 50% 50%, rgba(74, 158, 255, 0.05) 0%, transparent 50%),
    linear-gradient(45deg, rgba(255, 255, 255, 0.02) 25%, transparent 25%),
    linear-gradient(-45deg, rgba(255, 255, 255, 0.02) 25%, transparent 25%);
  background-size: 40px 40px, 20px 20px, 20px 20px;
  border: 2px dashed rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  overflow: hidden;
  min-height: 400px;
`

const DropZone = styled.div<{ $isDragOver: boolean }>`
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(255, 255, 255, 0.5);
  font-size: 18px;
  border: ${props => props.$isDragOver ? '3px dashed rgba(74, 158, 255, 0.6)' : 'none'};
  background: ${props => props.$isDragOver ? 'rgba(74, 158, 255, 0.1)' : 'transparent'};
  transition: all 0.3s ease;
  z-index: ${props => props.$isDragOver ? '10' : '1'};
`

const PinboardItem = styled.div<{ $x: number; $y: number; $isDragging: boolean; $type: 'evidence' | 'note' | 'photo' }>`
  position: absolute;
  left: ${props => props.$x}px;
  top: ${props => props.$y}px;
  width: 120px;
  min-height: 80px;
  background: ${props => {
    switch (props.$type) {
      case 'evidence': return 'linear-gradient(135deg, rgba(255, 193, 7, 0.9), rgba(255, 152, 0, 0.9))'
      case 'note': return 'linear-gradient(135deg, rgba(255, 235, 59, 0.9), rgba(255, 193, 7, 0.9))'
      case 'photo': return 'linear-gradient(135deg, rgba(156, 39, 176, 0.9), rgba(103, 58, 183, 0.9))'
      default: return 'rgba(255, 255, 255, 0.1)'
    }
  }};
  border: 1px solid rgba(0, 0, 0, 0.2);
  border-radius: 8px;
  padding: 8px;
  cursor: ${props => props.$isDragging ? 'grabbing' : 'grab'};
  transform: ${props => props.$isDragging ? 'rotate(5deg) scale(1.05)' : 'rotate(0deg) scale(1)'};
  transition: ${props => props.$isDragging ? 'none' : 'all 0.2s ease'};
  box-shadow: ${props => props.$isDragging ? '0 8px 25px rgba(0, 0, 0, 0.3)' : '0 2px 10px rgba(0, 0, 0, 0.2)'};
  z-index: ${props => props.$isDragging ? '100' : '10'};
  user-select: none;
  color: #000;
  font-size: 12px;
  font-weight: 500;
`

const ItemHeader = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 4px;
  font-weight: bold;
  font-size: 10px;
`

const ItemIcon = styled.span`
  font-size: 14px;
`

const ItemActions = styled.div`
  display: flex;
  gap: 2px;
  opacity: 0;
  transition: opacity 0.2s ease;

  ${PinboardItem}:hover & {
    opacity: 1;
  }
`

const ActionButton = styled.button`
  width: 16px;
  height: 16px;
  background: rgba(0, 0, 0, 0.3);
  border: none;
  border-radius: 2px;
  color: white;
  cursor: pointer;
  font-size: 8px;
  display: flex;
  align-items: center;
  justify-content: center;

  &:hover {
    background: rgba(0, 0, 0, 0.5);
  }

  &.delete {
    background: rgba(231, 76, 60, 0.7);
    
    &:hover {
      background: rgba(231, 76, 60, 0.9);
    }
  }
`

const ItemContent = styled.div`
  word-break: break-word;
  line-height: 1.2;
`

const ConnectionLine = styled.svg`
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  pointer-events: none;
  z-index: 5;
`

interface PinboardItemData {
  id: string
  type: 'evidence' | 'note' | 'photo'
  content: string
  x: number
  y: number
}

interface Connection {
  from: string
  to: string
}

const Pinboard: React.FC = () => {
  const { t } = useLanguage()
  const [items, setItems] = useState<PinboardItemData[]>([])
  const [connections, setConnections] = useState<Connection[]>([])
  const [draggedItem, setDraggedItem] = useState<string | null>(null)
  const [dragOffset, setDragOffset] = useState({ x: 0, y: 0 })
  const [isDragOver, setIsDragOver] = useState(false)
  const [connectingFrom, setConnectingFrom] = useState<string | null>(null)
  const boardRef = useRef<HTMLDivElement>(null)

  const addItem = (type: 'evidence' | 'note' | 'photo') => {
    const newItem: PinboardItemData = {
      id: `${type}-${Date.now()}`,
      type,
      content: type === 'evidence' ? 'Evidence item' : type === 'note' ? 'Note content' : 'Photo description',
      x: Math.random() * 300 + 50,
      y: Math.random() * 200 + 50
    }
    setItems(prev => [...prev, newItem])
  }

  const deleteItem = (id: string) => {
    setItems(prev => prev.filter(item => item.id !== id))
    setConnections(prev => prev.filter(conn => conn.from !== id && conn.to !== id))
  }

  const clearBoard = () => {
    setItems([])
    setConnections([])
    setConnectingFrom(null)
  }

  const handleItemMouseDown = (e: React.MouseEvent, itemId: string) => {
    e.preventDefault()
    const item = items.find(i => i.id === itemId)
    if (!item) return

    setDraggedItem(itemId)
    setDragOffset({
      x: e.clientX - item.x,
      y: e.clientY - item.y
    })
  }

  const handleItemClick = (itemId: string) => {
    if (connectingFrom === null) {
      setConnectingFrom(itemId)
    } else if (connectingFrom !== itemId) {
      // Create connection
      const newConnection = { from: connectingFrom, to: itemId }
      setConnections(prev => {
        const exists = prev.some(conn => 
          (conn.from === newConnection.from && conn.to === newConnection.to) ||
          (conn.from === newConnection.to && conn.to === newConnection.from)
        )
        if (!exists) {
          return [...prev, newConnection]
        }
        return prev
      })
      setConnectingFrom(null)
    } else {
      setConnectingFrom(null)
    }
  }

  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      if (draggedItem && boardRef.current) {
        const rect = boardRef.current.getBoundingClientRect()
        const newX = Math.max(0, Math.min(e.clientX - dragOffset.x - rect.left, rect.width - 120))
        const newY = Math.max(0, Math.min(e.clientY - dragOffset.y - rect.top, rect.height - 80))
        
        setItems(prev => prev.map(item => 
          item.id === draggedItem ? { ...item, x: newX, y: newY } : item
        ))
      }
    }

    const handleMouseUp = () => {
      setDraggedItem(null)
    }

    if (draggedItem) {
      document.addEventListener('mousemove', handleMouseMove)
      document.addEventListener('mouseup', handleMouseUp)
      return () => {
        document.removeEventListener('mousemove', handleMouseMove)
        document.removeEventListener('mouseup', handleMouseUp)
      }
    }
  }, [draggedItem, dragOffset])

  const getItemCenter = (item: PinboardItemData) => ({
    x: item.x + 60, // half width
    y: item.y + 40  // half height
  })

  const renderConnections = () => {
    return connections.map(conn => {
      const fromItem = items.find(item => item.id === conn.from)
      const toItem = items.find(item => item.id === conn.to)
      
      if (!fromItem || !toItem) return null

      const from = getItemCenter(fromItem)
      const to = getItemCenter(toItem)

      return (
        <line
          key={`${conn.from}-${conn.to}`}
          x1={from.x}
          y1={from.y}
          x2={to.x}
          y2={to.y}
          stroke="rgba(74, 158, 255, 0.6)"
          strokeWidth="2"
          strokeDasharray="5,5"
        />
      )
    })
  }

  const getItemIcon = (type: string) => {
    switch (type) {
      case 'evidence': return '🔍'
      case 'note': return '📝'
      case 'photo': return '📷'
      default: return '📌'
    }
  }

  const getItemTypeLabel = (type: string) => {
    switch (type) {
      case 'evidence': return t('evidenceItem')
      case 'note': return t('noteItem')
      case 'photo': return t('photoItem')
      default: return 'Item'
    }
  }

  return (
    <PinboardContainer>
      <PinboardHeader>
        <PinboardTitle>
          <span>📌</span>
          {t('pinboardTitle')}
        </PinboardTitle>
        <PinboardControls>
          <ControlButton 
            className="add-button"
            onClick={() => addItem('evidence')}
            title={t('addEvidence')}
          >
            🔍 {t('addEvidence')}
          </ControlButton>
          <ControlButton 
            className="add-button"
            onClick={() => addItem('note')}
            title={t('addNote')}
          >
            📝 {t('addNote')}
          </ControlButton>
          <ControlButton 
            className="add-button"
            onClick={() => addItem('photo')}
            title={t('addPhoto')}
          >
            📷 {t('addPhoto')}
          </ControlButton>
          <ControlButton 
            className="danger"
            onClick={clearBoard}
            title={t('clearBoard')}
          >
            🗑️ {t('clearBoard')}
          </ControlButton>
        </PinboardControls>
      </PinboardHeader>

      <BoardArea 
        ref={boardRef}
        onDragOver={(e) => {
          e.preventDefault()
          setIsDragOver(true)
        }}
        onDragLeave={() => setIsDragOver(false)}
        onDrop={(e) => {
          e.preventDefault()
          setIsDragOver(false)
        }}
      >
        {items.length === 0 && !isDragOver && (
          <DropZone $isDragOver={false}>
            {t('noItemsOnBoard')} - {t('dragToOrganize')}
          </DropZone>
        )}
        
        {isDragOver && (
          <DropZone $isDragOver={true}>
            {t('dropHereToAdd')}
          </DropZone>
        )}

        <ConnectionLine>
          {renderConnections()}
        </ConnectionLine>

        {items.map(item => (
          <PinboardItem
            key={item.id}
            $x={item.x}
            $y={item.y}
            $isDragging={draggedItem === item.id}
            $type={item.type}
            onMouseDown={(e) => handleItemMouseDown(e, item.id)}
            onClick={() => handleItemClick(item.id)}
            style={{
              border: connectingFrom === item.id ? '2px solid rgba(74, 158, 255, 0.8)' : undefined
            }}
          >
            <ItemHeader>
              <div>
                <ItemIcon>{getItemIcon(item.type)}</ItemIcon>
                <span style={{ marginLeft: '4px' }}>{getItemTypeLabel(item.type)}</span>
              </div>
              <ItemActions>
                <ActionButton
                  className="delete"
                  onClick={(e) => {
                    e.stopPropagation()
                    deleteItem(item.id)
                  }}
                  title={t('deleteItem')}
                >
                  ×
                </ActionButton>
              </ItemActions>
            </ItemHeader>
            <ItemContent>{item.content}</ItemContent>
          </PinboardItem>
        ))}
      </BoardArea>
      
      {connectingFrom && (
        <div style={{ 
          position: 'absolute', 
          bottom: '10px', 
          left: '50%', 
          transform: 'translateX(-50%)',
          background: 'rgba(74, 158, 255, 0.9)',
          color: 'white',
          padding: '8px 16px',
          borderRadius: '4px',
          fontSize: '12px'
        }}>
          {t('connectEvidence')} - Click another item to connect
        </div>
      )}
    </PinboardContainer>
  )
}

export default Pinboard