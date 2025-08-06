import { BrowserRouter as Router, Routes, Route } from 'react-router-dom'
import { WindowProvider } from './contexts/WindowContext'
import HomePage from './pages/HomePage'
import LoginPage from './pages/LoginPage'
import RegisterPage from './pages/RegisterPage'
import DashboardPage from './pages/DashboardPage'
import DesktopPage from './pages/DesktopPage'

function App() {
  return (
    <WindowProvider>
      <Router>
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/desktop/:caseId?" element={<DesktopPage />} />
        </Routes>
      </Router>
    </WindowProvider>
  )
}

export default App
