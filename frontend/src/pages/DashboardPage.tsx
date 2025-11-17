import { useNavigate } from 'react-router-dom'
import { useState, useEffect, useMemo } from 'react'
import styled from 'styled-components'
import { Activity, Shield, Briefcase, Target, AlertTriangle, Radio, Layers, ArrowRight } from 'react-feather'
import { useAuth } from '../hooks/useAuthContext'
import { useLanguage } from '../hooks/useLanguageContext'
import { casesApi } from '../services/api'
import LanguageSelector from '../components/LanguageSelector'
import type { Dashboard } from '../services/api'
import departmentBadge from '../assets/LogoMetroPolice_transparent.png'

const PageContainer = styled.div`
  min-height: 100vh;
  background: radial-gradient(circle at top, rgba(56, 189, 248, 0.08), transparent 45%),
    radial-gradient(circle at 15% 80%, rgba(59, 130, 246, 0.15), transparent 35%),
    #040714;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  color: #f8fafc;
  padding: clamp(1rem, 3vw, 2.5rem);
  box-sizing: border-box;
`

const BackgroundGrid = styled.div`
  position: fixed;
  inset: 0;
  pointer-events: none;
  background-image: linear-gradient(90deg, rgba(148, 197, 255, 0.05) 1px, transparent 0),
    linear-gradient(0deg, rgba(148, 197, 255, 0.05) 1px, transparent 0);
  background-size: 120px 120px;
  opacity: 0.35;
`

const Header = styled.header`
  position: relative;
  z-index: 1;
  background: rgba(8, 12, 28, 0.8);
  border: 1px solid rgba(56, 189, 248, 0.2);
  border-radius: 1.5rem;
  padding: clamp(1.25rem, 3vw, 2rem);
  margin-bottom: clamp(1rem, 3vw, 2rem);
  display: flex;
  flex-wrap: wrap;
  gap: clamp(1rem, 2vw, 1.5rem);
  align-items: center;
  justify-content: space-between;
  box-shadow: 0 25px 70px rgba(2, 6, 23, 0.7);
`

const IdentityBlock = styled.div`
  display: flex;
  align-items: center;
  gap: 1.25rem;
  min-width: 280px;
`

const BadgeImage = styled.img`
  width: clamp(72px, 9vw, 96px);
  height: clamp(72px, 9vw, 96px);
  object-fit: contain;
  border-radius: 50%;
  background: rgba(15, 23, 42, 0.9);
  padding: 0.8rem;
  border: 1px solid rgba(148, 197, 255, 0.35);
`

const IdentityText = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
`

const AgencyName = styled.span`
  font-size: 0.8rem;
  text-transform: uppercase;
  letter-spacing: 0.25em;
  color: rgba(148, 197, 255, 0.85);
`

const AgentName = styled.h1`
  margin: 0;
  font-size: clamp(1.25rem, 3vw, 1.75rem);
  color: #f8fafc;
`

const AgentMeta = styled.div`
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
  font-size: 0.9rem;
  color: rgba(226, 232, 240, 0.8);
`

const HeaderBadges = styled.div`
  display: flex;
  gap: 0.6rem;
  flex-wrap: wrap;
`

const HeaderBadge = styled.span`
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.4rem 0.85rem;
  border-radius: 999px;
  border: 1px solid rgba(56, 189, 248, 0.35);
  background: rgba(15, 23, 42, 0.55);
  font-size: 0.8rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: rgba(148, 197, 255, 0.9);
`

const HeaderControls = styled.div`
  display: flex;
  align-items: center;
  gap: 1rem;
  flex-wrap: wrap;
  justify-content: flex-end;
`

const LogoutButton = styled.button`
  padding: 0.7rem 1.4rem;
  border-radius: 999px;
  border: 1px solid rgba(239, 68, 68, 0.4);
  background: rgba(239, 68, 68, 0.15);
  color: #fecdd3;
  font-weight: 600;
  letter-spacing: 0.05em;
  cursor: pointer;
  transition: background 0.2s ease, border-color 0.2s ease;

  &:hover {
    border-color: rgba(248, 113, 113, 0.8);
    background: rgba(248, 113, 113, 0.2);
  }
