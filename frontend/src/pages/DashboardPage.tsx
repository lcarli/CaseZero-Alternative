import { useNavigate } from 'react-router-dom'
import { useState, useEffect } from 'react'
import styled from 'styled-components'
import { useAuth } from '../hooks/useAuthContext'
import { casesApi } from '../services/api'
import type { Dashboard } from '../services/api'

const PageContainer = styled.div`
  min-height: 100vh;
  background: linear-gradient(135deg, #0a0f23 0%, #1a2140 25%, #2a3458 50%, #1a2140 75%, #0a0f23 100%);
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  color: white;
  padding: 1rem;
  box-sizing: border-box;
  
  @media (max-width: 768px) {
    padding: 0.5rem;
  }
`

const Header = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.5rem;
  padding: 1rem;
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 1rem;
  backdrop-filter: blur(10px);
  
  @media (max-width: 768px) {
    flex-direction: column;
    gap: 1rem;
    padding: 0.75rem;
    margin-bottom: 1rem;
  }
`

const UserInfo = styled.div`
  display: flex;
  align-items: center;
  gap: 1rem;
`

const Avatar = styled.div`
  width: 50px;
  height: 50px;
  border-radius: 50%;
  background: linear-gradient(135deg, #3498db, #2980b9);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.5rem;
  font-weight: bold;
`

const UserDetails = styled.div`
  h2 {
    margin: 0 0 0.25rem 0;
    font-size: 1.2rem;
    color: white;
  }
  
  p {
    margin: 0;
    font-size: 0.9rem;
    color: rgba(52, 152, 219, 0.8);
  }
`

const LogoutButton = styled.button`
  padding: 0.5rem 1rem;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.3);
  border-radius: 0.5rem;
  color: white;
  cursor: pointer;
  font-size: 0.9rem;
  transition: all 0.3s ease;
  
  &:hover {
    background: rgba(255, 255, 255, 0.2);
  }
`

const Dashboard = styled.div`
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 1.5rem;
  
  @media (max-width: 1200px) {
    grid-template-columns: 1fr;
    gap: 1rem;
  }
`

const MainContent = styled.div`
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  
  @media (max-width: 768px) {
    gap: 1rem;
  }
`

const Sidebar = styled.div`
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  
  @media (max-width: 768px) {
    gap: 1rem;
  }
`

const Card = styled.div`
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 1rem;
  padding: 1.5rem;
  backdrop-filter: blur(10px);
  
  @media (max-width: 768px) {
    padding: 1rem;
    border-radius: 0.5rem;
  }
`

const CardHeader = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.5rem;
  
  h3 {
    margin: 0;
    color: white;
    font-size: 1.3rem;
  }
`

const StatsGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 1rem;
  margin-bottom: 1.5rem;
  
  @media (max-width: 768px) {
    grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
    gap: 0.75rem;
    margin-bottom: 1rem;
  }
`

const StatCard = styled.div`
  background: rgba(52, 152, 219, 0.1);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 0.5rem;
  padding: 1rem;
  text-align: center;
  
  .stat-value {
    font-size: 1.5rem;
    font-weight: bold;
    color: rgba(52, 152, 219, 0.9);
    margin-bottom: 0.5rem;
  }
  
  .stat-label {
    font-size: 0.8rem;
    color: rgba(255, 255, 255, 0.8);
  }
  
  @media (max-width: 768px) {
    padding: 0.75rem;
    
    .stat-value {
      font-size: 1.25rem;
    }
    
    .stat-label {
      font-size: 0.75rem;
    }
  }
`

const CaseList = styled.div`
  display: flex;
  flex-direction: column;
  gap: 1rem;
