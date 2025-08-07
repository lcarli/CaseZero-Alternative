# CASE-2024-002: Roubo na Clínica

## 📋 Informações Gerais

**Título:** Roubo na Clínica  
**ID:** CASE-2024-002  
**Tipo:** Caso Tutorial  
**Dificuldade:** Iniciante (3/10)  
**Tempo Estimado:** 1-2 horas  
**Rank Mínimo:** Cadet  

## 🎯 Objetivo do Caso

Este é um caso tutorial projetado para ensinar os fundamentos da investigação criminal. O jogador deve:

- Examinar evidências físicas e digitais
- Analisar interrogatórios de suspeitos
- Solicitar análises forenses
- Distinguir evidências relevantes de distrações
- Usar DNA para identificar o culpado

## 📖 Sinopse

Na madrugada de segunda-feira, ocorreu um roubo na Clínica Médica São Lucas. Um cofre foi arrombado e documentos confidenciais desapareceram. A porta da frente estava trancada sem sinais de arrombamento, indicando que o crime foi cometido por alguém com acesso interno.

Três pessoas estavam presentes no prédio nas horas anteriores ao crime, todas alegam ter saído antes do ocorrido. O investigador deve descobrir quem cometeu o roubo e qual evidência prova a culpa.

## 👥 Suspeitos

### 1. Joana Duarte (29 anos) - **CULPADA**
- **Profissão:** Enfermeira Chefe
- **Cabelo:** Loiro natural
- **Motivo:** Problemas financeiros graves + chantagem
- **Acesso:** Total (incluindo cofre)
- **Álibi:** Não verificável

### 2. Roberto Silva (34 anos) - Inocente
- **Profissão:** Médico Cardiologista
- **Cabelo:** Loiro descolorido (natural castanho)
- **Motivo:** Pressão para participar de esquemas
- **Acesso:** Limitado (sem acesso ao cofre)
- **Álibi:** Verificável (plantão hospital)

### 3. Carmen Rodriguez (45 anos) - Inocente
- **Profissão:** Administradora
- **Cabelo:** Castanho escuro
- **Motivo:** Ressentimento salarial
- **Acesso:** Sem acesso ao cofre
- **Álibi:** Verificável (família)

## 🔍 Evidências Principais

### Evidências Relevantes:
1. **Fio de Cabelo Loiro** (EVD002) - **CRÍTICO**
   - Encontrado na sala do cofre
   - DNA confirma: pertence a Joana Duarte

2. **Câmera de Segurança** (EVD003) - **CRÍTICO**
   - Mostra mulher loira saindo às 03:47h
   - Sistema desativado por usuário "ADMIN_JOANA" às 22:30h

3. **Carta Manuscrita** (EVD004) - **IMPORTANTE**
   - Conteúdo de chantagem
   - Análise grafotécnica confirma: escrita por Joana

### Evidências Distrativas:
- Bilhete romântico antigo
- Recibo de farmácia
- Chave que não abre nada
- Agenda pessoal com rabiscos
- Ficha de paciente irrelevante
- Caderno com anotações médicas

## 🧪 Análises Forenses Essenciais

1. **DNA do Cabelo** (90 minutos)
   - **Resultado:** Match com Joana Duarte (>99,99%)
   - **Importância:** Prova definitiva da presença no local

2. **Análise Digital da Câmera** (60 minutos)
   - **Resultado:** Confirma desativação por Joana + horários
   - **Importância:** Contradiz álibi e mostra planejamento

3. **Grafotécnica da Carta** (75 minutos)
   - **Resultado:** Escrita por Joana Duarte
   - **Importância:** Confirma motivo (chantagem)

## 🗂️ Progressão do Caso

### Fase 1: Evidências Iniciais (Imediatas)
- Relatório inicial da perícia
- Fio de cabelo loiro
- Imagem da câmera de segurança
- Interrogatórios de Joana e Roberto

### Fase 2: Evidências Intermediárias (15+ min)
- Carta manuscrita (desbloqueada após examinar relatório)
- Carmen Rodriguez (desbloqueada após examinar relatório)

### Fase 3: Evidências Distrativas (5-15 min)
- Bilhete romântico, recibo, chave, agenda, etc.
- Aparecem gradualmente para confundir o investigador

### Fase 4: Análises Forenses (60-90 min)
- Resultados das análises confirmam Joana como culpada

## 🎮 Mecânicas Tutorial

### Eventos Temporais:
- **30 min:** Memo explicativo sobre caso tutorial
- **45 min:** Depoimento do segurança (confirma mulher loira)
- **60 min:** Atualização do laboratório sobre DNA

### Desbloqueios:
- Evidências aparecem progressivamente
- Análises levam tempo realista para processar
- Cada descoberta leva naturalmente à próxima

## ✅ Solução

**Culpado:** Joana Duarte  
**Evidência Principal:** DNA do fio de cabelo (EVD002)  
**Evidências de Apoio:** Câmera + Grafotécnica  

### Explicação:
Joana Duarte cometeu o roubo devido a grave situação financeira. Ela tinha acesso total à clínica, conhecia todos os protocolos de segurança, e estava sendo chantageada por alterar receitas médicas. O DNA do cabelo encontrado no local, a desativação da câmera usando suas credenciais, e a carta manuscrita de sua própria autoria provam sua culpa.

## 🛠️ Arquivos Criados

### Suspects/
- `joana_duarte.txt` - Interrogatório completo da culpada
- `roberto_silva.txt` - Interrogatório do médico inocente
- `carmen_rodriguez.txt` - Interrogatório da administradora

### Evidence/
- `relatorio_inicial_clinica.pdf` - Relatório da perícia
- `fio_cabelo_loiro.jpg` - Foto da evidência principal
- `camera_seguranca.jpg` - Imagem da câmera
- `carta_manuscrita.pdf` - Carta de chantagem
- 6 arquivos de evidências distrativas

### Forensics/
- `dna_cabelo_resultado.pdf` - Análise DNA definitiva
- `camera_analysis.pdf` - Análise digital detalhada
- `analise_caligrafia.pdf` - Grafotécnica da carta

### Memos/ & Witnesses/
- Arquivos de eventos temporais para progressão narrativa

## 📚 Objetivos de Aprendizado

Ao completar este caso, o jogador aprende:
1. **Priorização de evidências** - O que é relevante vs. distração
2. **Análise forense** - Como e quando solicitar análises
3. **Construção de caso** - Montar evidências em narrativa coerente
4. **Investigação científica** - Usar DNA como prova definitiva
5. **Eliminação de suspeitos** - Como descartar inocentes sistematicamente

---

**Status:** ✅ Completo e validado  
**Testado:** API funcionando corretamente  
**Pronto para:** Integração no jogo