`

const ContentGrid = styled.div`
  position: relative;
  z-index: 1;
  display: grid;
  grid-template-columns: minmax(0, 2fr) minmax(280px, 1fr);
  gap: clamp(1rem, 2vw, 1.5rem);

  @media (max-width: 1100px) {
    grid-template-columns: 1fr;
  }
`

const Panel = styled.section`
  background: rgba(8, 12, 28, 0.75);
  border: 1px solid rgba(56, 189, 248, 0.15);
  border-radius: 1.25rem;
  padding: clamp(1rem, 2vw, 1.5rem);
  box-shadow: inset 0 0 0 1px rgba(15, 23, 42, 0.4);
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
`

const PanelHeader = styled.div`
  display: flex;
  align-items: center;
  gap: 0.6rem;
  text-transform: uppercase;
  letter-spacing: 0.18em;
  font-size: 0.8rem;
  color: rgba(148, 197, 255, 0.85);
`

const StatGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 1rem;
`

const StatCard = styled.div`
  border-radius: 1rem;
  padding: 1rem;
  background: rgba(15, 23, 42, 0.65);
  border: 1px solid rgba(56, 189, 248, 0.15);
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
`

const StatusSummary = styled.div`
  display: flex;
  flex-wrap: wrap;
  gap: 0.65rem;
  margin-top: 0.5rem;
`

const StatusPill = styled.span<{ $tone: 'open' | 'progress' | 'resolved' }>`
  padding: 0.35rem 0.7rem;
  border-radius: 999px;
  font-size: 0.8rem;
  background: ${({ $tone }) => ($tone === 'resolved' ? 'rgba(34, 197, 94, 0.15)' : $tone === 'progress' ? 'rgba(250, 204, 21, 0.15)' : 'rgba(248, 113, 113, 0.15)')};
  color: ${({ $tone }) => ($tone === 'resolved' ? '#86efac' : $tone === 'progress' ? '#fde047' : '#fca5a5')};
  border: 1px solid rgba(255, 255, 255, 0.08);
  display: inline-flex;
  gap: 0.35rem;
  align-items: center;
`

const StatLabel = styled.span`
  font-size: 0.85rem;
  color: rgba(148, 197, 255, 0.75);
`

const StatValue = styled.div`
  font-size: 1.75rem;
  font-weight: 600;
  color: #e0f2fe;
  display: flex;
  align-items: center;
  gap: 0.5rem;
`

const CaseTable = styled.div`
  border-radius: 1rem;
  border: 1px solid rgba(56, 189, 248, 0.12);
  overflow: hidden;
`

const TableHeader = styled.div`
  display: grid;
  grid-template-columns: 1.2fr 1.2fr 0.9fr 0.9fr 1fr;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
  font-size: 0.75rem;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  background: rgba(15, 23, 42, 0.85);
  color: rgba(148, 197, 255, 0.7);

  @media (max-width: 900px) {
    grid-template-columns: 1.5fr 1fr 1fr;
    & > span:nth-child(3),
    & > span:nth-child(4) {
      display: none;
    }
  }
`

const CaseRow = styled.button`
  width: 100%;
  display: grid;
  grid-template-columns: 1.2fr 1.2fr 0.9fr 0.9fr 1fr;
  gap: 0.75rem;
  padding: 1rem;
  background: rgba(7, 11, 26, 0.7);
  border: none;
  border-bottom: 1px solid rgba(56, 189, 248, 0.08);
  color: inherit;
  text-align: left;
  cursor: pointer;
  transition: background 0.15s ease;

  &:hover {
    background: rgba(15, 23, 42, 0.95);
  }

  @media (max-width: 900px) {
    grid-template-columns: 1.5fr 1fr 1fr;
    & > span:nth-child(3),
    & > span:nth-child(4) {
      display: none;
    }
  }
`

