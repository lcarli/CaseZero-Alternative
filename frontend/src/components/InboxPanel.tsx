import { useEffect, useState } from 'react'
import styled from 'styled-components'
import { Mail } from 'react-feather'
import { useLanguage } from '../hooks/useLanguageContext'
import { inboxApi, type InboxEmail } from '../services/api'
import { rankI18nKey } from '../types/ranks'

const Panel = styled.section`
  background: rgba(8, 12, 28, 0.8);
  border: 1px solid rgba(56, 189, 248, 0.2);
  border-radius: 1.25rem;
  padding: clamp(1rem, 2vw, 1.5rem);
`

const PanelHeader = styled.h3`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin: 0 0 1rem 0;
  font-size: 1rem;
  color: #38bdf8;
`

const Badge = styled.span`
  margin-left: auto;
  background: rgba(56, 189, 248, 0.2);
  color: #38bdf8;
  border-radius: 999px;
  padding: 0.1rem 0.55rem;
  font-size: 0.75rem;
`

const EmptyMessage = styled.div`
  color: rgba(148, 163, 184, 0.8);
  font-size: 0.85rem;
`

const EmailItem = styled.button<{ $read: boolean }>`
  width: 100%;
  text-align: left;
  background: ${p => (p.$read ? 'rgba(255,255,255,0.02)' : 'rgba(56,189,248,0.08)')};
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 0.75rem;
  padding: 0.75rem 0.9rem;
  margin-bottom: 0.6rem;
  color: #e2e8f0;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  gap: 0.2rem;

  &:hover {
    border-color: rgba(56, 189, 248, 0.4);
  }
`

const Subject = styled.div<{ $read: boolean }>`
  font-weight: ${p => (p.$read ? 500 : 700)};
`

const Body = styled.div`
  font-size: 0.8rem;
  color: rgba(203, 213, 225, 0.85);
`

const Meta = styled.div`
  font-size: 0.7rem;
  color: rgba(148, 163, 184, 0.7);
`

interface PromotionMeta {
  kind?: string
  previousRank?: string
  newRank?: string
}

const InboxPanel: React.FC = () => {
  const { t } = useLanguage()
  const [emails, setEmails] = useState<InboxEmail[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let active = true
    inboxApi.getInbox()
      .then(data => { if (active) setEmails(data ?? []) })
      .catch(() => { if (active) setEmails([]) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])

  const localize = (email: InboxEmail): { subject: string; body: string } => {
    if (email.type === 'PromotionNotice' && email.metadataJson) {
      try {
        const meta = JSON.parse(email.metadataJson) as PromotionMeta
        const newRank = meta.newRank
          ? t(rankI18nKey(meta.newRank) as Parameters<typeof t>[0])
          : ''
        const previousRank = meta.previousRank
          ? t(rankI18nKey(meta.previousRank) as Parameters<typeof t>[0])
          : ''
        return {
          subject: t('promotionEmailSubject').replace('{rank}', newRank),
          body: t('promotionEmailBody')
            .replace('{previousRank}', previousRank)
            .replace('{rank}', newRank)
        }
      } catch {
        // Fall through to stored content on malformed metadata.
      }
    }
    return { subject: email.subject, body: email.content }
  }

  const markRead = (email: InboxEmail) => {
    if (email.isRead) return
    setEmails(prev => prev.map(e => (e.id === email.id ? { ...e, isRead: true } : e)))
    inboxApi.markRead(email.id).catch(() => { /* best-effort */ })
  }

  const unread = emails.filter(e => !e.isRead).length

  return (
    <Panel>
      <PanelHeader>
        <Mail size={16} />
        {t('inboxTitle')}
        {unread > 0 && <Badge>{unread} {t('inboxUnread')}</Badge>}
      </PanelHeader>
      {loading ? (
        <EmptyMessage>{t('loading')}</EmptyMessage>
      ) : emails.length === 0 ? (
        <EmptyMessage>{t('inboxEmpty')}</EmptyMessage>
      ) : (
        emails.map(email => {
          const { subject, body } = localize(email)
          return (
            <EmailItem key={email.id} $read={email.isRead} onClick={() => markRead(email)}>
              <Subject $read={email.isRead}>{subject}</Subject>
              <Body>{body}</Body>
              <Meta>{new Date(email.sentAt).toLocaleString()}</Meta>
            </EmailItem>
          )
        })
      )}
    </Panel>
  )
}

export default InboxPanel
