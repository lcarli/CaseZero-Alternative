import { BrowserRouter as Router, Routes, Route } from 'react-router-dom'
import { WindowProvider } from './contexts/WindowContext'
import { AuthProvider } from './contexts/AuthContext'
import { LanguageProvider } from './contexts/LanguageContext'
import ErrorBoundary from './components/ui/ErrorBoundary'
import OfflineStatus from './components/ui/OfflineStatus'
import ProtectedRoute from './components/ProtectedRoute'
import HomePage from './pages/HomePage'
import LoginPage from './pages/LoginPage'
import RegisterPage from './pages/RegisterPage'
import EmailVerificationPage from './pages/EmailVerificationPage'
import DashboardPage from './pages/DashboardPage'
import DesktopPage from './pages/DesktopPage'
import GenerateCasePage from './pages/GenerateCasePage'

function App() {
  return (
    <LanguageProvider>
      <ErrorBoundary>
        <AuthProvider>
          <WindowProvider>
            <Router>
              <OfflineStatus />
              <Routes>
                <Route path="/" element={<HomePage />} />
                <Route path="/login" element={<LoginPage />} />
                <Route path="/register" element={<RegisterPage />} />
                <Route path="/verify-email" element={<EmailVerificationPage />} />
                <Route 
                  path="/dashboard" 
                  element={
                    <ProtectedRoute>
                      <DashboardPage />
                    </ProtectedRoute>
                  } 
                />
                <Route 
                  path="/generate-case" 
                  element={
                    <ProtectedRoute>
                      <GenerateCasePage />
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
      </ErrorBoundary>
    </LanguageProvider>
  )
}

export default App
