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
      ]
    }
    
    return caseEmails[caseId] || caseEmails['CASE-2024-001']
  }

  const emails = getCaseEmails(currentCase || 'CASE-2024-001')

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