import { BrowserRouter as Router, Routes, Route } from 'react-router-dom'
import { WindowProvider } from './contexts/WindowContext'
import { AuthProvider } from './contexts/AuthContext'
import ProtectedRoute from './components/ProtectedRoute'
import HomePage from './pages/HomePage'
import LoginPage from './pages/LoginPage'
import RegisterPage from './pages/RegisterPage'
import DashboardPage from './pages/DashboardPage'
import DesktopPage from './pages/DesktopPage'

function App() {
  return (
    <AuthProvider>
      <WindowProvider>
        <Router>
          <Routes>
            <Route path="/" element={<HomePage />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
            <Route 
              path="/dashboard" 
              element={
                <ProtectedRoute>
                  <DashboardPage />
                </ProtectedRoute>
              } 
            />
            <Route 
              path="/desktop/:caseId?" 
              element={
                <ProtectedRoute>
                  <DesktopPage />
                </ProtectedRoute>
              } 
            />
          </Routes>
        </Router>
      </WindowProvider>
    </AuthProvider>
  )
}

export default App
