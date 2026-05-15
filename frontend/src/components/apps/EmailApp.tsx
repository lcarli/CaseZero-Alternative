import React, { useState } from 'react'
import styled from 'styled-components'
import type { EmailListDTO, EmailDTO } from '../../services/api'
import { emailsApi } from '../../services/api'

const EmailAppContainer = styled.div`
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
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  overflow-y: auto;
`

const EmailItem = styled.div<{ $isRead: boolean; $isSelected: boolean }>`
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  padding: 0.75rem;
  border-radius: 4px;
  cursor: pointer;
  transition: background 0.2s ease;
  background: ${props => props.$isSelected ? 'rgba(74, 158, 255, 0.2)' : 'transparent'};
  border-left: 3px solid ${props => props.$isRead ? 'transparent' : 'rgba(74, 158, 255, 0.8)'};

  &:hover {
    background: rgba(255, 255, 255, 0.05);
  }
`

const EmailHeader = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
`

const EmailFrom = styled.span<{ $isRead: boolean }>`
  font-weight: ${props => props.$isRead ? '400' : '600'};
  color: ${props => props.$isRead ? 'rgba(255, 255, 255, 0.8)' : 'rgba(255, 255, 255, 1)'};
  font-size: 14px;
`

const EmailTime = styled.span`
  font-size: 11px;
  color: rgba(255, 255, 255, 0.5);
`

const EmailSubject = styled.span<{ $isRead: boolean }>`
  font-weight: ${props => props.$isRead ? '400' : '600'};
  color: ${props => props.$isRead ? 'rgba(255, 255, 255, 0.7)' : 'rgba(255, 255, 255, 0.9)'};
  font-size: 13px;
`

const EmailContent = styled.div`
  flex: 1;
  padding: 1rem;
  background: rgba(0, 0, 0, 0.2);
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  overflow-y: auto;
`

const EmailMetadata = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding-bottom: 1rem;
  margin-bottom: 1rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
`

const MetadataRow = styled.div`
  display: flex;
  gap: 0.5rem;
  font-size: 13px;
`

const MetadataLabel = styled.span`
  color: rgba(255, 255, 255, 0.5);
  min-width: 60px;
`

const MetadataValue = styled.span`
  color: rgba(255, 255, 255, 0.9);
`

const EmailBody = styled.div`
  color: rgba(255, 255, 255, 0.9);
  line-height: 1.6;
  font-size: 14px;
  white-space: pre-wrap;
  
  a {
    color: #4a9eff;
    text-decoration: underline;
  }
`

const AttachmentsSection = styled.div`
  margin-top: 1.5rem;
  padding-top: 1rem;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
`

const AttachmentsList = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-top: 0.5rem;
`

const AttachmentItem = styled.div`
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.5rem;
  background: rgba(74, 158, 255, 0.1);
  border-radius: 4px;
  border: 1px solid rgba(74, 158, 255, 0.3);
`

const AttachmentName = styled.span`
  color: rgba(255, 255, 255, 0.9);
  font-size: 13px;
`

const DownloadButton = styled.button`
  padding: 0.25rem 0.75rem;
  background: rgba(74, 158, 255, 0.8);
  border: none;
  border-radius: 4px;
  color: white;
  font-size: 12px;
  cursor: pointer;
  transition: background 0.2s ease;

  &:hover {
    background: rgba(74, 158, 255, 1);
  }

  &:disabled {
    background: rgba(100, 100, 100, 0.5);
    cursor: not-allowed;
  }
`

const LoadingMessage = styled.div`
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: rgba(255, 255, 255, 0.6);
  font-size: 14px;
