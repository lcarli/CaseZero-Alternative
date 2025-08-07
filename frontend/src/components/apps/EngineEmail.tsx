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

const EmailItemContainer = styled.div<{ $isSelected: boolean; $isUnread: boolean }>`
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

const LoadingMessage = styled.div`
  display: flex;
  align-items: center;
  justify-content: center;
  height: 200px;
  color: rgba(255, 255, 255, 0.6);
  font-size: 16px;
`

const ErrorMessage = styled.div`
  display: flex;
  align-items: center;
  justify-content: center;
  height: 200px;
  color: rgba(231, 76, 60, 0.9);
  font-size: 16px;
`

const EngineEmail: React.FC = () => {
  const [selectedEmail, setSelectedEmail] = useState<string | null>(null)
  const { currentCase, getAvailableEmails, downloadAttachment, isLoading, error } = useCase()

  if (isLoading) {
    return (
      <EmailContainer>
        <LoadingMessage>Loading emails...</LoadingMessage>
      </EmailContainer>
    )
  }

  if (error) {
    return (
      <EmailContainer>
        <ErrorMessage>Error: {error}</ErrorMessage>
      </EmailContainer>
    )
  }

  const emails = getAvailableEmails()

  const handleEmailClick = (emailId: string) => {
    setSelectedEmail(emailId)
  }

  const handleAttachmentOpen = (attachmentName: string) => {
    // Notify engine that attachment was downloaded
    downloadAttachment(attachmentName)
    alert(`File "${attachmentName}" downloaded and will be available in the File Viewer.`)
  }

  const selectedEmailData = emails.find(email => email.id === selectedEmail)

  return (
    <EmailContainer>
      <h3 style={{ margin: '0 0 1rem 0' }}>Police Email System - {currentCase || 'No Case'}</h3>
      
      <TwoColumnLayout>
        <LeftPanel>
          <h4 style={{ margin: '0 0 1rem 0', color: '#4a9eff' }}>Inbox ({emails.length})</h4>
          <EmailList>
            {emails.length > 0 ? (
              emails.map(email => (
                <EmailItemContainer
                  key={email.id}
                  $isSelected={email.id === selectedEmail}
                  $isUnread={email.isUnlocked}
                  onClick={() => handleEmailClick(email.id)}
                >
                  <EmailHeader>
                    <EmailSender $isUnread={email.isUnlocked}>
                      {email.sender}
                      <PriorityBadge $priority={email.priority}>
                        {email.priority}
                      </PriorityBadge>
                    </EmailSender>
                    <EmailTime>{email.time}</EmailTime>
                  </EmailHeader>
                  <EmailSubject $isUnread={email.isUnlocked}>
                    {email.subject}
                  </EmailSubject>
                  <EmailPreview>
                    {email.content.substring(0, 100)}...
                    {email.attachments && email.attachments.length > 0 && (
                      <span style={{ marginLeft: '0.5rem', color: '#4a9eff' }}>
                        📎 {email.attachments.length} attachment{email.attachments.length > 1 ? 's' : ''}
                      </span>
                    )}
                  </EmailPreview>
                </EmailItemContainer>
              ))
            ) : (
              <div style={{ 
                color: 'rgba(255, 255, 255, 0.6)', 
                textAlign: 'center', 
                padding: '2rem',
                fontStyle: 'italic'
              }}>
                No emails available for this case.
              </div>
            )}
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
                        Download & Open
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

export default EngineEmail