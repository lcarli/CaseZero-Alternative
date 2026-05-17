import { useNavigate } from 'react-router-dom'
import { useEffect, useMemo, useState } from 'react'
import styled from 'styled-components'
import { ArrowLeft, Award, BarChart2, Calendar, TrendingUp, FileText, User as UserIcon } from 'react-feather'
import { useAuth } from '../hooks/useAuthContext'
import { useLanguage } from '../hooks/useLanguageContext'
import { profileApi } from '../services/api'
import type { ProfileStats } from '../types/profile'
import LanguageSelector from '../components/LanguageSelector'
import departmentBadge from '../assets/LogoMetroPolice_transparent.png'

// ── Layout / shared styles (mirror DashboardPage palette) ────────────────────
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

const BackButton = styled.button`
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
  gap: 1rem;
  position: relative;
  z-index: 1;
  margin-bottom: clamp(1rem, 3vw, 1.5rem);
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

const EmptyMessage = styled.div`
  padding: 1.5rem;
  text-align: center;
  color: rgba(148, 163, 184, 0.8);
  font-size: 0.9rem;
`

// ── Table primitives ─────────────────────────────────────────────────────────
const TableWrap = styled.div`
  overflow-x: auto;
`

const Table = styled.table`
  width: 100%;
  border-collapse: collapse;
  font-size: 0.85rem;

  thead th {
    text-align: left;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    font-size: 0.7rem;
    color: rgba(148, 197, 255, 0.85);
    padding: 0.55rem 0.6rem;
    border-bottom: 1px solid rgba(56, 189, 248, 0.18);
  }

  tbody td {
    padding: 0.6rem;
    border-bottom: 1px solid rgba(56, 189, 248, 0.08);
    color: rgba(226, 232, 240, 0.92);
  }

  tbody tr:hover td {
    background: rgba(15, 23, 42, 0.5);
  }
`

const OutcomeChip = styled.span<{ tone: 'resolved' | 'practice' | 'open' }>`
  display: inline-flex;
  align-items: center;
  padding: 0.18rem 0.55rem;
  border-radius: 999px;
  font-size: 0.72rem;
  font-weight: 600;
  letter-spacing: 0.04em;
  border: 1px solid
    ${p => p.tone === 'resolved' ? 'rgba(34, 197, 94, 0.45)'
      : p.tone === 'practice'  ? 'rgba(234, 179, 8, 0.45)'
      :                          'rgba(148, 163, 184, 0.35)'};
  background:
    ${p => p.tone === 'resolved' ? 'rgba(34, 197, 94, 0.15)'
      : p.tone === 'practice'  ? 'rgba(234, 179, 8, 0.12)'
      :                          'rgba(148, 163, 184, 0.12)'};
  color:
    ${p => p.tone === 'resolved' ? '#bbf7d0'
      : p.tone === 'practice'  ? '#fde68a'
      :                          '#cbd5e1'};
`

// ── Score-by-difficulty bars ─────────────────────────────────────────────────
const ScoreList = styled.ul`
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.55rem;
`

const ScoreRow = styled.li`
  display: grid;
  grid-template-columns: 120px 1fr 90px;
  align-items: center;
  gap: 0.8rem;
  font-size: 0.85rem;
  color: rgba(226, 232, 240, 0.92);
`

const ScoreBar = styled.div`
  height: 10px;
  background: rgba(15, 23, 42, 0.85);
  border-radius: 999px;
  overflow: hidden;
  border: 1px solid rgba(56, 189, 248, 0.2);
`

const ScoreFill = styled.div<{ pct: number }>`
  width: ${p => Math.max(0, Math.min(100, p.pct))}%;
  height: 100%;
  background: linear-gradient(90deg, rgba(56, 189, 248, 0.7), rgba(34, 197, 94, 0.7));
`

const ScoreMeta = styled.span`
  font-variant-numeric: tabular-nums;
  text-align: right;
  color: rgba(191, 219, 254, 0.92);
`

// ── Category breakdown ───────────────────────────────────────────────────────
const CategoryGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 0.8rem;
`

