import { useNavigate } from 'react-router-dom'
import { useEffect, useMemo, useState } from 'react'
import styled from 'styled-components'
import { Briefcase, Clock, MapPin, ArrowRight, Shield, Target, Activity, FileText, CheckCircle, Plus, User as UserIcon, Award } from 'react-feather'
import { useAuth } from '../hooks/useAuthContext'
import { useLanguage } from '../hooks/useLanguageContext'
import { casesV2Api } from '../services/api'
import type { CaseDashboardItem, DashboardActivity, CasesByDifficultyBucket, PromotionProgress } from '../types/caseV2'
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

const NewCaseButton = styled.button`
  padding: 0.7rem 1.4rem;
  border-radius: 999px;
  border: 1px solid rgba(99, 102, 241, 0.5);
  background: linear-gradient(135deg, rgba(99, 102, 241, 0.18), rgba(56, 189, 248, 0.18));
  color: #c7d2fe;
  font-weight: 600;
  letter-spacing: 0.05em;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;

  &:hover {
    border-color: rgba(129, 140, 248, 0.9);
    background: linear-gradient(135deg, rgba(99, 102, 241, 0.3), rgba(56, 189, 248, 0.3));
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

const FilterBar = styled.div`
  display: flex;
  flex-wrap: wrap;
  gap: 0.85rem 1.2rem;
  align-items: center;
  margin: 0.85rem 0 1.1rem;
  padding: 0.65rem 0.85rem;
  background: rgba(8, 12, 28, 0.55);
  border: 1px solid rgba(56, 189, 248, 0.18);
  border-radius: 0.6rem;
`

const FilterLabel = styled.label`
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  font-size: 0.85rem;
  color: rgba(226, 232, 240, 0.85);

  select {
    background: rgba(2, 6, 23, 0.85);
    color: #e2e8f0;
    border: 1px solid rgba(56, 189, 248, 0.3);
    border-radius: 0.4rem;
    padding: 0.3rem 0.55rem;
    font-size: 0.85rem;
  }

  input[type='checkbox'] {
    accent-color: #38bdf8;
    transform: translateY(1px);
  }
`

const FilterCount = styled.span`
  margin-left: auto;
  font-size: 0.78rem;
  color: rgba(148, 197, 255, 0.7);
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

const ResolvedBadge = styled.span`
  font-size: 0.7rem;
  padding: 0.2rem 0.55rem;
  border-radius: 999px;
  background: rgba(34, 197, 94, 0.18);
  color: #bbf7d0;
  border: 1px solid rgba(34, 197, 94, 0.45);
  font-weight: 600;
  letter-spacing: 0.04em;
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
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

// ── Difficulty bucket chip (resolved / total) ─────────────────────────────
const DifficultyList = styled.ul`
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
`

const DifficultyRow = styled.li`
  display: flex;
  align-items: center;
  gap: 0.8rem;
  background: rgba(8, 12, 28, 0.5);
  border: 1px solid rgba(56, 189, 248, 0.15);
  border-radius: 0.55rem;
  padding: 0.5rem 0.75rem;
  font-size: 0.85rem;
`

const DifficultyLabel = styled.span`
  color: rgba(226, 232, 240, 0.92);
  min-width: 90px;
  text-transform: capitalize;
`

const DifficultyBar = styled.div`
  flex: 1;
  height: 8px;
  background: rgba(15, 23, 42, 0.85);
  border-radius: 999px;
  overflow: hidden;
  border: 1px solid rgba(56, 189, 248, 0.18);
`

const DifficultyFill = styled.div<{ pct: number }>`
  width: ${p => Math.max(0, Math.min(100, p.pct))}%;
  height: 100%;
  background: linear-gradient(90deg, rgba(56, 189, 248, 0.7), rgba(34, 197, 94, 0.7));
`

const DifficultyCount = styled.span`
  font-variant-numeric: tabular-nums;
  color: rgba(191, 219, 254, 0.95);
  min-width: 56px;
  text-align: right;
`

// ── Promotion progress block ──────────────────────────────────────────────
const PromotionWrap = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  font-size: 0.85rem;
  color: rgba(226, 232, 240, 0.9);
`

