import React, { useEffect, useState } from 'react'
import styled from 'styled-components'
import { useCase } from '../../hooks/useCaseContext'
import { useTimeContext } from '../../hooks/useTimeContext'
import { forensicsService } from '../../services/forensicsService'
import type { ForensicRequest } from '../../types/case'

const Container = styled.div`
  width: 100%;
  height: 100%;
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  color: #fff;
  overflow-y: auto;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
`

const Header = styled.div`
  padding: 20px;
  background: rgba(0, 0, 0, 0.3);
  border-bottom: 2px solid rgba(52, 152, 219, 0.3);
  display: flex;
  align-items: center;
  gap: 12px;
`

const Title = styled.h2`
  margin: 0;
  font-size: 24px;
  color: #3498db;
  display: flex;
  align-items: center;
  gap: 10px;
`

const Content = styled.div`
  padding: 20px;
`

const Section = styled.div`
  margin-bottom: 30px;
`

const SectionTitle = styled.h3`
  font-size: 18px;
  color: #3498db;
  margin-bottom: 15px;
  padding-bottom: 8px;
  border-bottom: 1px solid rgba(52, 152, 219, 0.3);
`

const RequestCard = styled.div<{ status: string }>`
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid ${props => 
    props.status === 'completed' ? 'rgba(46, 204, 113, 0.4)' :
    props.status === 'in-progress' ? 'rgba(241, 196, 15, 0.4)' :
    'rgba(52, 152, 219, 0.4)'
  };
  border-radius: 8px;
  padding: 15px;
  margin-bottom: 12px;
  transition: all 0.3s ease;

  &:hover {
    background: rgba(255, 255, 255, 0.08);
    transform: translateX(5px);
  }
`

const RequestHeader = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
`

const EvidenceName = styled.div`
  font-size: 16px;
  font-weight: 600;
  color: #ecf0f1;
`

const AnalysisType = styled.div<{ type: string }>`
  padding: 4px 12px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 600;
  background: ${props => {
    switch (props.type) {
      case 'DNA': return 'rgba(231, 76, 60, 0.2)'
      case 'Fingerprint': return 'rgba(52, 152, 219, 0.2)'
      case 'DigitalForensics': return 'rgba(155, 89, 182, 0.2)'
      case 'Ballistics': return 'rgba(241, 196, 15, 0.2)'
      default: return 'rgba(149, 165, 166, 0.2)'
    }
  }};
  color: ${props => {
    switch (props.type) {
      case 'DNA': return '#e74c3c'
      case 'Fingerprint': return '#3498db'
      case 'DigitalForensics': return '#9b59b6'
      case 'Ballistics': return '#f1c40f'
      default: return '#95a5a6'
    }
  }};
`

const RequestDetails = styled.div`
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 8px;
  font-size: 13px;
  color: #bdc3c7;
`

const DetailItem = styled.div`
  display: flex;
  flex-direction: column;
  gap: 2px;
`

const DetailLabel = styled.span`
  font-size: 11px;
  color: #7f8c8d;
  text-transform: uppercase;
`

const DetailValue = styled.span<{ highlight?: boolean }>`
  color: ${props => props.highlight ? '#2ecc71' : '#ecf0f1'};
  font-weight: ${props => props.highlight ? '600' : '400'};
`

const TimeRemaining = styled.div<{ ready?: boolean }>`
  margin-top: 10px;
  padding: 8px;
  background: ${props => props.ready ? 'rgba(46, 204, 113, 0.2)' : 'rgba(52, 152, 219, 0.1)'};
  border-radius: 4px;
  text-align: center;
  font-size: 14px;
  font-weight: 600;
  color: ${props => props.ready ? '#2ecc71' : '#3498db'};
`

const ViewResultButton = styled.button`
  margin-top: 10px;
  width: 100%;
  padding: 10px;
  background: linear-gradient(135deg, #2ecc71 0%, #27ae60 100%);
  border: none;
  border-radius: 6px;
  color: white;
  font-weight: 600;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.3s ease;

  &:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 12px rgba(46, 204, 113, 0.4);
  }

  &:active {
    transform: translateY(0);
  }
`

const EmptyState = styled.div`
  text-align: center;
  padding: 40px 20px;
  color: #7f8c8d;
  font-size: 14px;
`

const LoadingState = styled.div`
  text-align: center;
  padding: 40px 20px;
  color: #3498db;
  font-size: 14px;
