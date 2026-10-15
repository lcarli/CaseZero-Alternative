import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import ProtectedRoute from '../components/ProtectedRoute'
import type { User } from '../services/api'

const player: User = {
  id: 'player',
  firstName: 'John',
  lastName: 'Doe',
  email: 'john.doe@fic-police.gov',
  personalEmail: 'john.doe.personal@example.com',
  emailVerified: true,
  roles: ['PLAYER']
}

let currentUser = player

vi.mock('../hooks/useAuthContext', () => ({
  useAuth: () => ({
    user: currentUser,
    isAuthenticated: true,
    isLoading: false
  })
}))

vi.mock('../contexts/LanguageContext', () => ({
  useLanguage: () => ({ t: (key: string) => key })
}))

const renderRoute = () => render(
  <MemoryRouter initialEntries={['/case-generation']}>
    <Routes>
      <Route path="/dashboard" element={<div>dashboard</div>} />
      <Route
        path="/case-generation"
        element={(
          <ProtectedRoute requiredRole="ADMIN">
            <div>generator</div>
          </ProtectedRoute>
        )}
      />
    </Routes>
  </MemoryRouter>
)

describe('ProtectedRoute roles', () => {
  beforeEach(() => {
    currentUser = player
  })

  it('redirects a player away from the admin route', () => {
    renderRoute()
    expect(screen.getByText('dashboard')).toBeTruthy()
  })

  it('allows an admin to open the generation route', () => {
    currentUser = { ...player, id: 'admin', roles: ['PLAYER', 'ADMIN'] }
    renderRoute()
    expect(screen.getByText('generator')).toBeTruthy()
  })
})
