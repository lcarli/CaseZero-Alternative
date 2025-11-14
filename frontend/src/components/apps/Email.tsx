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

const EmailItem = styled.div<{ $isSelected: boolean; $isUnread: boolean; $isLocked: boolean }>`
  padding: 0.75rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  cursor: ${props => props.$isLocked ? 'not-allowed' : 'pointer'};
  transition: all 0.2s ease;
  background: ${props => props.$isSelected ? 'rgba(74, 158, 255, 0.2)' : 'transparent'};
  border-left: ${props => props.$isUnread ? '3px solid #4a9eff' : '3px solid transparent'};
  opacity: ${props => props.$isLocked ? 0.6 : 1};

  &:hover {
    background: ${props => props.$isLocked ? 'transparent' : 'rgba(255, 255, 255, 0.05)'};
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

const PriorityBadge = styled.span<{ $priority: 'high' | 'medium' | 'low' | 'normal' | 'urgent' }>`
  padding: 2px 6px;
  border-radius: 3px;
  font-size: 10px;
  font-weight: 500;
  text-transform: uppercase;
  background: ${props => {
    switch (props.$priority) {
      case 'high': 
      case 'urgent': 
        return '#ff4757'
      case 'medium': 
      case 'normal': 
        return '#ffa502'
      case 'low': 
        return '#2ed573'
      default: 
        return '#747d8c'
    }
  }};
  color: white;
  margin-left: 0.5rem;
`

const LockedEmailOverlay = styled.div`
  padding: 2rem;
  background: rgba(0, 0, 0, 0.3);
  border-radius: 6px;
  border: 1px solid rgba(255, 165, 0, 0.3);
  text-align: center;
`

const LockIcon = styled.div`
  font-size: 48px;
  margin-bottom: 1rem;
`

const UnlockRequirements = styled.div`
  margin-top: 1.5rem;
  padding: 1rem;
  background: rgba(255, 165, 0, 0.1);
  border-radius: 4px;
  border: 1px solid rgba(255, 165, 0, 0.3);
  text-align: left;
`

const RequirementList = styled.ul`
  list-style: none;
  padding: 0;
  margin: 0.5rem 0 0 0;
`

const RequirementItem = styled.li`
  padding: 0.25rem 0;
  color: rgba(255, 255, 255, 0.8);
  font-size: 13px;
  
  &:before {
    content: '• ';
    color: #ffa502;
    font-weight: bold;
    margin-right: 0.5rem;
  }
