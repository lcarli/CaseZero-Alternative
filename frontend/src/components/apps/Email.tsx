import React, { useState } from 'react'
import styled from 'styled-components'

const EmailContainer = styled.div`
  height: 100%;
  display: flex;
  flex-direction: column;
`

const EmailList = styled.div`
  flex: 1;
  overflow-y: auto;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  margin-bottom: 1rem;
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
  padding: 1rem;
  background: rgba(0, 0, 0, 0.2);
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  max-height: 200px;
  overflow-y: auto;
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

interface EmailData {
  id: string
  sender: string
  subject: string
  preview: string
  content: string
  time: string
  isUnread: boolean
  priority: 'high' | 'medium' | 'low'
}

const Email: React.FC = () => {
  const [selectedEmail, setSelectedEmail] = useState<string | null>('email1')
  const [emails, setEmails] = useState<EmailData[]>([
    {
      id: 'email1',
      sender: 'Chief Johnson',
      subject: 'URGENT: Case Assignment #247',
      preview: 'New high-priority case has been assigned to your unit. Suspect last seen downtown...',
      content: `Officer,

A new high-priority case has been assigned to your unit. Case #247 involves a series of break-ins in the downtown area.

Key Details:
- 3 incidents in the past week
- Similar MO: entry through rear windows
- Missing items: Electronics and cash
- Suspect: Male, 5'8", dark clothing

Please review the case files and begin investigation immediately.

Chief Johnson
Metropolitan Police Department`,
      time: '2 hours ago',
      isUnread: true,
      priority: 'high'
    },
    {
      id: 'email2',
      sender: 'Forensics Lab',
      subject: 'DNA Results - Case #245',
      preview: 'DNA analysis complete for evidence submitted last Tuesday...',
      content: `Lab Report - Case #245

DNA analysis has been completed for the evidence submitted on Tuesday.

Results:
- Sample 1: No match found in database
- Sample 2: Partial match (78% confidence)
- Sample 3: Match found - John Doe (Criminal ID: 12345)

Full report attached to case file.

Dr. Sarah Mitchell
Forensics Department`,
      time: '1 day ago',
      isUnread: false,
      priority: 'medium'
    },
    {
      id: 'email3',
      sender: 'IT Department',
      subject: 'System Maintenance Reminder',
      preview: 'Scheduled system maintenance tonight from 11 PM to 2 AM...',
      content: `System Maintenance Notice

Please be advised that scheduled system maintenance will occur tonight from 11:00 PM to 2:00 AM.

During this time:
- Database access may be limited
- Case management system will be offline
- Email services may be intermittent

Please save all work before 11:00 PM.

IT Support Team`,
      time: '3 days ago',
      isUnread: false,
      priority: 'low'
    }
  ])

  const handleEmailClick = (emailId: string) => {
    setSelectedEmail(emailId)
    // Mark as read
    setEmails(prev => 
      prev.map(email => 
        email.id === emailId ? { ...email, isUnread: false } : email
      )
    )
  }

  const selectedEmailData = emails.find(email => email.id === selectedEmail)

  return (
    <EmailContainer>
      <h3 style={{ margin: '0 0 1rem 0' }}>Police Email System</h3>
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
            <EmailPreview>{email.preview}</EmailPreview>
          </EmailItem>
        ))}
      </EmailList>

      {selectedEmailData && (
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
        </EmailContent>
      )}
    </EmailContainer>
  )
}

export default Email