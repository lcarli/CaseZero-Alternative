import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../hooks/useAuthContext'
import { useLanguage } from '../contexts/LanguageContext'

interface ProtectedRouteProps {
  children: React.ReactNode
  requiredRole?: string
}

const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ children, requiredRole }) => {
  const { user, isAuthenticated, isLoading } = useAuth()
  const { t } = useLanguage()
  const location = useLocation()

  if (isLoading) {
    // You could replace this with a proper loading component
    return <div>{t('loading')}</div>
  }

  if (!isAuthenticated) {
    // Redirect to login page with return url
    return <Navigate to="/login" state={{ from: location }} replace />
  }

  if (requiredRole && !user?.roles?.includes(requiredRole)) {
    return <Navigate to="/dashboard" replace />
  }

  return <>{children}</>
}

export default ProtectedRoute