`

const Email: React.FC = () => {
  const [selectedEmail, setSelectedEmail] = useState<string | null>(null)
  const { getAvailableEmails, currentCase } = useCase()

  // Get emails from CaseEngine (with gating logic applied)
  const emails = getAvailableEmails()

  // Auto-select first email when emails load
  React.useEffect(() => {
    if (emails.length > 0 && !selectedEmail) {
      setSelectedEmail(emails[0].id)
    }
  }, [emails, selectedEmail])

  const handleEmailClick = (emailId: string, isUnlocked: boolean) => {
    if (!isUnlocked) {
      // Don't allow clicking on locked emails
      return
    }
    setSelectedEmail(emailId)
  }

  const handleAttachmentOpen = (attachmentName: string, evidenceId?: string) => {
    // This will trigger opening the file in FileViewer
    console.log(`Opening file: ${attachmentName}${evidenceId ? ` (Evidence ID: ${evidenceId})` : ''} in FileViewer`)
    alert(`File "${attachmentName}" would be opened in the File Viewer component.`)
  }

  const selectedEmailData = emails.find(email => email.id === selectedEmail)

  return (
    <EmailContainer>
      <h3 style={{ margin: '0 0 1rem 0' }}>Police Email System{currentCase ? ` - ${currentCase}` : ''}</h3>
      
      <TwoColumnLayout>
        <LeftPanel>
          <h4 style={{ margin: '0 0 1rem 0', color: '#4a9eff' }}>Inbox ({emails.length})</h4>
          <EmailList>
            {emails.map(email => (
              <EmailItem
                key={email.id}
                $isSelected={email.id === selectedEmail}
                $isUnread={!email.isUnlocked && !selectedEmail}
                $isLocked={!email.isUnlocked}
                onClick={() => handleEmailClick(email.id, email.isUnlocked)}
              >
                <EmailHeader>
                  <EmailSender $isUnread={!email.isUnlocked && !selectedEmail}>
                    {!email.isUnlocked && '🔒 '}{email.sender || email.from}
                    <PriorityBadge $priority={email.priority}>
                      {email.priority}
                    </PriorityBadge>
                  </EmailSender>
                  <EmailTime>{email.time || email.sentAt}</EmailTime>
                </EmailHeader>
                <EmailSubject $isUnread={!email.isUnlocked && !selectedEmail}>
                  {email.subject}
                </EmailSubject>
                <EmailPreview>
                  {email.isUnlocked 
                    ? (email.content?.substring(0, 100) + '...' || '') 
                    : `🔒 ${email.gatingRule?.unlockMessage || 'Este email está bloqueado. Colete as evidências necessárias.'}`
                  }
                  {email.isUnlocked && email.attachments && email.attachments.length > 0 && (
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
            selectedEmailData.isUnlocked ? (
              <EmailContent>
                <h4 style={{ margin: '0 0 0.5rem 0', color: '#4a9eff' }}>
                  {selectedEmailData.subject}
                </h4>
                <p style={{ margin: '0 0 1rem 0', fontSize: '12px', color: 'rgba(255, 255, 255, 0.6)' }}>
                  From: {selectedEmailData.sender || selectedEmailData.from} • {selectedEmailData.time || selectedEmailData.sentAt}
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
                        <AttachmentButton onClick={() => handleAttachmentOpen(attachment.name, attachment.evidenceId)}>
                          Open in File Viewer
                        </AttachmentButton>
                      </AttachmentItem>
                    ))}
                  </AttachmentList>
                )}
              </EmailContent>
            ) : (
              <EmailContent style={{ position: 'relative' }}>
                <h4 style={{ margin: '0 0 0.5rem 0', color: '#4a9eff' }}>
                  {selectedEmailData.subject}
                </h4>
                <p style={{ margin: '0 0 1rem 0', fontSize: '12px', color: 'rgba(255, 255, 255, 0.6)' }}>
                  From: {selectedEmailData.sender || selectedEmailData.from} • {selectedEmailData.time || selectedEmailData.sentAt}
                </p>
                
                <LockedEmailOverlay>
                  <LockIcon>🔒</LockIcon>
                  <h3 style={{ margin: '0 0 1rem 0', color: '#fff' }}>Email Bloqueado</h3>
                  <p style={{ margin: '0 0 1.5rem 0', color: 'rgba(255, 255, 255, 0.8)', textAlign: 'center' }}>
                    {selectedEmailData.gatingRule?.unlockMessage || 'Este email está bloqueado. Colete as evidências necessárias para desbloqueá-lo.'}
                  </p>
                  
                  {selectedEmailData.gatingRule && (
                    <UnlockRequirements>
                      <h4 style={{ margin: '0 0 0.75rem 0', color: '#4a9eff' }}>Requisitos para Desbloquear:</h4>
                      <RequirementList>
                        {selectedEmailData.gatingRule.requiredDocuments && selectedEmailData.gatingRule.requiredDocuments.length > 0 && (
                          <>
                            <h5 style={{ margin: '0.5rem 0 0.25rem 0', color: 'rgba(255, 255, 255, 0.9)', fontSize: '13px' }}>Documentos:</h5>
                            {selectedEmailData.gatingRule.requiredDocuments.map((doc, index) => (
                              <RequirementItem key={`doc-${index}`}>📄 {doc}</RequirementItem>
                            ))}
                          </>
                        )}
                        
                        {selectedEmailData.gatingRule.requiredMedia && selectedEmailData.gatingRule.requiredMedia.length > 0 && (
                          <>
                            <h5 style={{ margin: '0.5rem 0 0.25rem 0', color: 'rgba(255, 255, 255, 0.9)', fontSize: '13px' }}>Mídia:</h5>
                            {selectedEmailData.gatingRule.requiredMedia.map((media, index) => (
                              <RequirementItem key={`media-${index}`}>🖼️ {media}</RequirementItem>
                            ))}
                          </>
                        )}
                        
                        {selectedEmailData.gatingRule.requiredEvidence && selectedEmailData.gatingRule.requiredEvidence.length > 0 && (
                          <>
                            <h5 style={{ margin: '0.5rem 0 0.25rem 0', color: 'rgba(255, 255, 255, 0.9)', fontSize: '13px' }}>Evidências:</h5>
                            {selectedEmailData.gatingRule.requiredEvidence.map((evidence, index) => (
                              <RequirementItem key={`evidence-${index}`}>🔍 {evidence}</RequirementItem>
                            ))}
                          </>
                        )}
                      </RequirementList>
                    </UnlockRequirements>
                  )}
                </LockedEmailOverlay>
              </EmailContent>
            )
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