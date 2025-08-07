import React, { useState } from 'react'
import styled from 'styled-components'
import { useCase } from '../../hooks/useCaseContext'

const FileViewerContainer = styled.div`
  height: 100%;
  display: flex;
  flex-direction: column;
`

const TwoColumnLayout = styled.div`
  display: flex;
  height: 100%;
  gap: 1rem;
`

const LeftPanel = styled.div`
  width: 300px;
  min-width: 250px;
  max-width: 400px;
  display: flex;
  flex-direction: column;
  border-right: 1px solid rgba(255, 255, 255, 0.1);
  padding-right: 1rem;
`

const RightPanel = styled.div`
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
`

const FileExplorer = styled.div`
  display: flex;
  flex-direction: column;
  gap: 1rem;
`

const FolderHeader = styled.div`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 6px;
  cursor: pointer;
  transition: background 0.2s ease;

  &:hover {
    background: rgba(255, 255, 255, 0.1);
  }
`

const FolderIcon = styled.span`
  font-size: 16px;
`

const FolderName = styled.span`
  font-weight: 500;
`

const FileList = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding-left: 1.5rem;
`

const FileItem = styled.div`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem;
  border-radius: 4px;
  cursor: pointer;
  transition: background 0.2s ease;

  &:hover {
    background: rgba(255, 255, 255, 0.05);
  }
`

const FileIcon = styled.span`
  font-size: 14px;
`

const FileName = styled.span`
  color: rgba(255, 255, 255, 0.8);
