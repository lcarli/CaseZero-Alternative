import React, { useState } from 'react'
import styled from 'styled-components'
import { useCase } from '../../hooks/useCaseContext'

const EmailContainer = styled.div`
  height: 100%;
  display: flex;
  flex-direction: column;
`

const TwoColumnLayout = styled.div`
  display: flex;
  height: 100%;
  gap: 1rem;
`

const LeftPanel = styled.div`
  width: 350px;
  min-width: 300px;
  max-width: 450px;
  display: flex;
  flex-direction: column;
  border-right: 1px solid rgba(255, 255, 255, 0.1);
  padding-right: 1rem;
`

const RightPanel = styled.div`
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
`

const EmailList = styled.div`
  flex: 1;
  overflow-y: auto;
`

const EmailItem = styled.div<{ $isSelected: boolean; $isUnread: boolean }>`
  padding: 0.75rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  cursor: pointer;
  transition: all 0.2s ease;
  background: ${props => props.$isSelected ? 'rgba(74, 158, 255, 0.2)' : 'transparent'};
  border-left: ${props => props.$isUnread ? '3px solid #4a9eff' : '3px solid transparent'};

  &:hover {
    background: rgba(255, 255, 255, 0.05);
  }
`

const EmailHeader = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.5rem;
`

const EmailSender = styled.span<{ $isUnread: boolean }>`
  font-weight: ${props => props.$isUnread ? '600' : '400'};
  color: ${props => props.$isUnread ? '#4a9eff' : 'white'};
`

const EmailTime = styled.span`
  font-size: 12px;
  color: rgba(255, 255, 255, 0.6);
`

const EmailSubject = styled.div<{ $isUnread: boolean }>`
  font-weight: ${props => props.$isUnread ? '500' : '400'};
  color: ${props => props.$isUnread ? 'white' : 'rgba(255, 255, 255, 0.8)'};
  margin-bottom: 0.25rem;
`

const EmailPreview = styled.div`
  font-size: 13px;
  color: rgba(255, 255, 255, 0.6);
  overflow: hidden;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
`

const EmailContent = styled.div`
  flex: 1;
  padding: 1rem;
  background: rgba(0, 0, 0, 0.2);
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  overflow-y: auto;
`

const AttachmentList = styled.div`
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
`

const AttachmentItem = styled.div`
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.5rem;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 4px;
  margin-bottom: 0.5rem;
  transition: background 0.2s ease;

  &:hover {
    background: rgba(255, 255, 255, 0.1);
  }
`

const AttachmentInfo = styled.div`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex: 1;
`

const AttachmentName = styled.span`
  font-weight: 500;
  color: #4a9eff;
`

const AttachmentMeta = styled.span`
  font-size: 12px;
  color: rgba(255, 255, 255, 0.6);
`

const AttachmentButton = styled.button`
  background: #4a9eff;
  color: white;
  border: none;
  border-radius: 4px;
  padding: 0.25rem 0.5rem;
  font-size: 12px;
  cursor: pointer;
  transition: background 0.2s ease;

  &:hover {
    background: #357abd;
  }
`

const PriorityBadge = styled.span<{ $priority: 'high' | 'medium' | 'low' }>`
  padding: 2px 6px;
  border-radius: 3px;
  font-size: 10px;
  font-weight: 500;
  text-transform: uppercase;
  background: ${props => {
    switch (props.$priority) {
      case 'high': return '#ff4757'
      case 'medium': return '#ffa502'
      case 'low': return '#2ed573'
      default: return '#747d8c'
    }
  }};
  color: white;
  margin-left: 0.5rem;
`

interface AttachmentData {
  name: string
  size: string
  type: string
}

interface EmailData {
  id: string
  sender: string
  subject: string
  preview: string
  content: string
  time: string
  isUnread: boolean
  priority: 'high' | 'medium' | 'low'
  attachments?: AttachmentData[]
}

const Email: React.FC = () => {
  const [selectedEmail, setSelectedEmail] = useState<string | null>('case001_email1')
  const { currentCase } = useCase()

  const getCaseEmails = (caseId: string): EmailData[] => {
    const caseEmails: { [key: string]: EmailData[] } = {
      'CASE-2024-001': [
        {
          id: 'case001_email1',
          sender: 'Chief Johnson',
          subject: 'URGENT: Case Assignment #001',
          preview: 'New high-priority case has been assigned to your unit. Downtown office break-in...',
          content: `Officer,