const CategoryCard = styled.div`
  border: 1px solid rgba(56, 189, 248, 0.18);
  border-radius: 0.8rem;
  padding: 0.85rem 1rem;
  background: rgba(7, 11, 26, 0.6);
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
`

const CategoryLabel = styled.span`
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  color: rgba(148, 197, 255, 0.85);
`

const CategoryValue = styled.div`
  font-size: 1.2rem;
  font-weight: 600;
  color: #f8fafc;
  font-variant-numeric: tabular-nums;
`

const CategoryBar = styled.div`
  height: 8px;
  background: rgba(15, 23, 42, 0.85);
  border-radius: 999px;
  overflow: hidden;
  border: 1px solid rgba(56, 189, 248, 0.18);
`

const CategoryFill = styled.div<{ pct: number }>`
  width: ${p => Math.max(0, Math.min(100, p.pct))}%;
  height: 100%;
  background: linear-gradient(90deg, rgba(99, 102, 241, 0.7), rgba(56, 189, 248, 0.7));
`

const CategoryFootnote = styled.div`
  font-size: 0.72rem;
  color: rgba(148, 163, 184, 0.85);
  margin-top: 0.3rem;
`

// ── Rank history timeline ────────────────────────────────────────────────────
const Timeline = styled.ol`
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
`

const TimelineItem = styled.li`
  background: rgba(7, 11, 26, 0.55);
  border: 1px solid rgba(56, 189, 248, 0.16);
  border-radius: 0.65rem;
  padding: 0.7rem 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  font-size: 0.85rem;
  color: rgba(226, 232, 240, 0.92);
`

const TimelineMeta = styled.span`
  font-size: 0.72rem;
  color: rgba(148, 163, 184, 0.85);
`

// ── Promotion progress (re-used pattern) ─────────────────────────────────────
const PromotionWrap = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
`

const PromotionBar = styled.div`
  height: 12px;
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

const PromotionRow = styled.div`
  display: flex;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 0.6rem;
  font-size: 0.85rem;
  color: rgba(226, 232, 240, 0.9);
`

