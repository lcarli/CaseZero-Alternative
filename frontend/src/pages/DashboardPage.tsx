import { useNavigate } from 'react-router-dom'
import { useEffect, useMemo, useState } from 'react'
import styled from 'styled-components'
import { Briefcase, Clock, MapPin, ArrowRight, Shield, Target, Activity, FileText, CheckCircle } from 'react-feather'
import { useAuth } from '../hooks/useAuthContext'
import { useLanguage } from '../hooks/useLanguageContext'
import { casesV2Api } from '../services/api'
import type { CaseDashboardItem } from '../types/caseV2'
import LanguageSelector from '../components/LanguageSelector'
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

  &:hover {
    border-color: rgba(248, 113, 113, 0.8);
    background: rgba(248, 113, 113, 0.2);
  }
`

const Panel = styled.section`
  background: rgba(8, 12, 28, 0.75);
  border: 1px solid rgba(56, 189, 248, 0.15);
  border-radius: 1.25rem;
  padding: clamp(1rem, 2vw, 1.5rem);
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
  position: relative;
  z-index: 1;
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

const CaseGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1rem;
`

const CaseCard = styled.button`
  text-align: left;
  background: rgba(7, 11, 26, 0.7);
  border: 1px solid rgba(56, 189, 248, 0.15);
  border-radius: 1rem;
  padding: 1rem;
  color: inherit;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  transition: background 0.15s ease, border-color 0.15s ease;

  &:hover {
    background: rgba(15, 23, 42, 0.95);
    border-color: rgba(56, 189, 248, 0.4);
  }
`

const CardTitle = styled.div`
  font-size: 1rem;
  font-weight: 600;
  color: #e0f2fe;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
`

const CardDescription = styled.p`
  margin: 0;
  font-size: 0.85rem;
  line-height: 1.4;
  color: rgba(203, 213, 225, 0.85);
`

const MetaRow = styled.div`
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  font-size: 0.78rem;
  color: rgba(148, 163, 184, 0.95);
`

const MetaItem = styled.span`
  display: inline-flex;
  align-items: center;
  gap: 0.3rem;
  padding: 0.25rem 0.55rem;
  border-radius: 999px;
  background: rgba(15, 23, 42, 0.7);
  border: 1px solid rgba(56, 189, 248, 0.12);
`

const TagRow = styled.div`
  display: flex;
  flex-wrap: wrap;
  gap: 0.3rem;
`

const Tag = styled.span`
  font-size: 0.7rem;
  padding: 0.2rem 0.5rem;
  border-radius: 999px;
  background: rgba(56, 189, 248, 0.12);
  color: #bfdbfe;
  border: 1px solid rgba(56, 189, 248, 0.25);
`

const EmptyMessage = styled.div`
  padding: 2rem;
  text-align: center;
  color: rgba(148, 163, 184, 0.8);
  font-size: 0.95rem;
`

const ErrorMessage = styled.div`
  padding: 2rem;
  text-align: center;
  color: #fecaca;
`

const StatsGrid = styled.div`
  position: relative;
  z-index: 1;
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: clamp(0.75rem, 2vw, 1.25rem);
  margin-bottom: clamp(1rem, 3vw, 2rem);
`

const StatCard = styled.div`
  background: rgba(8, 12, 28, 0.7);
  border: 1px solid rgba(56, 189, 248, 0.2);
  border-radius: 1rem;
  padding: 1rem 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
`

const StatLabel = styled.span`
  font-size: 0.7rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: rgba(191, 219, 254, 0.85);
`

const StatValue = styled.div`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 1.65rem;
  font-weight: 600;
  color: #f8fafc;
`

const SecondaryLayout = styled.div`
  position: relative;
  z-index: 1;
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: clamp(1rem, 2vw, 1.5rem);
  margin-top: clamp(1rem, 3vw, 2rem);

  @media (max-width: 900px) {
    grid-template-columns: 1fr;
  }
`

const ActivityList = styled.ul`
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
`

const ActivityItem = styled.li`
  background: rgba(8, 12, 28, 0.5);
  border: 1px solid rgba(56, 189, 248, 0.15);
  border-radius: 0.6rem;
  padding: 0.75rem 1rem;
  font-size: 0.85rem;
  color: rgba(226, 232, 240, 0.9);
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
`

const ActivityMeta = styled.span`
  font-size: 0.7rem;
  color: rgba(148, 163, 184, 0.8);
