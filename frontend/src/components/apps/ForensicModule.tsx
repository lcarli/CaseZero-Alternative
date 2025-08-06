import React, { useState, useEffect } from 'react'
import styled from 'styled-components'
import { useCase } from '../../hooks/useCaseContext'

const ForensicContainer = styled.div`
  height: 100%;
  display: flex;
  flex-direction: column;
`

const TabBar = styled.div`
  display: flex;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  margin-bottom: 1rem;
`

const Tab = styled.button<{ $active: boolean }>`
  padding: 0.75rem 1.5rem;
  background: ${props => props.$active ? 'rgba(52, 152, 219, 0.3)' : 'transparent'};
  border: none;
  border-bottom: 2px solid ${props => props.$active ? '#3498db' : 'transparent'};
  color: ${props => props.$active ? '#fff' : 'rgba(255, 255, 255, 0.7)'};
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 14px;
  font-weight: 500;

  &:hover {
    background: rgba(52, 152, 219, 0.2);
    color: #fff;
  }
`

const TabContent = styled.div`
  flex: 1;
  overflow-y: auto;
  padding: 0 0.5rem;
`

const SectionTitle = styled.h3`
  margin: 0 0 1rem 0;
  color: #4a9eff;
  font-size: 18px;
  display: flex;
  align-items: center;
  gap: 0.5rem;
`

const AnalysisCard = styled.div`
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  padding: 1rem;
  margin-bottom: 1rem;
  transition: border-color 0.2s ease;

  &:hover {
    border-color: rgba(52, 152, 219, 0.5);
  }
`

const AnalysisHeader = styled.div`
  display: flex;
  justify-content: between;
  align-items: center;
  margin-bottom: 0.5rem;
`

const AnalysisTitle = styled.h4`
  margin: 0;
  color: #fff;
  font-size: 16px;
`

const AnalysisStatus = styled.span<{ $status: 'pending' | 'completed' | 'processing' }>`
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
  text-transform: uppercase;
  background: ${props => {
    switch(props.$status) {
      case 'pending': return 'rgba(255, 193, 7, 0.2)'
      case 'processing': return 'rgba(52, 152, 219, 0.2)'
      case 'completed': return 'rgba(40, 167, 69, 0.2)'
      default: return 'rgba(108, 117, 125, 0.2)'
    }
  }};
  color: ${props => {
    switch(props.$status) {
      case 'pending': return '#ffc107'
      case 'processing': return '#3498db'
      case 'completed': return '#28a745'
      default: return '#6c757d'
    }
  }};
`

const ProgressBar = styled.div`
  width: 100%;
  height: 8px;
  background: rgba(255, 255, 255, 0.1);
  border-radius: 4px;
  margin: 0.5rem 0;
  overflow: hidden;
`

const ProgressFill = styled.div<{ $progress: number }>`
  height: 100%;
  background: linear-gradient(90deg, #3498db, #2ecc71);
  width: ${props => props.$progress}%;
  transition: width 0.3s ease;
`

const AnalysisInfo = styled.div`
  font-size: 14px;
  color: rgba(255, 255, 255, 0.8);
  line-height: 1.4;
`

const TimeRemaining = styled.div`
  font-size: 12px;
  color: #3498db;
  margin-top: 0.5rem;
  font-weight: 500;
`

const EmptyState = styled.div`
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 200px;
  color: rgba(255, 255, 255, 0.6);
  text-align: center;
  gap: 1rem;
`

const SelectionForm = styled.div`
  background: rgba(0, 0, 0, 0.2);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  padding: 1.5rem;
  margin-bottom: 1rem;
`

const FormGroup = styled.div`
  margin-bottom: 1rem;
`

const Label = styled.label`
  display: block;
  margin-bottom: 0.5rem;
  color: #4a9eff;
  font-weight: 500;
`

const Select = styled.select`
  width: 100%;
  padding: 0.5rem;
  background: rgba(0, 0, 0, 0.5);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px;
  color: #fff;
  font-size: 14px;

  &:focus {
    outline: none;
    border-color: #3498db;
  }

  option {
    background: #1a1a2e;
    color: #fff;
  }
`

const SubmitButton = styled.button`
  background: linear-gradient(135deg, #3498db, #2ecc71);
  border: none;
  border-radius: 6px;
  padding: 0.75rem 1.5rem;
  color: white;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;

  &:hover {
    transform: translateY(-1px);
    box-shadow: 0 4px 15px rgba(52, 152, 219, 0.3);
  }

  &:disabled {
    background: rgba(108, 117, 125, 0.3);
    cursor: not-allowed;
    transform: none;
    box-shadow: none;
  }
`