// ── Page ─────────────────────────────────────────────────────────────────────
const ProfilePage = () => {
  const navigate = useNavigate()
  const { user } = useAuth()
  const { t } = useLanguage()
  const [stats, setStats] = useState<ProfileStats | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let cancelled = false
    const run = async () => {
      setIsLoading(true)
      setError('')
      try {
        const data = await profileApi.getStats()
        if (!cancelled) setStats(data)
      } catch (err) {
        console.error('Failed to load profile stats:', err)
        if (!cancelled) setError(t('error'))
      } finally {
        if (!cancelled) setIsLoading(false)
      }
    }
    run()
    return () => { cancelled = true }
  }, [t])

  const formatDate = (s: string | null | undefined) => s ? new Date(s).toLocaleString() : '—'

  const outcomeFor = (row: ProfileStats['caseHistory'][number]) => {
    if (row.resolvedViaGraded) return { tone: 'resolved' as const, label: t('profileOutcomeResolved') }
    if (row.isResolved)        return { tone: 'practice' as const, label: t('profileOutcomeResolvedUngraded') }
    return                            { tone: 'open' as const,     label: t('profileOutcomeNotResolved') }
  }

  const category = stats?.categoryBreakdown
  const categoryCards = useMemo(() => {
    if (!category || category.sampleSize === 0) return []
    return [
      { key: 'culprit',   label: t('profileCategoryCulprit'),   value: category.culpritAvg,   max: category.culpritWeight   },
      { key: 'evidence',  label: t('profileCategoryEvidence'),  value: category.evidenceAvg,  max: category.evidenceWeight  },
      { key: 'analysis',  label: t('profileCategoryAnalysis'),  value: category.analysisAvg,  max: category.analysisWeight  },
      { key: 'questions', label: t('profileCategoryQuestions'), value: category.questionsAvg, max: category.questionsWeight },
    ]
  }, [category, t])

  return (
    <PageContainer>
      <BackgroundGrid />
      <Header>
        <IdentityBlock>
          <BadgeImage src={departmentBadge} alt={t('metropolitanPoliceDept')} />
          <IdentityText>
            <AgencyName>{t('metropolitanPoliceDept')}</AgencyName>
            <AgentName>
              {stats?.agent.firstName || user?.firstName} {stats?.agent.lastName || user?.lastName}
            </AgentName>
            <AgentMeta>
              <span>{stats?.agent.currentRank || user?.position || t('detective')}</span>
              {stats?.agent.badgeNumber && <span>Badge #{stats.agent.badgeNumber}</span>}
              <span>
                {t('profileLastPromotion')}: {stats?.agent.lastPromotionDate
                  ? formatDate(stats.agent.lastPromotionDate)
                  : t('profileNeverPromoted')}
              </span>
            </AgentMeta>
          </IdentityText>
        </IdentityBlock>
        <HeaderControls>
          <LanguageSelector appearance="landing" />
          <BackButton onClick={() => navigate('/dashboard')}>
            <ArrowLeft size={16} />{t('profileBackToDashboard')}
          </BackButton>
        </HeaderControls>
      </Header>

      {isLoading && <Panel><EmptyMessage>{t('loading')}</EmptyMessage></Panel>}
      {!isLoading && error && <Panel><EmptyMessage>{error}</EmptyMessage></Panel>}

      {!isLoading && !error && stats && (
        <>
          {/* Promotion */}
          <Panel>
            <PanelHeader><Award size={16} />{t('promotionProgress')}</PanelHeader>
            <PromotionWrap>
              <PromotionRow>
                <span><strong>{stats.promotion.currentRank}</strong>
                  {stats.promotion.nextRank ? ` → ${stats.promotion.nextRank}` : ''}
                </span>
                <span>{stats.promotion.casesResolved}
                  {stats.promotion.casesRequiredForNext != null ? ` / ${stats.promotion.casesRequiredForNext}` : ''}
                </span>
              </PromotionRow>
              <PromotionBar><PromotionFill pct={stats.promotion.progressPct} /></PromotionBar>
              <TimelineMeta>
                {stats.promotion.nextRank
                  ? t('casesToNextRank').replace('{count}', String(stats.promotion.casesRemaining ?? 0))
                  : t('maxRankReached')}
              </TimelineMeta>
            </PromotionWrap>
          </Panel>

          {/* Category breakdown */}
          <Panel>
            <PanelHeader><BarChart2 size={16} />{t('profileCategoryBreakdown')}</PanelHeader>
            {!category || category.sampleSize === 0 ? (
              <EmptyMessage>{t('profileCategoryNoData')}</EmptyMessage>
            ) : (
              <>
                <CategoryGrid>
                  {categoryCards.map(c => {
                    const pct = c.max > 0 ? (c.value / c.max) * 100 : 0
                    return (
                      <CategoryCard key={c.key}>
                        <CategoryLabel>{c.label}</CategoryLabel>
                        <CategoryValue>{(c.value * 100).toFixed(1)}% / {(c.max * 100).toFixed(0)}%</CategoryValue>
                        <CategoryBar><CategoryFill pct={pct} /></CategoryBar>
                      </CategoryCard>
                    )
                  })}
                </CategoryGrid>
                <CategoryFootnote>
                  {t('profileCategorySampleSize').replace('{count}', String(category.sampleSize))}
                </CategoryFootnote>
              </>
            )}
          </Panel>

          {/* Score by difficulty */}
          <Panel>
            <PanelHeader><TrendingUp size={16} />{t('profileScoreByDifficulty')}</PanelHeader>
            {stats.scoreByDifficulty.length === 0 ? (
              <EmptyMessage>{t('dashboardEmpty')}</EmptyMessage>
            ) : (
              <ScoreList>
                {stats.scoreByDifficulty.map(row => (
                  <ScoreRow key={row.difficulty}>
                    <span>{row.difficulty}</span>
                    <ScoreBar><ScoreFill pct={row.avgBestScore} /></ScoreBar>
                    <ScoreMeta>
                      {row.avgBestScore}% · {row.casesResolved}/{row.casesPlayed}
                    </ScoreMeta>
                  </ScoreRow>
                ))}
              </ScoreList>
            )}
          </Panel>

          {/* Case history table */}
          <Panel>
            <PanelHeader><FileText size={16} />{t('profileCaseHistory')}</PanelHeader>
            {stats.caseHistory.length === 0 ? (
              <EmptyMessage>{t('dashboardEmpty')}</EmptyMessage>
            ) : (
              <TableWrap>
                <Table>
                  <thead>
                    <tr>
                      <th>{t('profileColCase')}</th>
                      <th>{t('profileColDifficulty')}</th>
                      <th>{t('profileColAttempts')}</th>
                      <th>{t('profileColBestScore')}</th>
                      <th>{t('profileColOutcome')}</th>
                      <th>{t('profileColLastAttempt')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {stats.caseHistory.map(row => {
                      const o = outcomeFor(row)
                      return (
                        <tr key={row.caseId}>
                          <td>{row.title}</td>
                          <td>{row.difficulty}</td>
                          <td>{row.totalAttempts} ({row.gradedAttempts})</td>
                          <td>{row.bestScore}%</td>
                          <td><OutcomeChip tone={o.tone}>{o.label}</OutcomeChip></td>
                          <td>{formatDate(row.lastAttemptAt)}</td>
                        </tr>
                      )
                    })}
                  </tbody>
                </Table>
              </TableWrap>
            )}
          </Panel>

          {/* Rank history */}
          <Panel>
            <PanelHeader><UserIcon size={16} />{t('profileRankHistory')}</PanelHeader>
            {stats.rankHistory.length === 0 ? (
              <EmptyMessage>{t('dashboardEmpty')}</EmptyMessage>
            ) : (
              <Timeline>
                {stats.rankHistory.map((row, i) => (
                  <TimelineItem key={`${row.changedAt}-${i}`}>
                    <span>
                      {row.previousRank
                        ? <>{row.previousRank} → <strong>{row.newRank}</strong></>
                        : <><strong>{row.newRank}</strong> ({t('profileRankInitial')})</>}
                    </span>
                    <TimelineMeta>
                      {formatDate(row.changedAt)}{row.reason ? ` · ${row.reason}` : ''}
                    </TimelineMeta>
                  </TimelineItem>
                ))}
              </Timeline>
            )}
          </Panel>

          {/* Submissions timeline */}
          <Panel>
            <PanelHeader><Calendar size={16} />{t('profileSubmissionsTimeline')}</PanelHeader>
            {stats.submissionsTimeline.length === 0 ? (
              <EmptyMessage>{t('noRecentActivity')}</EmptyMessage>
            ) : (
              <TableWrap>
                <Table>
                  <thead>
                    <tr>
                      <th>{t('profileColLastAttempt')}</th>
                      <th>{t('profileColCase')}</th>
                      <th>{t('profileColDifficulty')}</th>
                      <th>{t('profileColBestScore')}</th>
                      <th>{t('profileColOutcome')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {stats.submissionsTimeline.map((row, i) => {
                      const tone: 'resolved' | 'practice' | 'open' =
                        row.type === 'resolved'           ? 'resolved' :
                        row.type === 'resolved_ungraded'  ? 'practice' :
                                                            'open'
                      const label =
                        row.type === 'resolved'           ? t('profileOutcomeResolved') :
                        row.type === 'resolved_ungraded'  ? t('profileOutcomeResolvedUngraded') :
                                                            t('profileOutcomeNotResolved')
                      return (
                        <tr key={`${row.date}-${i}`}>
                          <td>{formatDate(row.date)}</td>
                          <td>{row.caseTitle}</td>
                          <td>{row.difficulty}</td>
                          <td>{row.score}%</td>
                          <td><OutcomeChip tone={tone}>{label}</OutcomeChip></td>
                        </tr>
                      )
                    })}
                  </tbody>
                </Table>
              </TableWrap>
            )}
          </Panel>
        </>
      )}
    </PageContainer>
  )
}

export default ProfilePage