A new high-priority case has been assigned to your unit. Case #001 involves a break-in at the Downtown Office Building at 123 Main Street.

Key Details:
- Incident occurred at 02:30 AM on January 15, 2024
- Security system was compromised
- Suspect appeared to be searching for specific documents
- No random theft - electronics and cash left untouched

Please review the attached case files and begin investigation immediately.

Evidence files have been uploaded to the system for your review.

Chief Johnson
Metropolitan Police Department

[Email Source: /cases/CASE-2024-001/emails/case001_email1.json]`,
          time: '2 hours ago',
          isUnread: true,
          priority: 'high',
          attachments: [
            { name: 'case001.txt', size: '2.4 KB', type: 'text' },
            { name: 'evidence.jpg', size: '1.2 MB', type: 'image' }
          ]
        },
        {
          id: 'case001_email2',
          sender: 'Forensics Lab',
          subject: 'DNA Results - Case #001',
          preview: 'DNA analysis complete for evidence submitted from downtown break-in...',
          content: `Lab Report - Case #001

DNA analysis has been completed for the evidence submitted from the downtown office break-in.

Results Summary:
- Sample from window frame: Partial match found (85% confidence)
- Subject: David Thompson, DOB: 03/15/1987
- Criminal History: Minor theft (2019), Breaking & Entering (2021)
- Current Status: On probation

Recommendations:
1. Interview David Thompson regarding whereabouts on January 15, 2024
2. Obtain fresh DNA sample for definitive comparison
3. Check alibi and known associates

Full detailed report is attached for your review.

Dr. Emily Chen, PhD
Metropolitan Forensics Laboratory

[Email Source: /cases/CASE-2024-001/emails/case001_email2.json]`,
          time: '1 day ago',
          isUnread: false,
          priority: 'medium',
          attachments: [
            { name: 'dna_results.txt', size: '3.7 KB', type: 'text' }
          ]
        },
        {
          id: 'case001_email3',
          sender: 'Detective Sarah Johnson',
          subject: 'Witness Statement Available',
          preview: 'Night security guard witness statement has been processed and is ready for review...',
          content: `Case Update - Witness Statement

The witness statement from John Matthews, the night security guard, has been processed and is now available in the case files.

Key points from the statement:
- Suspect appeared familiar with building layout
- Individual avoided several camera angles
- Carried something when leaving that wasn't visible when entering
- Guard had never witnessed anything similar in 3 years of employment

The complete witness statement PDF is attached and also available in the case file system.

Detective Sarah Johnson
Investigating Officer

[Email Source: /cases/CASE-2024-001/emails/case001_email3.json]`,
          time: '2 days ago',
          isUnread: false,
          priority: 'medium',
          attachments: [
            { name: 'witness_statement.pdf', size: '156 KB', type: 'pdf' }
          ]
        }
      ],
      'CASE-2024-002': [
        {
          id: 'case002_email1',
          sender: 'Chefe de Polícia',
          subject: 'URGENTE: Roubo na Clínica - Caso #002',
          preview: 'Novo caso de alta prioridade: roubo em clínica médica com evidências forenses...',
          content: `Detetive,

Um novo caso de alta prioridade foi atribuído à sua unidade. Caso #002 envolve um roubo na Clínica Médica São Lucas no Bairro Jardins.

Detalhes Principais:
- Incidente ocorreu na madrugada de 05 de Fevereiro de 2024
- Cofre arrombado com documentos confidenciais furtados
- Sem sinais de arrombamento na entrada principal
- Evidências sugerem envolvimento de funcionário interno

A cena foi preservada e evidências foram coletadas:
- Fio de cabelo loiro
- Imagens de câmera de segurança
- Impressões digitais parciais

Três funcionários estão sendo investigados como pessoas de interesse.

Por favor, inicie a investigação imediatamente e mantenha-me informado sobre o progresso.

Chefe de Polícia Maria Santos
Departamento de Polícia Metropolitana

[Email Fonte: /cases/CASE-2024-002/emails/case002_email1.json]`,
          time: '3 horas atrás',
          isUnread: true,
          priority: 'high',
          attachments: [
            { name: 'relatorio_inicial_clinica.pdf', size: '3.1 KB', type: 'pdf' },
            { name: 'fio_cabelo_loiro.jpg', size: '890 KB', type: 'image' }
          ]
        },
        {
          id: 'case002_email2',
          sender: 'Laboratório Forense',
          subject: 'Resultados de DNA - Caso #002',
          preview: 'Análise de DNA concluída para cabelo encontrado na clínica. Correspondência positiva...',
          content: `Relatório de Laboratório - Caso #002