`

const CaseItem = styled.div`
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 0.5rem;
  padding: 1rem;
  cursor: pointer;
  transition: all 0.3s ease;
  
  &:hover {
    background: rgba(255, 255, 255, 0.1);
    border-color: rgba(52, 152, 219, 0.5);
  }
  
  .case-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 0.5rem;
  }
  
  .case-id {
    font-weight: bold;
    color: rgba(52, 152, 219, 0.9);
  }
  
  .case-status {
    padding: 0.25rem 0.5rem;
    border-radius: 0.25rem;
    font-size: 0.8rem;
    font-weight: 500;
    
    &.open {
      background: rgba(231, 76, 60, 0.2);
      color: rgba(231, 76, 60, 0.9);
    }
    
    &.in-progress {
      background: rgba(241, 196, 15, 0.2);
      color: rgba(241, 196, 15, 0.9);
    }
    
    &.resolved {
      background: rgba(46, 204, 113, 0.2);
      color: rgba(46, 204, 113, 0.9);
    }
  }
  
  .case-title {
    color: white;
    margin-bottom: 0.25rem;
    font-size: 1rem;
  }
  
  .case-priority {
    font-size: 0.8rem;
    color: rgba(255, 255, 255, 0.7);
  }
`

const Button = styled.button`
  padding: 0.75rem 1.5rem;
  background: linear-gradient(135deg, #3498db, #2980b9);
  border: none;
  border-radius: 0.5rem;
  color: white;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: all 0.3s ease;
  
  &:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 15px rgba(52, 152, 219, 0.3);
  }