`

const FileContent = styled.div`
  flex: 1;
  padding: 1rem;
  background: rgba(0, 0, 0, 0.2);
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  overflow-y: auto;
`

const ImagePreview = styled.div`
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
`

const ImagePlaceholder = styled.div`
  width: 100%;
  max-width: 300px;
  height: 200px;
  background: linear-gradient(135deg, #2a2a3e 0%, #1a1a2e 100%);
  border: 2px dashed rgba(255, 255, 255, 0.2);
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  color: rgba(255, 255, 255, 0.6);
`

const PDFPreview = styled.div`
  background: white;
  color: black;
  border-radius: 4px;
  padding: 1rem;
  max-height: 300px;
  overflow-y: auto;
  font-family: 'Times New Roman', serif;
  line-height: 1.6;
`

const FileInfo = styled.div`
  display: flex;
  gap: 1rem;
  font-size: 12px;
  color: rgba(255, 255, 255, 0.6);
  margin-bottom: 0.5rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
`

const FileInfoItem = styled.span`
  display: flex;
  align-items: center;
  gap: 0.25rem;
`

interface FileData {
  name: string
  icon: string
  type: string
  size: string
  modified: string
  content: string
  imageType?: string
}

interface FolderData {
  name: string
  icon: string
  files: FileData[]
}

interface CaseData {
  [key: string]: FolderData
}

const FileViewer: React.FC = () => {
  const [openFolders, setOpenFolders] = useState<Set<string>>(new Set(['case-files']))
  const [selectedFile, setSelectedFile] = useState<string | null>('case001.txt')
  const { currentCase } = useCase()

  // Updated data structure with case-specific content
  const getCaseData = (caseId: string): CaseData => {
    const caseFiles: { [key: string]: CaseData } = {
      'CASE-2024-001': {
        'case-files': {
          name: 'Case Files',
          icon: '📁',
          files: [
            { 
              name: 'case001.txt', 
              icon: '📄', 
              type: 'text',
              size: '2.4 KB',
              modified: '2024-01-15 14:30',
              content: `INCIDENT REPORT #001
====================

CASE ID: CASE-2024-001
DATE: January 15, 2024
TIME: 02:30 AM
LOCATION: Downtown Office Building, 123 Main Street
REPORTING OFFICER: Detective Sarah Johnson
STATUS: Under Investigation

INCIDENT SUMMARY:
Break-in reported at the Downtown Office Building. Security alarm triggered at 02:28 AM. Initial response by patrol units arrived at 02:35 AM.

DETAILS:
- Front door security lock was compromised
- Office on 3rd floor (Suite 301) was ransacked
- Security cameras show masked individual entering at 02:15 AM
- Suspect appeared to be searching for specific documents
- No signs of random theft - electronics and cash left untouched

EVIDENCE COLLECTED:
- Fingerprints from window frame
- Security footage (see evidence.jpg)
- Damaged lock mechanism
- Shoe prints in hallway

WITNESS STATEMENTS:
- Night security guard: John Matthews (see witness_statement.pdf)
- Neighboring business owner: Maria Rodriguez

NEXT STEPS:
- Process fingerprint evidence
- Interview additional witnesses
- Review complete security footage
- Check for similar incidents in area

ASSIGNED TO: Detective Unit 7
PRIORITY: High

===== TESASSETS INTEGRATION =====
This content is now available as a separate file:
/cases/TestAssets/CASE-2024-001/case-files/case001.txt

The system has been restructured to load data dynamically from 
individual files rather than hardcoded data structures.` 
            },
            { 
              name: 'evidence.jpg', 
              icon: '🖼️', 
              type: 'image',
              size: '1.2 MB',
              modified: '2024-01-15 02:35',
              content: 'Security camera footage showing masked suspect entering through main entrance. Clear view of suspect\'s build and clothing. Timestamp: 02:15:33 AM. Image quality: High definition. Additional details visible: dark clothing, approximately 5\'10" height, carrying small backpack.\n\n[Case File: /cases/CASE-2024-001/evidence/security_footage.jpg]',
              imageType: 'security-camera'
            },
            { 
              name: 'witness_statement.pdf', 
              icon: '📋', 
              type: 'pdf',
              size: '156 KB',
              modified: '2024-01-15 09:45',
              content: `OFFICIAL WITNESS STATEMENT

Case Number: CASE-2024-001
Date: January 15, 2024
Time: 09:45 AM

WITNESS INFORMATION:
Name: John Matthews
Age: 45
Position: Night Security Guard
Employment: SecureGuard Services Inc.
Contact: (555) 123-4567

STATEMENT:
I was conducting my regular rounds when I noticed the motion sensor light in the lobby activate at approximately 2:15 AM. This was unusual as the building should have been empty.

I immediately checked the security monitors and observed an individual dressed in dark clothing entering through the main entrance. The person appeared to know the building layout and proceeded directly to the elevator.

I called the police immediately and continued monitoring. The suspect spent approximately 10 minutes on the third floor before exiting through the same entrance. They appeared to be carrying something that wasn't visible when they entered.

I have been working security for this building for 3 years and have never witnessed anything like this before. The suspect seemed familiar with our security system and avoided several camera angles.

Signature: J. Matthews
Date: 01/15/2024
Witnessed by: Detective S. Johnson

[Case File: /cases/CASE-2024-001/witnesses/witness_statement.pdf]`
            }
          ]
        },
        'forensics': {
          name: 'Forensics',
          icon: '🔬',
          files: [
            { 
              name: 'dna_results.txt', 
              icon: '🧬', 
              type: 'text',
              size: '3.7 KB',
              modified: '2024-01-18 16:22',
              content: `DNA ANALYSIS REPORT
==================

Lab ID: LAB-2024-001-DNA
Case ID: CASE-2024-001
Date Processed: January 18, 2024
Technician: Dr. Emily Chen, PhD
Lab: Metropolitan Forensics Laboratory

SAMPLE INFORMATION:
Sample ID: EVD-001
Source: Fingerprint residue from window frame
Collection Date: January 15, 2024
Collection Officer: CSI Team Lead Marcus Wong

ANALYSIS RESULTS:
DNA Profile: Complete 13-loci STR profile obtained
Quality: High quality sample
Quantity: 2.3 ng/μL

DATABASE COMPARISON:
CODIS Search: Performed January 18, 2024
Result: Partial match found
Confidence Level: 85%
Matching Profiles: 1

MATCH DETAILS:
Subject: David Thompson
DOB: 03/15/1987
Last Known Address: 456 Oak Street, Metro City
Criminal History: Minor theft (2019), Breaking & Entering (2021)
Status: Probation

CONCLUSIONS:
The DNA profile from sample EVD-001 shows a strong statistical match to David Thompson with 85% confidence. While not sufficient for definitive identification, this warrants further investigation.

RECOMMENDATIONS:
1. Obtain fresh DNA sample from David Thompson for comparison
2. Interview subject regarding whereabouts on January 15, 2024
3. Check alibi and known associates

Report Certified by: Dr. Emily Chen
Date: January 18, 2024

[Case File: /cases/CASE-2024-001/forensics/dna_results.txt]` 
            },
            { 
              name: 'ballistics.pdf', 
              icon: '🎯', 
              type: 'pdf',
              size: '89 KB',
              modified: '2024-01-16 11:15',
              content: `BALLISTICS EXAMINATION REPORT

Case Number: CASE-2024-001
Lab Number: BAL-2024-015
Date: January 16, 2024
Examiner: Lieutenant Robert Hayes, Ballistics Expert

EXAMINATION REQUEST:
Determine if any firearms were used in the commission of the crime at 123 Main Street on January 15, 2024.

EVIDENCE EXAMINED:
- Scene photographs
- Physical evidence from crime scene
- Security footage analysis
- Victim statements

FINDINGS:
After thorough examination of all available evidence, no indication of firearm usage was found.

DETAILS:
• No bullet holes or impact marks discovered
• No shell casings recovered from scene
• No gunshot residue detected on surfaces
• Security footage shows no firearms in suspect's possession
• No witness reports of gunshots

CONCLUSION:
Based on the physical evidence and examination results, no firearms were involved in this incident. The break-in appears to have been accomplished through lock picking and physical force only.

This case does not require further ballistics investigation unless new evidence emerges.

Certified by: Lt. Robert Hayes
Ballistics Unit, Metro Police Department
Date: January 16, 2024

[Case File: /cases/CASE-2024-001/forensics/ballistics.pdf]`
            }
          ]
        }
      },
      'CASE-2024-002': {
        'case-files': {
          name: 'Arquivos do Caso',
          icon: '📁',
          files: [
            { 
              name: 'relatorio_inicial_clinica.pdf', 
              icon: '📄', 
              type: 'pdf',
              size: '3.1 KB',
              modified: '2024-02-05 08:30',
              content: `RELATÓRIO INICIAL DA PERÍCIA
========================

CASO ID: CASE-2024-002
DATA: 05 de Fevereiro de 2024
HORÁRIO: 08:30 AM
LOCAL: Clínica Médica São Lucas, Bairro Jardins
PERITO RESPONSÁVEL: Detetive Carlos Mendes
STATUS: Investigação Iniciada

RESUMO DO INCIDENTE:
Roubo ocorrido na madrugada de segunda-feira em clínica particular. Cofre arrombado, documentos confidenciais desapareceram. Porta trancada sem sinais de arrombamento.

DETALHES:
- Cofre localizado no consultório principal foi arrombado
- Documentos médicos confidenciais foram furtados
- Porta principal sem sinais de arrombamento
- Sistema de alarme desativado às 22:30 (horário suspeito)
- Apenas funcionários com acesso interno poderiam entrar na área restrita

EVIDÊNCIAS COLETADAS:
- Fio de cabelo loiro encontrado próximo ao cofre
- Imagem da câmera de segurança da entrada
- Carta manuscrita encontrada na gaveta da mesa
- Impressões digitais parciais no cofre

TESTEMUNHAS:
- Enfermeira Chefe: Joana Duarte
- Médico Assistente: Dr. Roberto Silva
- Administradora: Carmen Rodriguez

PRÓXIMOS PASSOS:
- Processar evidências forenses (DNA, análise digital)
- Entrevistar testemunhas/suspeitos
- Verificar álibi de todos os funcionários
- Analisar gravações de segurança

ATRIBUÍDO A: Unidade de Investigação 3
PRIORIDADE: Alta

[Arquivo Caso: /cases/CASE-2024-002/evidence/relatorio_inicial_clinica.pdf]` 
            },
            { 
              name: 'fio_cabelo_loiro.jpg', 
              icon: '🖼️', 
              type: 'image',
              size: '890 KB',
              modified: '2024-02-05 09:15',
              content: 'Evidência física: Fio de cabelo loiro encontrado no chão do consultório onde fica o cofre. Comprimento aproximado: 15cm. Coloração loira natural. Encaminhado para análise de DNA para identificação do proprietário.\n\n[Arquivo Caso: /cases/CASE-2024-002/evidence/fio_cabelo_loiro.jpg]',
              imageType: 'evidence-photo'
            },
            { 
              name: 'camera_seguranca.jpg', 
              icon: '🖼️', 
              type: 'image',
              size: '1.5 MB',
              modified: '2024-02-05 03:45',
              content: 'Imagem da câmera de segurança mostrando a entrada da clínica. Timestamp: 03:45:12 AM. Mulher de cabelo loiro saindo pela porta dos fundos. Altura aproximada: 1,65m. Vestindo jaleco médico branco. Parece conhecer o código da porta de emergência.\n\n[Arquivo Caso: /cases/CASE-2024-002/evidence/camera_seguranca.jpg]',
              imageType: 'security-camera'
            }
          ]
        },
        'forensics': {
          name: 'Perícia',
          icon: '🔬',
          files: [
            { 
              name: 'dna_cabelo_resultado.pdf', 
              icon: '🧬', 
              type: 'pdf',
              size: '2.8 KB',
              modified: '2024-02-07 14:20',
              content: `RESULTADO DA ANÁLISE DE DNA
==========================

ID do Laboratório: LAB-2024-002-DNA
Caso ID: CASE-2024-002
Data Processada: 07 de Fevereiro de 2024
Técnico: Dr. Patricia Santos, PhD
Laboratório: Laboratório Forense Metropolitano

INFORMAÇÕES DA AMOSTRA:
ID da Amostra: EVD-002
Origem: Fio de cabelo loiro encontrado na cena
Data da Coleta: 05 de Fevereiro de 2024
Oficial Coletor: Equipe CSI Lead Ana Silva

RESULTADOS DA ANÁLISE:
Perfil de DNA: Perfil STR de 13 loci completo obtido
Qualidade: Amostra de alta qualidade
Quantidade: 1.8 ng/μL

COMPARAÇÃO COM BANCO DE DADOS:
Busca CODIS: Realizada em 07 de Fevereiro de 2024
Resultado: Correspondência positiva encontrada
Nível de Confiança: 99.7%
Perfis Correspondentes: 1

DETALHES DA CORRESPONDÊNCIA:
Suspeita: Joana Duarte
DOB: 15/04/1995
Último Endereço Conhecido: Rua das Flores, 123 - Jardins
Histórico: Funcionária da clínica (Enfermeira Chefe)
Status: Suspeita Principal

CONCLUSÕES:
O perfil de DNA da amostra EVD-002 corresponde definitivamente a Joana Duarte com 99.7% de confiança. Esta é uma correspondência estatisticamente significativa para identificação positiva.

RECOMENDAÇÕES:
1. Interrogar Joana Duarte imediatamente
2. Verificar álibi detalhadamente
3. Buscar mandado de busca se necessário

Relatório Certificado por: Dr. Patricia Santos
Data: 07 de Fevereiro de 2024

[Arquivo Caso: /cases/CASE-2024-002/forensics/dna_cabelo_resultado.pdf]` 
            }
          ]
        }
      },
      'CASE-2024-003': {
        'case-files': {
          name: 'Arquivos de Demonstração',
          icon: '📁',
          files: [
            { 
              name: 'caso_demo.txt', 
              icon: '📄', 
              type: 'text',
              size: '1.2 KB',
              modified: '2024-03-01 08:00',
              content: `CASO DE DEMONSTRAÇÃO
===================

CASO ID: CASE-2024-003
DATA: 01 de Março de 2024
TIPO: Demonstração de Sistema

OBJETIVO:
Este é um caso de teste criado para demonstrar o carregamento dinâmico de casos no sistema CaseZero.

FUNCIONALIDADES DEMONSTRADAS:
- Carregamento automático de novos casos
- Interface de seleção de casos no dashboard
- Navegação entre diferentes casos
- Componentes independentes e agnósticos ao caso

INSTRUÇÕES:
1. Este caso aparece automaticamente no dashboard
2. Pode ser selecionado como qualquer outro caso
3. Demonstra que o sistema é modular e expansível
4. Cada componente (arquivos, email, perícia) mostra conteúdo específico do caso

STATUS: Demonstração Ativa
DURAÇÃO ESTIMADA: 10 minutos

Este caso serve apenas para validar que o sistema está funcionando corretamente.

[Arquivo Caso: /cases/CASE-2024-003/case-files/caso_demo.txt]` 
            }
          ]
        }
      }
    }
    
    return caseFiles[caseId] || {
      'no-case': {
        name: 'Nenhum Caso Selecionado',
        icon: '❌',
        files: [
          {
            name: 'selecione_caso.txt',
            icon: '📄',
            type: 'text',
            size: '0.5 KB',
            modified: new Date().toISOString().split('T')[0],
            content: 'Nenhum caso foi selecionado. Por favor, selecione um caso no dashboard para visualizar os arquivos correspondentes.'
          }
        ]
      }
    }
  }

  const fileStructure = getCaseData(currentCase || '')

  const toggleFolder = (folderId: string) => {
    const newOpenFolders = new Set(openFolders)
    if (newOpenFolders.has(folderId)) {
      newOpenFolders.delete(folderId)
    } else {
      newOpenFolders.add(folderId)
    }
    setOpenFolders(newOpenFolders)
  }

  const getFileContent = (filename: string): FileData | null => {
    for (const folder of Object.values(fileStructure)) {
      const file = folder.files.find((f: FileData) => f.name === filename)
      if (file) return file
    }
    return null
  }

  const renderFileContent = (file: FileData) => {
    if (!file) return 'File not found'

    switch (file.type) {
      case 'image':
        return (
          <ImagePreview>
            <ImagePlaceholder>
              <div style={{ fontSize: '48px' }}>🖼️</div>
              <div style={{ textAlign: 'center' }}>
                <div style={{ fontWeight: 'bold', marginBottom: '0.25rem' }}>Security Camera Footage</div>
                <div style={{ fontSize: '11px' }}>Resolution: 1920x1080</div>
                <div style={{ fontSize: '11px' }}>Format: JPEG</div>
              </div>
            </ImagePlaceholder>
            <div style={{ textAlign: 'center', fontSize: '13px', lineHeight: '1.4' }}>
              {file.content}
            </div>
          </ImagePreview>
        )
      
      case 'pdf':
        return (
          <PDFPreview>
            <div style={{ marginBottom: '1rem', paddingBottom: '0.5rem', borderBottom: '1px solid #ccc' }}>
              <strong>📋 PDF Document - {file.name}</strong>
            </div>
            <div style={{ whiteSpace: 'pre-wrap', fontSize: '13px', lineHeight: '1.6' }}>
              {file.content}
            </div>
          </PDFPreview>
        )
      
      default: // text files
        return (
          <pre style={{ 
            margin: 0, 
            whiteSpace: 'pre-wrap', 
            fontFamily: 'monospace', 
            fontSize: '13px', 
            lineHeight: '1.4',
            color: 'rgba(255, 255, 255, 0.9)'
          }}>
            {file.content}
          </pre>
        )
    }
  }

  return (
    <FileViewerContainer>
      <h3 style={{ margin: '0 0 1rem 0' }}>Police File System - {currentCase || 'No Case'}</h3>
      
      <TwoColumnLayout>
        <LeftPanel>
          <h4 style={{ margin: '0 0 1rem 0', color: '#4a9eff' }}>Files & Folders</h4>
          <FileExplorer>
            {Object.entries(fileStructure).map(([folderId, folder]) => (
              <div key={folderId}>
                <FolderHeader onClick={() => toggleFolder(folderId)}>
                  <FolderIcon>{openFolders.has(folderId) ? '📂' : '📁'}</FolderIcon>
                  <FolderName>{folder.name}</FolderName>
                </FolderHeader>
                {openFolders.has(folderId) && (
                  <FileList>
                    {folder.files.map(file => (
                      <FileItem
                        key={file.name}
                        onClick={() => setSelectedFile(file.name)}
                        style={{ 
                          background: selectedFile === file.name ? 'rgba(74, 158, 255, 0.2)' : 'transparent' 
                        }}
                      >
                        <FileIcon>{file.icon}</FileIcon>
                        <FileName>{file.name}</FileName>
                      </FileItem>
                    ))}
                  </FileList>
                )}
              </div>
            ))}
          </FileExplorer>
        </LeftPanel>
        
        <RightPanel>
          {selectedFile ? (
            <FileContent>
              {(() => {
                const file = getFileContent(selectedFile)
                if (!file) return <div>File not found</div>
                
                return (
                  <>
                    <h4 style={{ margin: '0 0 1rem 0', color: '#4a9eff' }}>{selectedFile}</h4>
                    <FileInfo>
                      <FileInfoItem>
                        <span>📊</span>
                        {file.size}
                      </FileInfoItem>
                      <FileInfoItem>
                        <span>📅</span>
                        {file.modified}
                      </FileInfoItem>
                      <FileInfoItem>
                        <span>📄</span>
                        {file.type.toUpperCase()}
                      </FileInfoItem>
                    </FileInfo>
                    {renderFileContent(file)}
                  </>
                )
              })()}
            </FileContent>
          ) : (
            <div style={{ 
              flex: 1, 
              display: 'flex', 
              alignItems: 'center', 
              justifyContent: 'center',
              color: 'rgba(255, 255, 255, 0.6)',
              fontSize: '16px'
            }}>
              Select a file to view its contents
            </div>
          )}
        </RightPanel>
      </TwoColumnLayout>
    </FileViewerContainer>
  )
}

export default FileViewer