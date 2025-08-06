import React, { useState, useEffect, useRef } from 'react'
import styled from 'styled-components'
import { useTimeContext } from '../../hooks/useTimeContext'
import type { TimeEntry } from '../../contexts/TimeContext'

const LogsContainer = styled.div`
  width: 100%;
  height: 100%;
  background: rgba(5, 10, 20, 0.95);
  color: rgba(255, 255, 255, 0.9);
  font-family: 'Courier New', monospace;
  display: flex;
  flex-direction: column;
  overflow: hidden;
`

const LogsHeader = styled.div`
  background: rgba(0, 0, 0, 0.8);
  border-bottom: 2px solid rgba(52, 152, 219, 0.6);
  padding: 16px 20px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
`

const HeaderTitle = styled.h2`
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: rgba(52, 152, 219, 0.9);
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.8);
  
  &::before {
    content: '📋 ';
    margin-right: 8px;
  }
`

const HeaderInfo = styled.div`
  font-size: 12px;
  color: rgba(255, 255, 255, 0.7);
  text-align: right;
  
  .count {
    color: rgba(52, 152, 219, 0.8);
    font-weight: 600;
  }
`

const FilterContainer = styled.div`
  background: rgba(0, 0, 0, 0.6);
  border-bottom: 1px solid rgba(52, 152, 219, 0.3);
  padding: 12px 20px;
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
`

const FilterButton = styled.button.withConfig({
  shouldForwardProp: (prop) => !['active'].includes(prop)
})<{ active: boolean }>`
  background: ${props => props.active ? 'rgba(52, 152, 219, 0.6)' : 'rgba(0, 0, 0, 0.6)'};
  border: 1px solid ${props => props.active ? 'rgba(52, 152, 219, 0.8)' : 'rgba(52, 152, 219, 0.4)'};
  color: rgba(255, 255, 255, 0.9);
  padding: 6px 12px;
  border-radius: 4px;
  font-size: 11px;
  font-family: 'Courier New', monospace;
  cursor: pointer;
  transition: all 0.3s ease;
  
  &:hover {
    background: rgba(52, 152, 219, 0.5);
    border-color: rgba(52, 152, 219, 0.8);
  }
`

const LogsList = styled.div`
  flex: 1;
  overflow-y: auto;
  padding: 16px 20px;
  
  &::-webkit-scrollbar {
    width: 8px;
  }
  
  &::-webkit-scrollbar-track {
    background: rgba(0, 0, 0, 0.2);
  }
  
  &::-webkit-scrollbar-thumb {
    background: rgba(52, 152, 219, 0.4);
    border-radius: 4px;
  }
  
  &::-webkit-scrollbar-thumb:hover {
    background: rgba(52, 152, 219, 0.6);
  }
`

const LogEntry = styled.div.withConfig({
  shouldForwardProp: (prop) => !['priority'].includes(prop)
})<{ priority: string }>`
  background: rgba(0, 0, 0, 0.4);
  border-left: 4px solid ${props => {
    switch (props.priority) {
      case 'critical': return '#e74c3c';
      case 'high': return '#f39c12';
      case 'medium': return '#3498db';
      case 'low': return '#27ae60';
      default: return '#95a5a6';
    }
  }};
  border-radius: 0 6px 6px 0;
  margin-bottom: 12px;
  padding: 12px 16px;
  transition: all 0.3s ease;
  
  &:hover {
    background: rgba(0, 0, 0, 0.6);
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
  }
`

const LogTimestamp = styled.div`
  font-size: 11px;
  color: rgba(52, 152, 219, 0.8);
  margin-bottom: 4px;
  font-weight: 600;
`

const LogMessage = styled.div`
  font-size: 13px;
  color: rgba(255, 255, 255, 0.9);
  line-height: 1.4;
  margin-bottom: 6px;
`