A análise de DNA foi concluída para a evidência submetida do roubo na clínica.

Resumo dos Resultados:
- Amostra do fio de cabelo: Correspondência positiva encontrada (99.7% confiança)
- Suspeita: Joana Duarte, DOB: 15/04/1995
- Status: Funcionária da clínica (Enfermeira Chefe)
- Histórico: Acesso total às instalações

Esta é uma correspondência estatisticamente significativa para identificação positiva.

Recomendações Urgentes:
1. Interrogar Joana Duarte imediatamente
2. Verificar álibi detalhadamente
3. Obter mandado de busca se necessário

O relatório detalhado está anexado para sua revisão.

Dr. Patricia Santos, PhD
Laboratório Forense Metropolitano

[Email Fonte: /cases/CASE-2024-002/emails/case002_email2.json]`,
          time: '1 dia atrás',
          isUnread: false,
          priority: 'high',
          attachments: [
            { name: 'dna_cabelo_resultado.pdf', size: '2.8 KB', type: 'pdf' }
          ]
        },
        {
          id: 'case002_email3',
          sender: 'Segurança da Clínica',
          subject: 'Depoimento sobre Avistamento Suspeito',
          preview: 'Segurança noturno relatou ter visto mulher loira saindo pela porta dos fundos...',
          content: `Atualização do Caso - Depoimento de Testemunha

O depoimento do segurança noturno da clínica foi processado e está disponível nos arquivos do caso.

Pontos-chave do depoimento:
- Mulher loira vista saindo pela porta dos fundos às 03:45
- Suspeita parecia conhecer o código da porta de emergência
- Estava vestindo jaleco médico branco
- Comportamento suspeito - tentando evitar câmeras

O segurança trabalha na clínica há 2 anos e nunca havia presenciado algo similar.

Este depoimento corrobora com as evidências de câmera de segurança coletadas.

Oficial de Segurança João Martinez
Clínica Médica São Lucas

[Email Fonte: /cases/CASE-2024-002/emails/case002_email3.json]`,
          time: '2 dias atrás',
          isUnread: false,
          priority: 'medium',
          attachments: [
            { name: 'depoimento_seguranca.pdf', size: '145 KB', type: 'pdf' }
          ]
        }
      ],
      'CASE-2024-003': [
        {
          id: 'case003_email1',
          sender: 'Sistema CaseZero',
          subject: 'Caso de Demonstração Ativo',
          preview: 'Este é um email de demonstração para mostrar a funcionalidade do sistema...',
          content: `Email de Demonstração - Sistema CaseZero

Este é um email automático gerado para demonstrar a funcionalidade do sistema de casos.

Funcionalidades Demonstradas:
- Emails específicos por caso
- Sistema de prioridades
- Anexos por email
- Interface responsiva

Este caso (CASE-2024-003) serve para:
1. Validar o carregamento automático de casos
2. Testar a interface de seleção
3. Demonstrar componentes independentes
4. Verificar navegação entre casos

O sistema está funcionando corretamente se você consegue ver este email apenas quando o CASE-2024-003 está selecionado.

Sistema CaseZero v1.0
Departamento de Desenvolvimento

[Email Fonte: /cases/CASE-2024-003/emails/case003_email1.json]`,
          time: '1 hora atrás',
          isUnread: true,
          priority: 'low',
          attachments: [
            { name: 'caso_demo.txt', size: '1.2 KB', type: 'text' }
          ]
        }
      ]
    }
    
    return caseEmails[caseId] || [
      {
        id: 'no_case_email',
        sender: 'Sistema',
        subject: 'Nenhum caso selecionado',
        preview: 'Selecione um caso no dashboard para visualizar emails específicos...',
        content: `Nenhum Caso Ativo

Atualmente não há nenhum caso selecionado. 

Para visualizar emails específicos de um caso:
1. Volte ao dashboard
2. Selecione um caso disponível
3. Retorne ao sistema de email

Os emails são específicos para cada caso e mostram comunicações relevantes para a investigação em andamento.