`

const DashboardPage = () => {
  const navigate = useNavigate()
  const { user, logout } = useAuth()
  const { t } = useLanguage()
  const [cases, setCases] = useState<CaseDashboardItem[]>([])
  const [stats, setStats] = useState<{ casesResolved: number; casesActive: number; successRate: number; averageRating: number } | null>(null)
  const [activities, setActivities] = useState<Array<{ description: string; date: string; type?: string; caseId?: string }>>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    const loadDashboard = async () => {
      setIsLoading(true)
      setError('')
      try {
        const data = await casesV2Api.getDashboard()
        setCases(data?.cases ?? [])
        setStats(data?.stats ?? null)
        setActivities(data?.recentActivities ?? [])
      } catch (err) {
        console.error('Failed to load dashboard:', err)
        setError(t('error'))
      } finally {
        setIsLoading(false)
      }
    }

    loadDashboard()
  }, [t])

  // Counts derived from the cases list — works even if backend stats are stubbed.
  const totals = useMemo(() => {
    const total = cases.length
    const byRank = cases.reduce<Record<string, number>>((acc, c) => {
      const r = c.requiredRank || 'Unknown'
      acc[r] = (acc[r] ?? 0) + 1
      return acc
    }, {})
    return { total, byRank }
  }, [cases])

  const handleLogout = () => {
    logout()
    navigate('/')
  }

  const handleCaseClick = (caseId: string) => {
    navigate(`/desktop/${caseId}`)
  }

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
          </IdentityText>
        </IdentityBlock>
        <HeaderControls>
          <LanguageSelector appearance="landing" />
          <LogoutButton onClick={handleLogout}>{t('logout')}</LogoutButton>
        </HeaderControls>
      </Header>

      <StatsGrid>
        <StatCard>
          <StatLabel>{t('casesActive')}</StatLabel>
          <StatValue><Briefcase size={20} />{stats?.casesActive ?? totals.total}</StatValue>
        </StatCard>
        <StatCard>
          <StatLabel>{t('casesResolved')}</StatLabel>
          <StatValue><CheckCircle size={20} />{stats?.casesResolved ?? 0}</StatValue>
        </StatCard>
        <StatCard>
          <StatLabel>{t('successRate')}</StatLabel>
          <StatValue><Target size={20} />{stats?.successRate ?? 0}%</StatValue>
        </StatCard>
        <StatCard>
          <StatLabel>{t('averageRating')}</StatLabel>
          <StatValue><Activity size={20} />{stats?.averageRating ?? 0}</StatValue>
        </StatCard>
      </StatsGrid>

      <Panel>
        <PanelHeader>
          <Briefcase size={16} />
          {t('availableCases')}
        </PanelHeader>

        {isLoading && <EmptyMessage>{t('loading')}</EmptyMessage>}
        {!isLoading && error && <ErrorMessage>{error}</ErrorMessage>}
        {!isLoading && !error && cases.length === 0 && (
          <EmptyMessage>{t('dashboardEmpty')}</EmptyMessage>
        )}

        {!isLoading && !error && cases.length > 0 && (
          <CaseGrid>
            {cases.map(c => (
              <CaseCard key={c.caseId} onClick={() => handleCaseClick(c.caseId)}>
                <CardTitle>
                  <span>{c.title}</span>
                  <ArrowRight size={16} />
                </CardTitle>
                <CardDescription>{c.description}</CardDescription>
                <MetaRow>
                  <MetaItem>
                    <MapPin size={12} />
                    {c.location}
                  </MetaItem>
                  <MetaItem>
                    <Shield size={12} />
                    {t('dashboardDifficulty')}: {c.difficulty}
                  </MetaItem>
                  <MetaItem>
                    <Shield size={12} />
                    {t('dashboardRequiredRank')}: {c.requiredRank}
                  </MetaItem>
                  {typeof c.estimatedDurationMinutes === 'number' && (
                    <MetaItem>
                      <Clock size={12} />
                      {c.estimatedDurationMinutes} min
                    </MetaItem>
                  )}
                </MetaRow>
                {c.tags && c.tags.length > 0 && (
                  <TagRow>
                    {c.tags.map(tag => (
                      <Tag key={tag}>{tag}</Tag>
                    ))}
                  </TagRow>
                )}
              </CaseCard>
            ))}
          </CaseGrid>
        )}
      </Panel>

      <SecondaryLayout>
        <Panel>
          <PanelHeader>
            <Activity size={16} />
            {t('recentHistory')}
          </PanelHeader>
          {activities.length === 0 ? (
            <EmptyMessage>{t('noRecentActivity')}</EmptyMessage>
          ) : (
            <ActivityList>
              {activities.slice(0, 8).map((a, i) => (
                <ActivityItem key={`${a.date}-${i}`}>
                  <span>{a.description}</span>
                  <ActivityMeta>
                    {new Date(a.date).toLocaleString()}
                    {a.caseId ? ` · ${a.caseId}` : ''}
                  </ActivityMeta>
                </ActivityItem>
              ))}
            </ActivityList>
          )}
        </Panel>

        <Panel>
          <PanelHeader>
            <FileText size={16} />
            {t('casesByRank')}
          </PanelHeader>
          {Object.keys(totals.byRank).length === 0 ? (
            <EmptyMessage>{t('dashboardEmpty')}</EmptyMessage>
          ) : (
            <ActivityList>
              {Object.entries(totals.byRank).map(([rank, count]) => (
                <ActivityItem key={rank}>
                  <span style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <strong>{rank}</strong>
                    <span>{count}</span>
                  </span>
                </ActivityItem>
              ))}
            </ActivityList>
          )}
        </Panel>
      </SecondaryLayout>
    </PageContainer>
  )
}

export default DashboardPage