const PromotionLine = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 0.5rem;
  flex-wrap: wrap;
`

const PromotionRank = styled.strong`
  font-size: 1rem;
  color: #e0f2fe;
  letter-spacing: 0.04em;
`

const PromotionBar = styled.div`
  height: 10px;
  background: rgba(15, 23, 42, 0.85);
  border-radius: 999px;
  overflow: hidden;
  border: 1px solid rgba(99, 102, 241, 0.35);
`

const PromotionFill = styled.div<{ pct: number }>`
  width: ${p => Math.max(0, Math.min(100, p.pct))}%;
  height: 100%;
  background: linear-gradient(90deg, rgba(99, 102, 241, 0.85), rgba(56, 189, 248, 0.85));
`

const PromotionMeta = styled.span`
  font-size: 0.78rem;
  color: rgba(148, 163, 184, 0.9);
`

const DashboardPage = () => {
  const navigate = useNavigate()
  const { user, logout } = useAuth()
  const { t } = useLanguage()
  const [cases, setCases] = useState<CaseDashboardItem[]>([])
  const [stats, setStats] = useState<{ casesResolved: number; casesActive: number; successRate: number; averageRating: number } | null>(null)
  const [activities, setActivities] = useState<DashboardActivity[]>([])
  const [casesByDifficulty, setCasesByDifficulty] = useState<CasesByDifficultyBucket[]>([])
  const [promotion, setPromotion] = useState<PromotionProgress | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [filterDifficulty, setFilterDifficulty] = useState<string>('all')
  const [hideResolved, setHideResolved] = useState<boolean>(false)

  useEffect(() => {
    const loadDashboard = async () => {
      setIsLoading(true)
      setError('')
      try {
        const data = await casesV2Api.getDashboard()
        setCases(data?.cases ?? [])
        setStats(data?.stats ?? null)
        setActivities(data?.recentActivities ?? [])
        setCasesByDifficulty(data?.casesByDifficulty ?? [])
        setPromotion(data?.promotion ?? null)
      } catch (err) {
        console.error('Failed to load dashboard:', err)
        setError(t('error'))
      } finally {
        setIsLoading(false)
      }
    }

    loadDashboard()
  }, [t])

  // Total case count derived from list — used as a fallback for backend stats.
  const totals = useMemo(() => ({ total: cases.length }), [cases])

  const availableDifficulties = useMemo(() => {
    const set = new Set<string>()
    for (const c of cases) if (c.difficulty) set.add(c.difficulty)
    return Array.from(set)
  }, [cases])

  const filteredCases = useMemo(() => {
    return cases.filter(c => {
      if (filterDifficulty !== 'all' && c.difficulty !== filterDifficulty) return false
      if (hideResolved && c.isResolved) return false
      return true
    })
  }, [cases, filterDifficulty, hideResolved])

  const handleLogout = () => {
    logout()
    navigate('/')
  }

  const handleCaseClick = (caseId: string) => {
    navigate(`/desktop/${caseId}`)
  }

  // Render the activity row text, choosing a localized template based on `type`
  // or falling back to a legacy `description` if the backend still ships one.
  const renderActivity = (a: DashboardActivity): string => {
    if (a.description) return a.description
    const title = a.caseTitle ?? a.caseId ?? ''
    const template = (() => {
      switch (a.type) {
        case 'resolved':            return t('activityResolved')
        case 'resolved_ungraded':   return t('activityResolvedUngraded')
        case 'attempted':           return t('activityAttempted')
        case 'attempted_ungraded':  return t('activityAttemptedUngraded')
        default:                    return ''
      }
    })()
    return template.replace('{title}', title)
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
          <NewCaseButton onClick={() => navigate('/profile')}>
            <UserIcon size={16} />{t('viewProfile')}
          </NewCaseButton>
          <NewCaseButton onClick={() => navigate('/case-generation')}>
            <Plus size={16} />New Case
          </NewCaseButton>
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
          <StatValue><Activity size={20} />{stats?.averageRating ?? 0}%</StatValue>
        </StatCard>
      </StatsGrid>

      <Panel>
        <PanelHeader>
          <Briefcase size={16} />
          {t('availableCases')}
        </PanelHeader>

        {!isLoading && !error && cases.length > 0 && (
          <FilterBar>
            <FilterLabel>
              {t('filterByDifficulty')}:
              <select
                value={filterDifficulty}
                onChange={e => setFilterDifficulty(e.target.value)}
              >
                <option value="all">{t('allDifficulties')}</option>
                {availableDifficulties.map(d => (
                  <option key={d} value={d}>{d}</option>
                ))}
              </select>
            </FilterLabel>
            <FilterLabel>
              <input
                type="checkbox"
                checked={hideResolved}
                onChange={e => setHideResolved(e.target.checked)}
              />
              {t('hideResolvedCases')}
            </FilterLabel>
            <FilterCount>
              {filteredCases.length} / {cases.length}
            </FilterCount>
          </FilterBar>
        )}

        {isLoading && <EmptyMessage>{t('loading')}</EmptyMessage>}
        {!isLoading && error && <ErrorMessage>{error}</ErrorMessage>}
        {!isLoading && !error && cases.length === 0 && (
          <EmptyMessage>{t('dashboardEmpty')}</EmptyMessage>
        )}
        {!isLoading && !error && cases.length > 0 && filteredCases.length === 0 && (
          <EmptyMessage>{t('noCasesMatchFilters')}</EmptyMessage>
        )}

        {!isLoading && !error && filteredCases.length > 0 && (
          <CaseGrid>
            {filteredCases.map(c => (
              <CaseCard key={c.caseId} onClick={() => handleCaseClick(c.caseId)}>
                <CardTitle>
                  <span>{c.title}</span>
                  {c.isResolved
                    ? <ResolvedBadge><CheckCircle size={12} />{t('resolvedBadge')}</ResolvedBadge>
                    : <ArrowRight size={16} />}
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
                  <span>{renderActivity(a)}</span>
                  <ActivityMeta>
                    {new Date(a.date).toLocaleString()}
                    {a.caseId ? ` · ${a.caseId}` : ''}
                  </ActivityMeta>
                </ActivityItem>
              ))}
            </ActivityList>
          )}
        </Panel>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <Panel>
            <PanelHeader>
              <Award size={16} />
              {t('promotionProgress')}
            </PanelHeader>
            {promotion ? (
              <PromotionWrap>
                <PromotionLine>
                  <span>{t('currentRank')}</span>
                  <PromotionRank>{promotion.currentRank}</PromotionRank>
                </PromotionLine>
                <PromotionBar>
                  <PromotionFill pct={promotion.progressPct} />
                </PromotionBar>
                {promotion.nextRank ? (
                  <PromotionLine>
                    <PromotionMeta>
                      → {promotion.nextRank} · {promotion.casesResolved}
                      {promotion.casesRequiredForNext != null ? `/${promotion.casesRequiredForNext}` : ''}
                    </PromotionMeta>
                    <PromotionMeta>
                      {t('casesToNextRank').replace('{count}', String(promotion.casesRemaining ?? 0))}
                    </PromotionMeta>
                  </PromotionLine>
                ) : (
                  <PromotionMeta>{t('maxRankReached')}</PromotionMeta>
                )}
              </PromotionWrap>
            ) : (
              <EmptyMessage>{t('loading')}</EmptyMessage>
            )}
          </Panel>

          <Panel>
            <PanelHeader>
              <FileText size={16} />
              {t('casesByDifficulty')}
            </PanelHeader>
            {casesByDifficulty.length === 0 ? (
              <EmptyMessage>{t('dashboardEmpty')}</EmptyMessage>
            ) : (
              <DifficultyList>
                {casesByDifficulty.map(b => {
                  const pct = b.total > 0 ? (b.resolved / b.total) * 100 : 0
                  return (
                    <DifficultyRow key={b.difficulty}>
                      <DifficultyLabel>{b.difficulty}</DifficultyLabel>
                      <DifficultyBar><DifficultyFill pct={pct} /></DifficultyBar>
                      <DifficultyCount>{b.resolved}/{b.total}</DifficultyCount>
                    </DifficultyRow>
                  )
                })}
              </DifficultyList>
            )}
          </Panel>
        </div>
      </SecondaryLayout>
    </PageContainer>
  )
}

export default DashboardPage
