import React, { useState, useEffect } from 'react'
import styled from 'styled-components'
import { useTimeContext } from '../hooks/useTimeContext'
import { useCase } from '../hooks/useCaseContext'

const ClockContainer = styled.div`
  position: relative;
  background: rgba(0, 0, 0, 0.6);
  border: 2px solid rgba(52, 152, 219, 0.4);
  border-radius: 8px;
  padding: 8px 12px;
  color: rgba(255, 255, 255, 0.95);
  font-family: 'Courier New', monospace;
  font-weight: 600;
  backdrop-filter: blur(10px);
  box-shadow: 0 4px 15px rgba(0, 0, 0, 0.4);
  cursor: pointer;
  transition: all 0.3s ease;
  user-select: none;
  margin-left: auto;

  &:hover {
    background: rgba(52, 152, 219, 0.3);
    border-color: rgba(52, 152, 219, 0.8);
    transform: translateY(-4px);
    box-shadow: 0 8px 25px rgba(52, 152, 219, 0.3);
  }

  &:active {
    transform: translateY(-2px);
  }

  @media (max-width: 768px) {
    padding: 6px 8px;
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
  font-size: 14px;
  color: rgba(255, 255, 255, 0.95);
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.8);
  
  @media (max-width: 768px) {
    font-size: 12px;
  }
`

const DateText = styled.div`
  font-size: 10px;
  color: rgba(52, 152, 219, 0.9);
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.8);
  
  @media (max-width: 768px) {
    font-size: 9px;
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
  bottom: 80px;
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

const ForensicsBadge = styled.div<{ count: number }>`
  position: absolute;
  top: -8px;
  right: -8px;
  background: ${props => props.count > 0 ? 'linear-gradient(135deg, #f39c12 0%, #e67e22 100%)' : 'rgba(149, 165, 166, 0.3)'};
  color: white;
  font-size: 11px;
  font-weight: 700;
  border-radius: 50%;
  width: 20px;
  height: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.4);
  animation: ${props => props.count > 0 ? 'pulse 2s infinite' : 'none'};

  @keyframes pulse {
    0%, 100% {
      transform: scale(1);
      opacity: 1;
    }
    50% {
      transform: scale(1.1);
      opacity: 0.9;
    }
  }
`

const ForensicsInfo = styled.div`
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid rgba(52, 152, 219, 0.3);
`

const ForensicsTitle = styled.div`
  font-size: 12px;
  color: rgba(241, 196, 15, 0.9);
  margin-bottom: 8px;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 6px;
`

const Clock: React.FC = () => {
  const { getFormattedTime, getFormattedDate, isRunning, startTime, gameTime } = useTimeContext()
  const { getPendingForensicRequests, currentCase } = useCase()
  const [isExpanded, setIsExpanded] = useState(false)
  const [pendingCount, setPendingCount] = useState(0)

  useEffect(() => {
    const updatePendingCount = async () => {
      if (!currentCase) return
      
      try {
        const pending = await getPendingForensicRequests()
        setPendingCount(pending.length)
      } catch (error) {
        console.error('Error fetching pending forensics:', error)
      }
    }

    updatePendingCount()
    
    // Update every 30 seconds
    const interval = setInterval(updatePendingCount, 30000)
    return () => clearInterval(interval)
  }, [currentCase, getPendingForensicRequests])

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
      <ForensicsBadge count={pendingCount}>
        {pendingCount > 0 ? pendingCount : '✓'}
      </ForensicsBadge>
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
              <span className="value">60x tempo real</span>
            </InfoRow>
          </ClockInfo>
          
          {pendingCount > 0 && (
            <ForensicsInfo>
              <ForensicsTitle>
                🔬 Perícias em Andamento
              </ForensicsTitle>
              <InfoRow>
                <span className="label">Perícias Pendentes:</span>
                <span className="value" style={{ color: '#f39c12', fontWeight: '700' }}>
                  {pendingCount}
                </span>
              </InfoRow>
            </ForensicsInfo>
          )}
        </ExpandedClock>
      )}
    </ClockContainer>
  )
}

export default Clock