const StatusBadge = styled.span<{ $status: 'open' | 'progress' | 'resolved' }>`
  padding: 0.25rem 0.7rem;
  border-radius: 999px;
  font-size: 0.8rem;
  font-weight: 600;
  justify-self: flex-start;
  background: ${({ $status }) => {
    if ($status === 'resolved') return 'rgba(34, 197, 94, 0.15)'
    if ($status === 'progress') return 'rgba(250, 204, 21, 0.15)'
    return 'rgba(248, 113, 113, 0.15)'
  }};
  color: ${({ $status }) => {
    if ($status === 'resolved') return '#86efac'
    if ($status === 'progress') return '#fde047'
    return '#fca5a5'
  }};
  border: 1px solid rgba(255, 255, 255, 0.08);
`

const PriorityTag = styled.span<{ $priority: string }>`
  padding: 0.25rem 0.6rem;
  border-radius: 0.5rem;
  font-size: 0.8rem;
  background: ${({ $priority }) => {
    switch ($priority) {
      case 'Critical': return 'rgba(248, 113, 113, 0.15)'
      case 'High': return 'rgba(251, 191, 36, 0.15)'
      case 'Medium': return 'rgba(96, 165, 250, 0.15)'
      default: return 'rgba(34, 197, 94, 0.15)'
    }
  }};
  color: ${({ $priority }) => {
    switch ($priority) {
      case 'Critical': return '#fca5a5'
      case 'High': return '#fbbf24'
      case 'Medium': return '#93c5fd'
      default: return '#86efac'
    }
  }};
`

const ActivityList = styled.div`
  display: flex;
  flex-direction: column;
  gap: 1rem;
`

const ActivityItem = styled.div`
  display: grid;
  grid-template-columns: auto 1fr;
  gap: 0.75rem;
  align-items: start;
  padding-bottom: 1rem;
  border-bottom: 1px solid rgba(56, 189, 248, 0.1);

  &:last-child {
    border-bottom: none;
  }
`

const ActivityIcon = styled.div`
  width: 36px;
  height: 36px;
  border-radius: 0.75rem;
  background: rgba(56, 189, 248, 0.15);
  display: flex;
  align-items: center;
  justify-content: center;
`

const ActivityText = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
`

const ActivityTimestamp = styled.span`
  font-size: 0.75rem;
  color: rgba(148, 163, 184, 0.85);
`

const HeatmapBar = styled.div`
  display: flex;
  align-items: center;
  gap: 0.75rem;
`

const HeatmapTrack = styled.div`
  flex: 1;
  height: 8px;
  border-radius: 999px;
  background: rgba(15, 23, 42, 0.75);
  overflow: hidden;
`

const HeatmapFill = styled.div<{ $width: number; $priority: string }>`
  height: 100%;
  width: ${({ $width }) => `${$width}%`};
  border-radius: 999px;
  background: ${({ $priority }) => {
    switch ($priority) {
      case 'Critical': return 'linear-gradient(90deg, #f87171, #ef4444)'
      case 'High': return 'linear-gradient(90deg, #fbbf24, #ea580c)'
      case 'Medium': return 'linear-gradient(90deg, #60a5fa, #2563eb)'
      default: return 'linear-gradient(90deg, #34d399, #059669)'
    }
  }};
`

const ProgressTable = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
`

const ProgressRow = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
`

const ProgressMeta = styled.div`
  display: flex;
  justify-content: space-between;
  font-size: 0.85rem;
  color: rgba(148, 163, 184, 0.9);
`

const ProgressTrack = styled.div`
  width: 100%;
  height: 8px;
  border-radius: 999px;
  background: rgba(15, 23, 42, 0.75);