const LogMetadata = styled.div`
  display: flex;
  gap: 12px;
  font-size: 10px;
  color: rgba(255, 255, 255, 0.6);
  
  .type {
    background: rgba(52, 152, 219, 0.3);
    padding: 2px 6px;
    border-radius: 3px;
    text-transform: uppercase;
    font-weight: 600;
  }
  
  .category {
    background: rgba(155, 89, 182, 0.3);
    padding: 2px 6px;
    border-radius: 3px;
  }
`

const EmptyState = styled.div`
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 200px;
  color: rgba(255, 255, 255, 0.5);
  font-size: 14px;
  text-align: center;
  
  &::before {
    content: '📋';
    font-size: 48px;
    margin-bottom: 16px;
    opacity: 0.3;
  }
`

type FilterType = 'all' | 'system' | 'forensics' | 'user' | 'case'

const Logs: React.FC = () => {
  const { timeEntries, addTimeEntry } = useTimeContext()
  const [filter, setFilter] = useState<FilterType>('all')
  const initializedRef = useRef(false)

  // Add some sample entries for demonstration
  useEffect(() => {
    if (!initializedRef.current) {
      const sampleEntry: TimeEntry = {
        id: 'login-1',
        timestamp: new Date(),
        type: 'system',
        message: 'Detective conectado ao sistema',
        category: 'Autenticação',
        priority: 'low'
      }
      
      addTimeEntry(sampleEntry)
      initializedRef.current = true
    }
  }, [addTimeEntry])

  const filteredEntries = filter === 'all' 
    ? timeEntries 
    : timeEntries.filter((entry: TimeEntry) => entry.type === filter)

  const formatTimestamp = (timestamp: Date) => {
    return timestamp.toLocaleString('pt-BR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    })
  }

  const getTypeIcon = (type: string) => {
    switch (type) {
      case 'system': return '⚙️'
      case 'forensics': return '🔬'
      case 'user': return '👤'
      case 'case': return '📋'
      default: return '📝'
    }
  }

  return (
    <LogsContainer>
      <LogsHeader>
        <HeaderTitle>Log da Investigação</HeaderTitle>
        <HeaderInfo>
          <div>Total de eventos: <span className="count">{timeEntries.length}</span></div>
          <div>Exibindo: <span className="count">{filteredEntries.length}</span></div>
        </HeaderInfo>
      </LogsHeader>
      
      <FilterContainer>
        <span style={{ fontSize: '11px', color: 'rgba(255, 255, 255, 0.7)' }}>Filtrar por:</span>
        {(['all', 'system', 'forensics', 'user', 'case'] as FilterType[]).map(type => (
          <FilterButton
            key={type}
            active={filter === type}
            onClick={() => setFilter(type)}
          >
            {type === 'all' ? 'Todos' : type === 'system' ? 'Sistema' : 
             type === 'forensics' ? 'Perícia' : type === 'user' ? 'Usuário' : 'Caso'}
          </FilterButton>
        ))}
      </FilterContainer>
      
      <LogsList>
        {filteredEntries.length === 0 ? (
          <EmptyState>
            <div>Nenhum evento encontrado</div>
            <div style={{ fontSize: '12px', marginTop: '8px', opacity: 0.7 }}>
              {filter === 'all' ? 'Aguardando eventos da investigação...' : `Nenhum evento do tipo "${filter}" encontrado`}
            </div>
          </EmptyState>
        ) : (
          filteredEntries.map((entry: TimeEntry) => (
            <LogEntry key={entry.id} priority={entry.priority || 'low'}>
              <LogTimestamp>
                {getTypeIcon(entry.type)} {formatTimestamp(entry.timestamp)}
              </LogTimestamp>
              <LogMessage>{entry.message}</LogMessage>
              <LogMetadata>
                <span className="type">{entry.type}</span>
                {entry.category && <span className="category">{entry.category}</span>}
                <span>Prioridade: {entry.priority || 'low'}</span>
              </LogMetadata>
            </LogEntry>
          ))
        )}
      </LogsList>
    </LogsContainer>
  )
}

export default Logs