`

const DashboardPage = () => {
  const navigate = useNavigate()
  const { user, logout } = useAuth()
  const [dashboard, setDashboard] = useState<Dashboard | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    const loadDashboard = async () => {
      try {
        const data = await casesApi.getDashboard()
        setDashboard(data)
      } catch (err) {
        setError('Erro ao carregar dashboard')
        console.error('Dashboard error:', err)
      } finally {
        setIsLoading(false)
      }
    }

    loadDashboard()
  }, [])

  const handleLogout = () => {
    logout()
    navigate('/')
  }

  const handleCaseClick = (caseId: string) => {
    navigate(`/desktop/${caseId}`)
  }

  const getStatusText = (status: string) => {
    switch (status) {
      case 'Open': return 'Aberto'
      case 'InProgress': return 'Em Andamento'
      case 'Resolved': return 'Resolvido'
      case 'Closed': return 'Fechado'
      default: return status
    }
  }

  const getStatusClass = (status: string) => {
    switch (status) {
      case 'Open': return 'open'
      case 'InProgress': return 'in-progress'
      case 'Resolved': return 'resolved'
      case 'Closed': return 'resolved'
      default: return 'open'
    }
  }

  const getPriorityText = (priority: string) => {
    switch (priority) {
      case 'Low': return 'Baixa'
      case 'Medium': return 'Média'
      case 'High': return 'Alta'
      case 'Critical': return 'Crítica'
      default: return priority
    }
  }

  if (isLoading) {
    return (
      <PageContainer>
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>
          <div style={{ color: 'white', fontSize: '1.2rem' }}>Carregando...</div>
        </div>
      </PageContainer>
    )
  }

  if (error) {
    return (
      <PageContainer>
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>
          <div style={{ color: 'rgba(231, 76, 60, 0.9)', fontSize: '1.2rem' }}>{error}</div>
        </div>
      </PageContainer>
    )
  }

  if (!dashboard) {
    return null
  }

  return (
    <PageContainer>
      <Header>
        <UserInfo>
          <Avatar>{user?.firstName?.charAt(0)}{user?.lastName?.charAt(0)}</Avatar>
          <UserDetails>
            <h2>{user?.firstName} {user?.lastName}</h2>
            <p>{user?.department} • Badge #{user?.badgeNumber} • Rank: Detective</p>
          </UserDetails>
        </UserInfo>
        <LogoutButton onClick={handleLogout}>
          Sair do Sistema
        </LogoutButton>
      </Header>

      <Dashboard>
        <MainContent>
          <Card>
            <CardHeader>
              <h3>📊 Estatísticas de Performance</h3>
            </CardHeader>
            <StatsGrid>
              <StatCard>
                <div className="stat-value">{dashboard.stats.casesResolved}</div>
                <div className="stat-label">Casos Resolvidos</div>
              </StatCard>
              <StatCard>
                <div className="stat-value">{dashboard.stats.casesActive}</div>
                <div className="stat-label">Casos Ativos</div>
              </StatCard>
              <StatCard>
                <div className="stat-value">{dashboard.stats.successRate}%</div>
                <div className="stat-label">Taxa de Sucesso</div>
              </StatCard>
              <StatCard>
                <div className="stat-value">{dashboard.stats.averageRating}</div>
                <div className="stat-label">Avaliação Média</div>
              </StatCard>
            </StatsGrid>
          </Card>

          <Card>
            <CardHeader>
              <h3>📁 Casos Disponíveis</h3>
              <Button onClick={() => navigate('/desktop')}>
                Abrir Ambiente de Trabalho
              </Button>
            </CardHeader>
            <CaseList>
              {dashboard.cases.map(case_ => (
                <CaseItem 
                  key={case_.id} 
                  onClick={() => handleCaseClick(case_.id)}
                >
                  <div className="case-header">
                    <span className="case-id">{case_.id}</span>
                    <span className={`case-status ${getStatusClass(case_.status)}`}>
                      {getStatusText(case_.status)}
                    </span>
                  </div>
                  <div className="case-title">{case_.title}</div>
                  <div className="case-priority">Prioridade: {getPriorityText(case_.priority)}</div>
                  {case_.userProgress?.lastActivity && (
                    <div className="case-last-session" style={{ 
                      fontSize: '0.75rem', 
                      color: 'rgba(52, 152, 219, 0.8)', 
                      marginTop: '0.25rem' 
                    }}>
                      Última sessão: {new Date(case_.userProgress.lastActivity).toLocaleString('pt-BR')}
                    </div>
                  )}
                </CaseItem>
              ))}
            </CaseList>
          </Card>
        </MainContent>

        <Sidebar>
          <Card>
            <CardHeader>
              <h3>🎯 Objetivos Semanais</h3>
            </CardHeader>
            <div>
              <p style={{ color: 'rgba(255, 255, 255, 0.8)', marginBottom: '1rem' }}>
                Progresso das metas desta semana:
              </p>
              <div style={{ marginBottom: '1rem' }}>
                <div style={{ color: 'rgba(52, 152, 219, 0.9)', marginBottom: '0.5rem' }}>
                  Resolver 3 casos • 2/3 ✓
                </div>
                <div style={{ background: 'rgba(255, 255, 255, 0.1)', borderRadius: '0.25rem', height: '8px' }}>
                  <div style={{ background: 'linear-gradient(135deg, #3498db, #2980b9)', borderRadius: '0.25rem', height: '100%', width: '67%' }}></div>
                </div>
              </div>
              <div style={{ marginBottom: '1rem' }}>
                <div style={{ color: 'rgba(52, 152, 219, 0.9)', marginBottom: '0.5rem' }}>
                  Coletar 10 evidências • 8/10 ✓
                </div>
                <div style={{ background: 'rgba(255, 255, 255, 0.1)', borderRadius: '0.25rem', height: '8px' }}>
                  <div style={{ background: 'linear-gradient(135deg, #3498db, #2980b9)', borderRadius: '0.25rem', height: '100%', width: '80%' }}></div>
                </div>
              </div>
            </div>
          </Card>

          <Card>
            <CardHeader>
              <h3>📈 Histórico Recente</h3>
            </CardHeader>
            <div>
              <div style={{ color: 'rgba(255, 255, 255, 0.8)', fontSize: '0.9rem' }}>
                {dashboard.recentActivities.map((activity, index) => (
                  <p key={index}>• {activity.description}</p>
                ))}
              </div>
            </div>
          </Card>
        </Sidebar>
      </Dashboard>
    </PageContainer>
  )
}

export default DashboardPage