Sistema CaseZero`,
        time: 'Agora',
        isUnread: false,
        priority: 'low',
        attachments: []
      }
    ]
  }

  const emails = getCaseEmails(currentCase || '')

  const handleEmailClick = (emailId: string) => {
    setSelectedEmail(emailId)
  }

  const handleAttachmentOpen = (attachmentName: string) => {
    // This will trigger opening the file in FileViewer
    // For now, we'll just log it - this would need to be implemented with inter-component communication
    console.log(`Opening file: ${attachmentName} in FileViewer`)
    alert(`File "${attachmentName}" would be opened in the File Viewer component.`)
  }

  const selectedEmailData = emails.find(email => email.id === selectedEmail)

  return (
    <EmailContainer>
      <h3 style={{ margin: '0 0 1rem 0' }}>Police Email System - {currentCase || 'No Case'}</h3>
      
      <TwoColumnLayout>
        <LeftPanel>
          <h4 style={{ margin: '0 0 1rem 0', color: '#4a9eff' }}>Inbox</h4>
          <EmailList>
            {emails.map(email => (
              <EmailItem
                key={email.id}
                $isSelected={email.id === selectedEmail}
                $isUnread={email.isUnread}
                onClick={() => handleEmailClick(email.id)}
              >
                <EmailHeader>
                  <EmailSender $isUnread={email.isUnread}>
                    {email.sender}
                    <PriorityBadge $priority={email.priority}>
                      {email.priority}
                    </PriorityBadge>
                  </EmailSender>
                  <EmailTime>{email.time}</EmailTime>
                </EmailHeader>
                <EmailSubject $isUnread={email.isUnread}>
                  {email.subject}
                </EmailSubject>
                <EmailPreview>
                  {email.preview}
                  {email.attachments && email.attachments.length > 0 && (
                    <span style={{ marginLeft: '0.5rem', color: '#4a9eff' }}>
                      📎 {email.attachments.length} attachment{email.attachments.length > 1 ? 's' : ''}
                    </span>
                  )}
                </EmailPreview>
              </EmailItem>
            ))}
          </EmailList>
        </LeftPanel>

        <RightPanel>
          {selectedEmailData ? (
            <EmailContent>
              <h4 style={{ margin: '0 0 0.5rem 0', color: '#4a9eff' }}>
                {selectedEmailData.subject}
              </h4>
              <p style={{ margin: '0 0 1rem 0', fontSize: '12px', color: 'rgba(255, 255, 255, 0.6)' }}>
                From: {selectedEmailData.sender} • {selectedEmailData.time}
              </p>
              <pre style={{ 
                margin: 0, 
                whiteSpace: 'pre-wrap', 
                fontFamily: 'inherit', 
                fontSize: '14px', 
                lineHeight: '1.5',
                color: 'rgba(255, 255, 255, 0.9)'
              }}>
                {selectedEmailData.content}
              </pre>
              
              {selectedEmailData.attachments && selectedEmailData.attachments.length > 0 && (
                <AttachmentList>
                  <h5 style={{ margin: '0 0 0.5rem 0', color: 'rgba(255, 255, 255, 0.8)', fontSize: '13px' }}>
                    Attachments ({selectedEmailData.attachments.length})
                  </h5>
                  {selectedEmailData.attachments.map((attachment, index) => (
                    <AttachmentItem key={index}>
                      <AttachmentInfo>
                        <span style={{ fontSize: '16px' }}>
                          {attachment.type === 'image' ? '🖼️' : 
                           attachment.type === 'pdf' ? '📋' : '📄'}
                        </span>
                        <div>
                          <AttachmentName>{attachment.name}</AttachmentName>
                          <div>
                            <AttachmentMeta>{attachment.size} • {attachment.type.toUpperCase()}</AttachmentMeta>
                          </div>
                        </div>
                      </AttachmentInfo>
                      <AttachmentButton onClick={() => handleAttachmentOpen(attachment.name)}>
                        Open in File Viewer
                      </AttachmentButton>
                    </AttachmentItem>
                  ))}
                </AttachmentList>
              )}
            </EmailContent>
          ) : (
            <div style={{ 
              flex: 1, 
              display: 'flex', 
              alignItems: 'center', 
              justifyContent: 'center',
              color: 'rgba(255, 255, 255, 0.6)',
              fontSize: '16px'
            }}>
              Select an email to view its contents
            </div>
          )}
        </RightPanel>
      </TwoColumnLayout>
    </EmailContainer>
  )
}

export default Email