`

const ForensicsQueue: React.FC = () => {
  const { currentCase } = useCase()
  const { gameTime } = useTimeContext()
  const [requests, setRequests] = useState<ForensicRequest[]>([])
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    loadRequests()
    
    // Refresh every 10 seconds
    const interval = setInterval(loadRequests, 10000)
    return () => clearInterval(interval)
  }, [currentCase])

  const loadRequests = async () => {
    if (!currentCase) return

    try {
      const allRequests = await forensicsService.getForensicRequests(currentCase)
      setRequests(allRequests)
    } catch (error) {
      console.error('Error loading forensic requests:', error)
    } finally {
      setIsLoading(false)
    }
  }

  const getTypeLabel = (type: string): string => {
    switch (type) {
      case 'DNA': return 'DNA'
      case 'Fingerprint': return 'Impressões Digitais'
      case 'DigitalForensics': return 'Perícia Digital'
      case 'Ballistics': return 'Balística'
      default: return type
    }
  }

  const formatDateTime = (date: Date): string => {
    return date.toLocaleString('pt-BR', {
      day: '2-digit',
      month: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    })
  }

  const handleViewResult = (request: ForensicRequest) => {
    if (request.resultDocumentId) {
      // Open document viewer with the result
      console.log('Opening result:', request.resultDocumentId)
      // TODO: Integrate with document viewer
    }
  }

  const pendingRequests = requests.filter(r => r.status === 'pending' || r.status === 'in-progress')
  const completedRequests = requests.filter(r => r.status === 'completed')

  if (isLoading) {
    return (
      <Container>
        <Header>
          <Title>🔬 Fila de Perícias</Title>
        </Header>
        <LoadingState>Carregando perícias...</LoadingState>
      </Container>
    )
  }

  return (
    <Container>
      <Header>
        <Title>
          🔬 Fila de Perícias
          {pendingRequests.length > 0 && (
            <span style={{ fontSize: '16px', color: '#f39c12' }}>
              ({pendingRequests.length} pendente{pendingRequests.length !== 1 ? 's' : ''})
            </span>
          )}
        </Title>
      </Header>

      <Content>
        {/* Pending Requests */}
        <Section>
          <SectionTitle>⏳ Em Andamento</SectionTitle>
          {pendingRequests.length === 0 ? (
            <EmptyState>Nenhuma perícia em andamento</EmptyState>
          ) : (
            pendingRequests.map(request => {
              const timeRemaining = forensicsService.getFormattedTimeRemaining(request, gameTime)
              const isReady = timeRemaining === 'Ready'

              return (
                <RequestCard key={request.id} status={request.status}>
                  <RequestHeader>
                    <EvidenceName>{request.evidenceName}</EvidenceName>
                    <AnalysisType type={request.analysisType}>
                      {getTypeLabel(request.analysisType)}
                    </AnalysisType>
                  </RequestHeader>

                  <RequestDetails>
                    <DetailItem>
                      <DetailLabel>Solicitada em</DetailLabel>
                      <DetailValue>{formatDateTime(request.requestedAt)}</DetailValue>
                    </DetailItem>
                    <DetailItem>
                      <DetailLabel>Conclusão prevista</DetailLabel>
                      <DetailValue>{formatDateTime(request.estimatedCompletionTime)}</DetailValue>
                    </DetailItem>
                  </RequestDetails>

                  <TimeRemaining ready={isReady}>
                    {isReady ? '✅ Pronta para visualização' : `⏱️ ${timeRemaining} restantes`}
                  </TimeRemaining>

                  {request.notes && (
                    <DetailItem style={{ marginTop: '10px' }}>
                      <DetailLabel>Observações</DetailLabel>
                      <DetailValue>{request.notes}</DetailValue>
                    </DetailItem>
                  )}
                </RequestCard>
              )
            })
          )}
        </Section>

        {/* Completed Requests */}
        {completedRequests.length > 0 && (
          <Section>
            <SectionTitle>✅ Concluídas</SectionTitle>
            {completedRequests.map(request => (
              <RequestCard key={request.id} status={request.status}>
                <RequestHeader>
                  <EvidenceName>{request.evidenceName}</EvidenceName>
                  <AnalysisType type={request.analysisType}>
                    {getTypeLabel(request.analysisType)}
                  </AnalysisType>
                </RequestHeader>

                <RequestDetails>
                  <DetailItem>
                    <DetailLabel>Solicitada em</DetailLabel>
                    <DetailValue>{formatDateTime(request.requestedAt)}</DetailValue>
                  </DetailItem>
                  <DetailItem>
                    <DetailLabel>Concluída em</DetailLabel>
                    <DetailValue highlight>
                      {request.completedAt ? formatDateTime(request.completedAt) : 'N/A'}
                    </DetailValue>
                  </DetailItem>
                </RequestDetails>

                {request.resultDocumentId && (
                  <ViewResultButton onClick={() => handleViewResult(request)}>
                    📄 Ver Resultado
                  </ViewResultButton>
                )}
              </RequestCard>
            ))}
          </Section>
        )}
      </Content>
    </Container>
  )
}

export default ForensicsQueue
