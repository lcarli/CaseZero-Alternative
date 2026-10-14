import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react'
import '@testing-library/jest-dom'
import { LanguageProvider } from '../contexts/LanguageContext'
import { TimeProvider } from '../contexts/TimeContext'
import { useTimeContext } from '../hooks/useTimeContext'

const api = vi.hoisted(() => ({ getGameClock: vi.fn(), pauseGameClock: vi.fn(), resumeGameClock: vi.fn() }))
vi.mock('../services/api', () => ({ caseSessionApi: api }))
const initial = {
  gameTime: '2024-01-01T08:20:00Z', elapsedMinutes: 20, isRunning: true, multiplier: 60,
  asOfUtc: '2026-10-13T20:00:00Z',
}
function Probe() {
  const { gameTime, elapsedMinutes, isRunning, pauseTime, resumeTime, error } = useTimeContext()
  return <div>
    <span data-testid="elapsed">{elapsedMinutes.toFixed(0)}</span>
    <span data-testid="game-time">{gameTime.toISOString()}</span>
    <span data-testid="running">{String(isRunning)}</span>
    <button onClick={() => { void pauseTime() }}>pause</button>
    <button onClick={() => { void resumeTime() }}>resume</button>
    {error && <p role="alert">{error}</p>}
  </div>
}
function mount() {
  localStorage.setItem('casezero-language', 'en-US')
  return render(<LanguageProvider><TimeProvider caseId="case_clock" initialGameClock={initial}><Probe /></TimeProvider></LanguageProvider>)
}

describe('authoritative game clock', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-10-13T20:00:00Z'))
    vi.resetAllMocks()
    api.getGameClock.mockResolvedValue(initial)
  })
  afterEach(() => { cleanup(); vi.useRealTimers() })

  it('continues accumulated game minutes rather than restarting time on reopen', async () => {
    mount()
    await act(async () => { await Promise.resolve() })
    expect(screen.getByTestId('elapsed')).toHaveTextContent('20')
    await act(async () => { vi.advanceTimersByTime(2000) })
    expect(screen.getByTestId('elapsed')).toHaveTextContent('22')
    expect(screen.getByTestId('game-time')).toHaveTextContent('2024-01-01T08:22:00.000Z')
  })

  it('uses server pause/resume snapshots and does not add paused real time', async () => {
    mount()
    await act(async () => { await Promise.resolve() })
    const paused = { ...initial, isRunning: false, elapsedMinutes: 21, gameTime: '2024-01-01T08:21:00Z' }
    api.pauseGameClock.mockResolvedValue({ gameClock: paused })
    api.getGameClock.mockResolvedValue(paused)
    fireEvent.click(screen.getByText('pause'))
    await act(async () => { await Promise.resolve() })
    await act(async () => { vi.advanceTimersByTime(3000) })
    expect(api.pauseGameClock).toHaveBeenCalledWith('case_clock')
    expect(screen.getByTestId('elapsed')).toHaveTextContent('21')
    expect(screen.getByTestId('running')).toHaveTextContent('false')
    api.resumeGameClock.mockResolvedValue({ gameClock: { ...paused, isRunning: true } })
    fireEvent.click(screen.getByText('resume'))
    await act(async () => { await Promise.resolve() })
    await act(async () => { vi.advanceTimersByTime(1000) })
    expect(screen.getByTestId('elapsed')).toHaveTextContent('22')
    expect(api.resumeGameClock).toHaveBeenCalledWith('case_clock')
  })

  it('surfaces a rejected pause without pretending the server clock stopped', async () => {
    mount()
    await act(async () => { await Promise.resolve() })
    api.pauseGameClock.mockRejectedValue(new Error('Disconnected'))
    fireEvent.click(screen.getByText('pause'))
    await act(async () => { await Promise.resolve() })
    expect(screen.getByRole('alert')).toHaveTextContent('Unable to synchronize the case clock')
    expect(screen.getByTestId('running')).toHaveTextContent('true')
  })

  it('ignores an in-flight synchronization response after a confirmed pause', async () => {
    let completeSync!: (clock: typeof initial) => void
    api.getGameClock.mockReturnValueOnce(new Promise<typeof initial>(resolve => { completeSync = resolve }))
    api.pauseGameClock.mockResolvedValue({
      gameClock: { ...initial, isRunning: false, asOfUtc: '2026-10-13T20:00:01Z' },
    })
    mount()
    fireEvent.click(screen.getByText('pause'))
    await act(async () => { await Promise.resolve() })
    await act(async () => { completeSync({ ...initial, asOfUtc: '2026-10-13T20:00:02Z' }) })
    await act(async () => { vi.advanceTimersByTime(2000) })
    expect(screen.getByTestId('running')).toHaveTextContent('false')
    expect(screen.getByTestId('elapsed')).toHaveTextContent('20')
  })

  it('checkpoints the server clock on page exit with a keepalive request', async () => {
    api.pauseGameClock.mockResolvedValue({ gameClock: { ...initial, isRunning: false } })
    mount()
    await act(async () => { await Promise.resolve() })
    fireEvent(window, new Event('pagehide'))
    await act(async () => { await Promise.resolve() })
    expect(api.pauseGameClock).toHaveBeenCalledWith('case_clock', true)
  })
})
