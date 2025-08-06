import React, { useState } from 'react'
import styled from 'styled-components'
import { useTimeContext } from '../hooks/useTimeContext'

const ClockContainer = styled.div`
  position: absolute;
  top: 20px;
  right: 120px;
  background: rgba(0, 0, 0, 0.85);
  border: 2px solid rgba(52, 152, 219, 0.6);
  border-radius: 8px;
  padding: 12px 16px;
  color: rgba(255, 255, 255, 0.95);
  font-family: 'Courier New', monospace;
  font-weight: 600;
  backdrop-filter: blur(15px);
  box-shadow: 0 4px 15px rgba(0, 0, 0, 0.4);
  z-index: 100;
  cursor: pointer;
  transition: all 0.3s ease;
  user-select: none;

  &:hover {
    background: rgba(0, 0, 0, 0.9);
    border-color: rgba(52, 152, 219, 0.8);
    transform: translateY(-1px);
    box-shadow: 0 6px 20px rgba(52, 152, 219, 0.2);
  }

  &:active {
    transform: translateY(0);
  }

  @media (max-width: 768px) {
    right: 10px;
    padding: 8px 12px;
    font-size: 12px;
  }
`

const TimeDisplay = styled.div`
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
`

const TimeText = styled.div`
  font-size: 16px;
  color: rgba(255, 255, 255, 0.95);
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.8);
  
  @media (max-width: 768px) {
    font-size: 14px;
  }
`

const DateText = styled.div`
  font-size: 11px;
  color: rgba(52, 152, 219, 0.9);
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.8);
  
  @media (max-width: 768px) {
    font-size: 10px;
  }
`

const StatusIndicator = styled.div.withConfig({
  shouldForwardProp: (prop) => !['isRunning'].includes(prop)
})<{ isRunning: boolean }>`
  position: absolute;
  top: 4px;
  right: 4px;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: ${props => props.isRunning ? '#27ae60' : '#e74c3c'};
  box-shadow: 0 0 8px ${props => props.isRunning ? '#27ae60' : '#e74c3c'};
  animation: ${props => props.isRunning ? 'pulse 2s infinite' : 'none'};

  @keyframes pulse {
    0% { opacity: 1; }
    50% { opacity: 0.5; }
    100% { opacity: 1; }
  }
`

const ExpandedClock = styled.div`
  position: absolute;
  top: 60px;
  right: 0;
  background: rgba(0, 0, 0, 0.9);
  border: 2px solid rgba(52, 152, 219, 0.6);
  border-radius: 8px;
  padding: 16px;
  color: rgba(255, 255, 255, 0.95);
  font-family: 'Courier New', monospace;
  backdrop-filter: blur(15px);
  box-shadow: 0 8px 25px rgba(0, 0, 0, 0.5);
  z-index: 101;
  min-width: 220px;
  
  @media (max-width: 768px) {
    right: -80px;
    min-width: 200px;
  }
`

const ClockTitle = styled.div`
  font-size: 14px;
  font-weight: 600;
  color: rgba(52, 152, 219, 0.9);
  margin-bottom: 12px;
  text-align: center;
  border-bottom: 1px solid rgba(52, 152, 219, 0.3);
  padding-bottom: 8px;
`

const ClockInfo = styled.div`
  display: flex;
  flex-direction: column;
  gap: 8px;
`

const InfoRow = styled.div`
  display: flex;
  justify-content: space-between;
  font-size: 12px;
  
  .label {
    color: rgba(52, 152, 219, 0.8);
    font-weight: 600;
  }
  
  .value {
    color: rgba(255, 255, 255, 0.9);
  }
`

const Clock: React.FC = () => {
  const { getFormattedTime, getFormattedDate, isRunning, startTime, gameTime } = useTimeContext()
  const [isExpanded, setIsExpanded] = useState(false)

  const handleClick = () => {
    setIsExpanded(!isExpanded)
  }

  const getElapsedTime = () => {
    const elapsed = gameTime.getTime() - startTime.getTime()
    const hours = Math.floor(elapsed / (1000 * 60 * 60))
    const minutes = Math.floor((elapsed % (1000 * 60 * 60)) / (1000 * 60))
    return `${hours}h ${minutes}m`
  }

  return (
    <ClockContainer onClick={handleClick}>
      <StatusIndicator isRunning={isRunning} />
      <TimeDisplay>
        <TimeText>{getFormattedTime()}</TimeText>
        <DateText>{getFormattedDate()}</DateText>
      </TimeDisplay>
      
      {isExpanded && (
        <ExpandedClock>
          <ClockTitle>🕐 Sistema de Tempo</ClockTitle>
          <ClockInfo>
            <InfoRow>
              <span className="label">Status:</span>
              <span className="value">{isRunning ? 'Ativo' : 'Pausado'}</span>
            </InfoRow>
            <InfoRow>
              <span className="label">Início:</span>
              <span className="value">{startTime.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}</span>
            </InfoRow>
            <InfoRow>
              <span className="label">Tempo Decorrido:</span>
              <span className="value">{getElapsedTime()}</span>
            </InfoRow>
            <InfoRow>
              <span className="label">Velocidade:</span>
              <span className="value">30x tempo real</span>
            </InfoRow>
          </ClockInfo>
        </ExpandedClock>
      )}
    </ClockContainer>
  )
}

export default Clock