`

const ProgressFill = styled.div<{ $value: number }>`
  height: 100%;
  width: ${({ $value }) => `${$value}%`};
  border-radius: 999px;
  background: linear-gradient(90deg, #22d3ee, #2563eb);
`

const BulletinList = styled.div`
  display: flex;
  flex-direction: column;
  gap: 1rem;
`

const BulletinCard = styled.div`
  border-radius: 1rem;
  border: 1px solid rgba(56, 189, 248, 0.15);
  padding: 0.75rem 1rem;
  background: rgba(7, 11, 26, 0.7);
  display: flex;
  gap: 0.75rem;
  align-items: center;
`

const BulletinContent = styled.div`
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.9rem;
`

const BulletinCTA = styled.button`
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.45rem 0.9rem;
  border-radius: 999px;
  border: 1px solid rgba(56, 189, 248, 0.35);
  background: transparent;
  color: #bfdbfe;
  cursor: pointer;
  font-size: 0.8rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;

  &:hover {
    background: rgba(56, 189, 248, 0.15);
  }
`

const DashboardPage = () => {
  const navigate = useNavigate()
  const { user, logout } = useAuth()
  const { t, language } = useLanguage()
  const [dashboard, setDashboard] = useState<Dashboard | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    const loadDashboard = async () => {
      setIsLoading(true)
      setError('')
      try {
        const data = await casesApi.getDashboard()
        setDashboard(data)
      } catch (err) {
        setError(t('error'))
      } finally {
        setIsLoading(false)
      }
    }

    loadDashboard()
  }, [t])

  const locale = language?.code || 'en-US'

  const handleLogout = () => {
    logout()
    navigate('/')
  }

  const handleCaseClick = (caseId: string) => {
    navigate(`/desktop/${caseId}`)
  }

  const getStatusText = (status: string) => {
    switch (status) {
      case 'Open': return t('statusOpen')
      case 'InProgress': return t('statusInProgress')
      case 'Resolved':
      case 'Closed':
        return t('statusClosed')
      default: return status
    }
  }

  const getStatusStyle = (status: string): 'open' | 'progress' | 'resolved' => {
    if (status === 'Resolved' || status === 'Closed') return 'resolved'
    if (status === 'InProgress') return 'progress'
    return 'open'
  }

  const getPriorityText = (priority: string) => {
    switch (priority) {
      case 'Low': return t('priorityLow')
      case 'Medium': return t('priorityMedium')
      case 'High': return t('priorityHigh')
      case 'Critical': return t('priorityCritical')
      default: return priority
    }
  }

  const formatDate = (value?: string) => {
    if (!value) return '—'
    return new Intl.DateTimeFormat(locale, {
      dateStyle: 'short',
      timeStyle: 'short'
    }).format(new Date(value))
  }

  const statusBreakdown = useMemo(() => {
    if (!dashboard) return { Open: 0, InProgress: 0, Resolved: 0, Closed: 0 }
    return dashboard.cases.reduce((acc, case_) => {
      acc[case_.status] = (acc[case_.status] || 0) + 1
      return acc
    }, { Open: 0, InProgress: 0, Resolved: 0, Closed: 0 } as Record<string, number>)
  }, [dashboard])

  const priorityBreakdown = useMemo(() => {
    if (!dashboard) return { Critical: 0, High: 0, Medium: 0, Low: 0 }
    return dashboard.cases.reduce((acc, case_) => {
      acc[case_.priority] = (acc[case_.priority] || 0) + 1
      return acc
    }, { Critical: 0, High: 0, Medium: 0, Low: 0 } as Record<string, number>)
  }, [dashboard])

  const progressEntries = useMemo(() => {
    if (!dashboard) return []
    return dashboard.cases
      .filter(case_ => case_.userProgress)
      .map(case_ => ({
        id: case_.id,
        title: case_.title,
        completion: case_.userProgress?.completionPercentage || 0,
        evidences: case_.userProgress?.evidencesCollected || 0,
        interviews: case_.userProgress?.interviewsCompleted || 0
      }))
      .slice(0, 4)
  }, [dashboard])

  const sortedCases = useMemo(() => {
    if (!dashboard) return []
    return [...dashboard.cases].sort((a, b) => {
      const aDate = a.userProgress?.lastActivity || a.createdAt
      const bDate = b.userProgress?.lastActivity || b.createdAt
      return new Date(bDate).getTime() - new Date(aDate).getTime()
    })
  }, [dashboard])

  if (isLoading) {
    return (
      <PageContainer>
        <BackgroundGrid />
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '60vh' }}>
          <span>{t('loading')}</span>
        </div>
      </PageContainer>
    )
  }

  if (error) {
    return (
      <PageContainer>
        <BackgroundGrid />
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '60vh', color: '#fecaca' }}>
          <span>{error}</span>
        </div>
      </PageContainer>
    )
  }

  if (!dashboard) {
    return null
  }

  const totalCases = dashboard.cases.length || 1

  return (
    <PageContainer>
      <BackgroundGrid />
      <Header>
        <IdentityBlock>
          <BadgeImage src={departmentBadge} alt={t('metropolitanPoliceDept')} />
          <IdentityText>
            <AgencyName>{t('metropolitanPoliceDept')}</AgencyName>
            <AgentName>{user?.firstName} {user?.lastName}</AgentName>
            <AgentMeta>
              <span>{user?.position || t('detective')}</span>
              {user?.badgeNumber && <span>Badge #{user.badgeNumber}</span>}
              {user?.department && <span>{user.department}</span>}
            </AgentMeta>
            <HeaderBadges>
              <HeaderBadge>{t('loginBadgeSecure')}</HeaderBadge>
              <HeaderBadge>{t('loginBadgeMonitored')}</HeaderBadge>
            </HeaderBadges>
          </IdentityText>
        </IdentityBlock>
        <HeaderControls>
          <LanguageSelector appearance="landing" />
          <LogoutButton onClick={handleLogout}>{t('logout')}</LogoutButton>
        </HeaderControls>
      </Header>

      <ContentGrid>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
          <Panel>
            <PanelHeader>
              <Activity size={16} />
              {t('performanceStats')}
            </PanelHeader>
            <StatGrid>
              <StatCard>
                <StatLabel>{t('casesResolved')}</StatLabel>
                <StatValue><Shield size={20} />{dashboard.stats.casesResolved}</StatValue>
              </StatCard>
              <StatCard>
                <StatLabel>{t('casesActive')}</StatLabel>
                <StatValue><Briefcase size={20} />{dashboard.stats.casesActive}</StatValue>
              </StatCard>
              <StatCard>
                <StatLabel>{t('successRate')}</StatLabel>
                <StatValue><Target size={20} />{dashboard.stats.successRate}%</StatValue>
              </StatCard>
              <StatCard>
                <StatLabel>{t('averageRating')}</StatLabel>
                <StatValue><Activity size={20} />{dashboard.stats.averageRating}</StatValue>
              </StatCard>
            </StatGrid>
            <StatusSummary>
              <StatusPill $tone="open">
                {statusBreakdown.Open ?? 0}
                <span>{t('statusOpen')}</span>
              </StatusPill>
              <StatusPill $tone="progress">
                {statusBreakdown.InProgress ?? 0}
                <span>{t('statusInProgress')}</span>
              </StatusPill>
              <StatusPill $tone="resolved">
                {(statusBreakdown.Resolved ?? 0) + (statusBreakdown.Closed ?? 0)}
                <span>{t('statusClosed')}</span>
              </StatusPill>
            </StatusSummary>
          </Panel>

          <Panel>
            <PanelHeader>
              <Briefcase size={16} />
              {t('availableCases')}
            </PanelHeader>
            <CaseTable>
              <TableHeader>
                <span>{t('caseNumber')}</span>
                <span>{t('caseTitle')}</span>
                <span>{t('caseStatus')}</span>
                <span>{t('priority')}</span>
                <span>{t('lastSession')}</span>
              </TableHeader>
              {sortedCases.map(case_ => (
                <CaseRow key={case_.id} onClick={() => handleCaseClick(case_.id)}>
                  <span>{case_.id}</span>
                  <span>{case_.title}</span>
                  <StatusBadge $status={getStatusStyle(case_.status)}>{getStatusText(case_.status)}</StatusBadge>
                  <PriorityTag $priority={case_.priority}>{getPriorityText(case_.priority)}</PriorityTag>
                  <span style={{ display: 'flex', alignItems: 'center', gap: '0.35rem', color: '#94a3b8' }}>
                    {case_.userProgress?.lastActivity ? formatDate(case_.userProgress.lastActivity) : formatDate(case_.createdAt)}
                    <ArrowRight size={16} />
                  </span>
                </CaseRow>
              ))}
            </CaseTable>
          </Panel>

          <Panel>
            <PanelHeader>
              <Radio size={16} />
              {t('recentHistory')}
            </PanelHeader>
            <ActivityList>
              {dashboard.recentActivities.length === 0 && (
                <span style={{ color: 'rgba(148,163,184,0.8)' }}>{t('noRecentActivity')}</span>
              )}
              {dashboard.recentActivities.map((activity, index) => (
                <ActivityItem key={activity.caseId ?? index}>
                  <ActivityIcon>
                    {activity.caseId ? <Radio size={16} color="#93c5fd" /> : <Activity size={16} color="#93c5fd" />}
                  </ActivityIcon>
                  <ActivityText>
                    <span>{activity.description}</span>
                    <ActivityTimestamp>{formatDate(activity.date)}</ActivityTimestamp>
                  </ActivityText>
                </ActivityItem>
              ))}
            </ActivityList>
          </Panel>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
          <Panel>
            <PanelHeader>
              <Layers size={16} />
              {t('divisionHeatmap')}
            </PanelHeader>
            {(['Critical', 'High', 'Medium', 'Low'] as const).map(priority => (
              <HeatmapBar key={priority}>
                <span style={{ width: '85px', fontSize: '0.85rem', color: 'rgba(148,163,184,0.9)' }}>
                  {getPriorityText(priority)}
                </span>
                <HeatmapTrack>
                  <HeatmapFill
                    $priority={priority}
                    $width={(priorityBreakdown[priority] / totalCases) * 100}
                  />
                </HeatmapTrack>
                <span style={{ fontSize: '0.85rem', color: 'rgba(226,232,240,0.85)' }}>
                  {priorityBreakdown[priority]}
                </span>
              </HeatmapBar>
            ))}
          </Panel>

          <Panel>
            <PanelHeader>
              <Target size={16} />
              {t('evidenceProgress')}
            </PanelHeader>
            <ProgressTable>
              {progressEntries.length === 0 && (
                <span style={{ color: 'rgba(148,163,184,0.8)' }}>{t('noDataAvailable')}</span>
              )}
              {progressEntries.map(entry => (
                <ProgressRow key={entry.id}>
                  <span style={{ fontSize: '0.9rem', color: '#e2e8f0' }}>{entry.title}</span>
                  <ProgressMeta>
                    <span>{entry.completion}%</span>
                    <span>{entry.evidences} {t('evidence')} / {entry.interviews} {t('witnesses')}</span>
                  </ProgressMeta>
                  <ProgressTrack>
                    <ProgressFill $value={entry.completion} />
                  </ProgressTrack>
                </ProgressRow>
              ))}
            </ProgressTable>
          </Panel>

          <Panel>
            <PanelHeader>
              <AlertTriangle size={16} />
              {t('commandBulletins')}
            </PanelHeader>
            <BulletinList>
              {dashboard.recentActivities.slice(0, 3).map((activity, index) => {
                const relatedCaseId = activity.caseId
                return (
                  <BulletinCard key={`bulletin-${relatedCaseId ?? index}`}>
                    <AlertTriangle size={20} color="#fbbf24" />
                    <BulletinContent>
                      <span>{activity.description}</span>
                      <ActivityTimestamp>{formatDate(activity.date)}</ActivityTimestamp>
                    </BulletinContent>
                    {relatedCaseId && (
                      <BulletinCTA onClick={() => handleCaseClick(relatedCaseId)}>
                        {t('viewDossier')}
                        <ArrowRight size={14} />
                      </BulletinCTA>
                    )}
                  </BulletinCard>
                )
              })}
            </BulletinList>
          </Panel>
        </div>
      </ContentGrid>
    </PageContainer>
  )
}

export default DashboardPage