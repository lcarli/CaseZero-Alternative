import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import '@testing-library/jest-dom'
import { LanguageProvider } from '../contexts/LanguageContext'
import { enUS, translations } from '../locales'
import type { Asset, Email } from '../types/caseV2'
import EmailApp from '../components/apps/EmailApp'

const mocks = vi.hoisted(() => ({
  getEmails: vi.fn(), openEmail: vi.fn(), downloadAttachment: vi.fn(), refreshCase: vi.fn(),
}))
let visibleEmails: Email[] = []
let visibleAssets: Asset[] = []

vi.mock('../services/api', () => ({
  emailsApi: { getEmails: mocks.getEmails, openEmail: mocks.openEmail, downloadAttachment: mocks.downloadAttachment },
}))
vi.mock('../contexts/CaseContext', () => ({
  useCase: () => ({
    currentCase: 'case_mail',
    state: { case: { emailStates: {} }, visibleEmails },
    refreshCase: mocks.refreshCase,
  }),
  useAssets: () => visibleAssets,
}))

function renderEmail(language = 'en-US') {
  localStorage.setItem('casezero-language', language)
  return render(<LanguageProvider><EmailApp /></LanguageProvider>)
}

describe('case email discovery', () => {
  beforeEach(() => {
    vi.resetAllMocks()
    localStorage.clear()
    visibleEmails = [{
      id: 'email.raw_01', from: 'Lab', to: ['Detective', 'Inspector'], subject: 'Lab result',
      body: 'Original laboratory content', sentAt: '2024-01-01T08:00:00Z',
      visibility: 'hidden', attachments: ['asset.hidden_report'],
    }]
    visibleAssets = []
    mocks.getEmails.mockResolvedValue([])
    mocks.openEmail.mockResolvedValue({
      emailId: 'email.raw_01', from: 'Lab', to: ['Detective', 'Inspector'], subject: 'Lab result',
      content: 'Original laboratory content', sentAt: '2024-01-01T08:00:00Z',
      attachments: ['asset.hidden_report'], isRead: true, openCount: 1,
    })
    mocks.downloadAttachment.mockResolvedValue(new Blob(['%PDF-1.7'], { type: 'application/pdf' }))
    mocks.refreshCase.mockResolvedValue(undefined)
  })

  afterEach(() => {
    cleanup()
    vi.restoreAllMocks()
  })

  it.each(['pt-BR', 'en-US', 'es-ES', 'fr-FR'])('preserves raw IDs and localizes attachment discovery in %s', async language => {
    const locale = translations[language]
    const page = renderEmail(language)
    expect(screen.getByRole('heading', { name: locale.emailInbox.replace('{n}', '1') })).toBeInTheDocument()
    fireEvent.click(screen.getByText(/Lab result/))
    const download = await screen.findByRole('button', { name: locale.emailDownload })
    expect(mocks.openEmail).toHaveBeenCalledWith('case_mail', 'email.raw_01')
    expect(screen.getByText('Detective; Inspector')).toBeInTheDocument()
    expect(screen.getByText(locale.emailAttachmentFallback.replace('{n}', '1'))).toBeInTheDocument()
    fireEvent.click(download)
    await waitFor(() => expect(mocks.downloadAttachment).toHaveBeenCalledWith('case_mail', 'email.raw_01', 'asset.hidden_report'))
    await waitFor(() => expect(mocks.refreshCase).toHaveBeenCalledTimes(2))

    visibleAssets = [{
      id: 'asset.hidden_report', type: 'pdf', title: 'Hidden laboratory report',
      uri: 'case://case_mail/assets/bundle/report.pdf', visibility: 'hidden',
    }]
    page.rerender(<LanguageProvider><EmailApp /></LanguageProvider>)
    expect(screen.getByText(`✓ Hidden laboratory report — ${locale.emailInFiles}`)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: locale.emailDownload })).toBeEnabled()
  })

  it('still records attachment downloads when the asset is already visible through another reveal', async () => {
    visibleAssets = [{
      id: 'asset.hidden_report', type: 'pdf', title: 'Visible report',
      uri: 'case://case_mail/assets/bundle/report.pdf', visibility: 'hidden',
    }]
    renderEmail()
    fireEvent.click(screen.getByText(/Lab result/))
    fireEvent.click(await screen.findByRole('button', { name: enUS.emailDownload }))
    await waitFor(() => expect(mocks.downloadAttachment).toHaveBeenCalledOnce())
    expect(mocks.downloadAttachment).toHaveBeenCalledWith('case_mail', 'email.raw_01', 'asset.hidden_report')
  })

  it('shows a localized error instead of claiming a rejected attachment was discovered', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    mocks.downloadAttachment.mockRejectedValue(new Error('Download rejected'))
    renderEmail()
    fireEvent.click(screen.getByText(/Lab result/))
    fireEvent.click(await screen.findByRole('button', { name: enUS.emailDownload }))
    expect(await screen.findByRole('alert')).toHaveTextContent(enUS.emailDownloadError)
    expect(mocks.refreshCase).toHaveBeenCalledTimes(1)
    expect(screen.getByRole('button', { name: enUS.emailDownload })).toBeEnabled()
  })

  it('keeps historical emails with missing timestamps readable', async () => {
    visibleEmails[0].sentAt = ''
    renderEmail()
    fireEvent.click(screen.getByText(/Lab result/))
    expect(await screen.findByText('Original laboratory content')).toBeInTheDocument()
    expect(mocks.openEmail).toHaveBeenCalledWith('case_mail', 'email.raw_01')
  })
})