`

interface EmailAppProps {
  emails?: EmailListDTO[]
  caseId?: string
  onRefetchAssets?: () => void
}

const EmailApp: React.FC<EmailAppProps> = ({ emails = [], caseId, onRefetchAssets }) => {
  const [selectedEmailId, setSelectedEmailId] = useState<string | null>(null)
  const [openedEmail, setOpenedEmail] = useState<EmailDTO | null>(null)
  const [loading, setLoading] = useState(false)
  const [downloadingAttachment, setDownloadingAttachment] = useState<string | null>(null)

  const handleEmailClick = async (emailId: string) => {
    if (!caseId) return

    setSelectedEmailId(emailId)
    setLoading(true)

    try {
      // Task 49: POST /open then GET full email
      const fullEmail = await emailsApi.openEmail(caseId, emailId)
      setOpenedEmail(fullEmail)
    } catch (error) {
      console.error('Failed to open email:', error)
    } finally {
      setLoading(false)
    }
  }

  const handleDownloadAttachment = async (assetId: string) => {
    if (!caseId || !openedEmail) return

    setDownloadingAttachment(assetId)

    try {
      // Task 50: POST /emails/{emailId}/attachments/{assetId}/download
      await emailsApi.downloadAttachment(caseId, openedEmail.emailId, assetId)
      
      // Refetch assets to update FileViewer
      if (onRefetchAssets) {
        onRefetchAssets()
      }

      console.log(`✅ Attachment ${assetId} downloaded successfully`)
    } catch (error) {
      console.error('Failed to download attachment:', error)
    } finally {
      setDownloadingAttachment(null)
    }
  }

  const formatTimestamp = (timestamp: string): string => {
    const date = new Date(timestamp)
    const now = new Date()
    const diffMs = now.getTime() - date.getTime()
    const diffMins = Math.floor(diffMs / 60000)
    const diffHours = Math.floor(diffMins / 60)
    const diffDays = Math.floor(diffHours / 24)

    if (diffMins < 60) return `${diffMins}m ago`
    if (diffHours < 24) return `${diffHours}h ago`
    if (diffDays < 7) return `${diffDays}d ago`
    
    return date.toLocaleDateString()
  }

  return (
    <EmailAppContainer>
      <h3 style={{ margin: '0 0 1rem 0' }}>Inbox - {emails.length} emails</h3>
      
      <TwoColumnLayout>
        <LeftPanel>
          <h4 style={{ margin: '0 0 1rem 0', color: '#4a9eff' }}>Messages</h4>
          <EmailList>
            {emails.length === 0 ? (
              <div style={{ padding: '1rem', color: 'rgba(255, 255, 255, 0.6)', textAlign: 'center' }}>
                No emails available
              </div>
            ) : (
              emails.map(email => (
                <EmailItem
                  key={email.emailId}
                  $isRead={email.isRead}
                  $isSelected={selectedEmailId === email.emailId}
                  onClick={() => handleEmailClick(email.emailId)}
                >
                  <EmailHeader>
                    <EmailFrom $isRead={email.isRead}>{email.from}</EmailFrom>
                    <EmailTime>{formatTimestamp(email.sentAt)}</EmailTime>
                  </EmailHeader>
                  <EmailSubject $isRead={email.isRead}>
                    {email.hasAttachments && '📎 '}
                    {email.subject}
                  </EmailSubject>
                </EmailItem>
              ))
            )}
          </EmailList>
        </LeftPanel>
        
        <RightPanel>
          {loading ? (
            <LoadingMessage>Loading email...</LoadingMessage>
          ) : openedEmail ? (
            <EmailContent>
              <h4 style={{ margin: '0 0 1rem 0', color: '#4a9eff' }}>{openedEmail.subject}</h4>
              
              <EmailMetadata>
                <MetadataRow>
                  <MetadataLabel>From:</MetadataLabel>
                  <MetadataValue>{openedEmail.from}</MetadataValue>
                </MetadataRow>
                <MetadataRow>
                  <MetadataLabel>To:</MetadataLabel>
                  <MetadataValue>{openedEmail.to}</MetadataValue>
                </MetadataRow>
                <MetadataRow>
                  <MetadataLabel>Date:</MetadataLabel>
                  <MetadataValue>{new Date(openedEmail.sentAt).toLocaleString()}</MetadataValue>
                </MetadataRow>
              </EmailMetadata>

              <EmailBody dangerouslySetInnerHTML={{ __html: openedEmail.content }} />

              {openedEmail.attachments && openedEmail.attachments.length > 0 && (
                <AttachmentsSection>
                  <h5 style={{ margin: '0 0 0.5rem 0', color: '#4a9eff' }}>
                    📎 Attachments ({openedEmail.attachments.length})
                  </h5>
                  <AttachmentsList>
                    {openedEmail.attachments.map((assetId, index) => (
                      <AttachmentItem key={assetId}>
                        <AttachmentName>Attachment_{index + 1} (ID: {assetId})</AttachmentName>
                        <DownloadButton
                          onClick={() => handleDownloadAttachment(assetId)}
                          disabled={downloadingAttachment === assetId}
                        >
                          {downloadingAttachment === assetId ? 'Downloading...' : 'Download'}
                        </DownloadButton>
                      </AttachmentItem>
                    ))}
                  </AttachmentsList>
                </AttachmentsSection>
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
              Select an email to read
            </div>
          )}
        </RightPanel>
      </TwoColumnLayout>
    </EmailAppContainer>
  )
}

export default EmailApp