const ResultsButton = styled.button`
  background: rgba(40, 167, 69, 0.2);
  border: 1px solid #28a745;
  border-radius: 4px;
  padding: 0.5rem 1rem;
  color: #28a745;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s ease;
  margin-top: 0.5rem;

  &:hover {
    background: rgba(40, 167, 69, 0.3);
  }
`

interface AnalysisItem {
  id: string
  evidenceName: string
  analysisType: string
  status: 'pending' | 'processing' | 'completed'
  startTime: number
  duration: number // in minutes
  progress: number
  result?: string
}

const ANALYSIS_TYPES = {
  'fingerprint': { name: 'Impressão Digital', duration: 30, icon: '🔍' },
  'dna': { name: 'Análise de DNA', duration: 120, icon: '🧬' },
  'ballistics': { name: 'Balística', duration: 90, icon: '🎯' },
  'chemical': { name: 'Análise Química', duration: 180, icon: '🧪' },
  'video': { name: 'Análise de Vídeo', duration: 45, icon: '📹' },
  'metadata': { name: 'Análise de Metadados', duration: 15, icon: '💾' }
}

const ForensicModule: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'processing' | 'completed' | 'request'>('processing')
  const [analyses, setAnalyses] = useState<AnalysisItem[]>([])
  const [selectedEvidence, setSelectedEvidence] = useState('')
  const [selectedAnalysisType, setSelectedAnalysisType] = useState('')
  const { currentCase } = useCase()

  // Sample evidence items from the current case
  const availableEvidence = [
    'evidence.jpg - Security Camera Footage',
    'witness_statement.pdf - Witness Statement', 
    'dna_sample.txt - DNA Sample from Window',
    'fingerprints.jpg - Fingerprints from Door',
    'security_footage.mp4 - Mall Security Video',
    'unknown_substance.txt - Unknown White Powder'
  ]

  // Update progress for processing analyses
  useEffect(() => {
    const interval = setInterval(() => {
      setAnalyses(prev => prev.map(analysis => {
        if (analysis.status === 'processing') {
          const now = Date.now()
          const elapsed = (now - analysis.startTime) / (1000 * 60) // minutes
          const progress = Math.min((elapsed / analysis.duration) * 100, 100)
          
          if (progress >= 100) {
            return {
              ...analysis,
              status: 'completed' as const,
              progress: 100,
              result: `forensic_result_${analysis.id}.pdf`
            }
          }
          
          return { ...analysis, progress }
        }
        return analysis
      }))
    }, 1000)

    return () => clearInterval(interval)
  }, [])

  const handleSubmitAnalysis = () => {
    if (!selectedEvidence || !selectedAnalysisType) return

    const newAnalysis: AnalysisItem = {
      id: Date.now().toString(),
      evidenceName: selectedEvidence,
      analysisType: selectedAnalysisType,
      status: 'processing',
      startTime: Date.now(),
      duration: ANALYSIS_TYPES[selectedAnalysisType as keyof typeof ANALYSIS_TYPES].duration,
      progress: 0
    }

    setAnalyses(prev => [...prev, newAnalysis])
    setSelectedEvidence('')
    setSelectedAnalysisType('')
    setActiveTab('processing')
  }

  const formatTimeRemaining = (analysis: AnalysisItem) => {
    const elapsed = (Date.now() - analysis.startTime) / (1000 * 60)
    const remaining = Math.max(0, analysis.duration - elapsed)
    
    if (remaining === 0) return 'Concluído'
    
    const hours = Math.floor(remaining / 60)
    const minutes = Math.ceil(remaining % 60)
    
    if (hours > 0) {
      return `${hours}h ${minutes}m restantes`
    }
    return `${minutes}m restantes`
  }

  const processingAnalyses = analyses.filter(a => a.status === 'processing')
  const completedAnalyses = analyses.filter(a => a.status === 'completed')

  return (
    <ForensicContainer>
      <SectionTitle>
        🔬 Módulo de Perícia - {currentCase || 'No Case'}
      </SectionTitle>
      
      <TabBar>
        <Tab 
          $active={activeTab === 'processing'} 
          onClick={() => setActiveTab('processing')}
        >
          Em Análise ({processingAnalyses.length})
        </Tab>
        <Tab 
          $active={activeTab === 'completed'} 
          onClick={() => setActiveTab('completed')}
        >
          Concluídas ({completedAnalyses.length})
        </Tab>
        <Tab 
          $active={activeTab === 'request'} 
          onClick={() => setActiveTab('request')}
        >
          Solicitar Nova Análise
        </Tab>
      </TabBar>

      <TabContent>
        {activeTab === 'processing' && (
          <>
            {processingAnalyses.length === 0 ? (
              <EmptyState>
                <div style={{ fontSize: '48px' }}>⏳</div>
                <div>Nenhuma análise em andamento</div>
                <div style={{ fontSize: '12px' }}>
                  Use a aba "Solicitar Nova Análise" para enviar evidências para perícia
                </div>
              </EmptyState>
            ) : (
              processingAnalyses.map(analysis => (
                <AnalysisCard key={analysis.id}>
                  <AnalysisHeader>
                    <AnalysisTitle>
                      {ANALYSIS_TYPES[analysis.analysisType as keyof typeof ANALYSIS_TYPES]?.icon} {' '}
                      {ANALYSIS_TYPES[analysis.analysisType as keyof typeof ANALYSIS_TYPES]?.name}
                    </AnalysisTitle>
                    <AnalysisStatus $status={analysis.status}>
                      Processando
                    </AnalysisStatus>
                  </AnalysisHeader>
                  <AnalysisInfo>
                    <strong>Evidência:</strong> {analysis.evidenceName}
                  </AnalysisInfo>
                  <ProgressBar>
                    <ProgressFill $progress={analysis.progress} />
                  </ProgressBar>
                  <TimeRemaining>
                    {formatTimeRemaining(analysis)}
                  </TimeRemaining>
                </AnalysisCard>
              ))
            )}
          </>
        )}

        {activeTab === 'completed' && (
          <>
            {completedAnalyses.length === 0 ? (
              <EmptyState>
                <div style={{ fontSize: '48px' }}>📋</div>
                <div>Nenhuma análise concluída</div>
                <div style={{ fontSize: '12px' }}>
                  Análises concluídas aparecerão aqui
                </div>
              </EmptyState>
            ) : (
              completedAnalyses.map(analysis => (
                <AnalysisCard key={analysis.id}>
                  <AnalysisHeader>
                    <AnalysisTitle>
                      {ANALYSIS_TYPES[analysis.analysisType as keyof typeof ANALYSIS_TYPES]?.icon} {' '}
                      {ANALYSIS_TYPES[analysis.analysisType as keyof typeof ANALYSIS_TYPES]?.name}
                    </AnalysisTitle>
                    <AnalysisStatus $status={analysis.status}>
                      Concluída
                    </AnalysisStatus>
                  </AnalysisHeader>
                  <AnalysisInfo>
                    <strong>Evidência:</strong> {analysis.evidenceName}
                  </AnalysisInfo>
                  <ResultsButton>
                    📄 Ver Resultado ({analysis.result})
                  </ResultsButton>
                </AnalysisCard>
              ))
            )}
          </>
        )}

        {activeTab === 'request' && (
          <SelectionForm>
            <FormGroup>
              <Label>Selecionar Evidência:</Label>
              <Select 
                value={selectedEvidence} 
                onChange={(e) => setSelectedEvidence(e.target.value)}
              >
                <option value="">-- Escolha uma evidência --</option>
                {availableEvidence.map(evidence => (
                  <option key={evidence} value={evidence}>
                    {evidence}
                  </option>
                ))}
              </Select>
            </FormGroup>

            <FormGroup>
              <Label>Tipo de Análise:</Label>
              <Select 
                value={selectedAnalysisType} 
                onChange={(e) => setSelectedAnalysisType(e.target.value)}
              >
                <option value="">-- Escolha o tipo de análise --</option>
                {Object.entries(ANALYSIS_TYPES).map(([key, type]) => (
                  <option key={key} value={key}>
                    {type.icon} {type.name} (~{type.duration}min)
                  </option>
                ))}
              </Select>
            </FormGroup>

            <SubmitButton 
              disabled={!selectedEvidence || !selectedAnalysisType}
              onClick={handleSubmitAnalysis}
            >
              📤 Enviar para Análise
            </SubmitButton>
          </SelectionForm>
        )}
      </TabContent>
    </ForensicContainer>
  )
}

export default ForensicModule