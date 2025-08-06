import { useNavigate } from 'react-router-dom'
import styled from 'styled-components'

const PageContainer = styled.div`
  min-height: 100vh;
  background: linear-gradient(135deg, #0a0f23 0%, #1a2140 25%, #2a3458 50%, #1a2140 75%, #0a0f23 100%);
  background-image: 
    radial-gradient(circle at 20% 80%, rgba(52, 152, 219, 0.1) 0%, transparent 50%),
    radial-gradient(circle at 80% 20%, rgba(74, 158, 255, 0.08) 0%, transparent 50%);
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  color: white;
  padding: 2rem;
`

const Header = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
  padding: 1rem 2rem;
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 1rem;
  backdrop-filter: blur(10px);
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
  gap: 2rem;
  
  @media (max-width: 1200px) {
    grid-template-columns: 1fr;
  }
`

const MainContent = styled.div`
  display: flex;
  flex-direction: column;
  gap: 2rem;
`

const Sidebar = styled.div`
  display: flex;
  flex-direction: column;
  gap: 2rem;
`

const Card = styled.div`
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 1rem;
  padding: 2rem;
  backdrop-filter: blur(10px);
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
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
  margin-bottom: 2rem;
`

const StatCard = styled.div`
  background: rgba(52, 152, 219, 0.1);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 0.5rem;
  padding: 1.5rem;
  text-align: center;
  
  .stat-value {
    font-size: 2rem;
    font-weight: bold;
    color: rgba(52, 152, 219, 0.9);
    margin-bottom: 0.5rem;
  }
  
  .stat-label {
    font-size: 0.9rem;
    color: rgba(255, 255, 255, 0.8);
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

  const handleLogout = () => {
    // TODO: Implement actual logout
    navigate('/')
  }

  const handleCaseClick = (caseId: string) => {
    navigate(`/desktop/${caseId}`)
  }

  const mockCases = [
    {
      id: 'CASE-2024-001',
      title: 'Roubo no Banco Central',
      status: 'in-progress',
      priority: 'Alta',
      description: 'Investigação de roubo milionário'
    },
    {
      id: 'CASE-2024-002',
      title: 'Fraude Corporativa TechCorp',
      status: 'open',
      priority: 'Média',
      description: 'Suspeita de fraude contábil'
    },
    {
      id: 'CASE-2024-003',
      title: 'Homicídio no Porto',
      status: 'resolved',
      priority: 'Alta',
      description: 'Caso resolvido com sucesso'
    }
  ]

  return (
    <PageContainer>
      <Header>
        <UserInfo>
          <Avatar>JD</Avatar>
          <UserDetails>
            <h2>Detective John Doe</h2>
            <p>Investigation Division • Badge #4729</p>
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
                <div className="stat-value">23</div>
                <div className="stat-label">Casos Resolvidos</div>
              </StatCard>
              <StatCard>
                <div className="stat-value">5</div>
                <div className="stat-label">Casos Ativos</div>
              </StatCard>
              <StatCard>
                <div className="stat-value">92%</div>
                <div className="stat-label">Taxa de Sucesso</div>
              </StatCard>
              <StatCard>
                <div className="stat-value">4.8</div>
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
              {mockCases.map(case_ => (
                <CaseItem 
                  key={case_.id} 
                  onClick={() => handleCaseClick(case_.id)}
                >
                  <div className="case-header">
                    <span className="case-id">{case_.id}</span>
                    <span className={`case-status ${case_.status}`}>
                      {case_.status === 'open' ? 'Aberto' : 
                       case_.status === 'in-progress' ? 'Em Andamento' : 'Resolvido'}
                    </span>
                  </div>
                  <div className="case-title">{case_.title}</div>
                  <div className="case-priority">Prioridade: {case_.priority}</div>
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
                <p>• Caso BANK-2024-001 resolvido (Ontem)</p>
                <p>• Nova evidência coletada em TECH-2024-002 (2 dias)</p>
                <p>• Entrevista realizada com suspeito (3 dias)</p>
                <p>• Relatório forense recebido (5 dias)</p>
              </div>
            </div>
          </Card>
        </Sidebar>
      </Dashboard>
    </PageContainer>
  )
}

export default DashboardPage