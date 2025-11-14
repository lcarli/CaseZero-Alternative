# Capítulo 13: Glossário

## Visão Geral

Este glossário fornece definições abrangentes para todos os termos técnicos, terminologia específica do jogo, siglas e vocabulário especializado utilizados em todo o Documento de Design de Jogo do CaseZero v3.0. Os termos estão organizados alfabeticamente, com referências cruzadas para os capítulos relevantes onde são discutidos em detalhes.

---

## A

### Achievement

Marco ou conquista que concede reconhecimento ao jogador e, potencialmente, XP bônus. Exemplos: "Primeiro Caso Resolvido", "Detetive Perfeito" (caso resolvido sem dicas) ou "Perito em Perícias" (utilizou todos os tipos de perícia). Consulte **[Capítulo 06 - Progressão do Jogador]** para detalhes do sistema de conquistas.

### API (Application Programming Interface)

Conjunto de endpoints HTTP que permite ao frontend se comunicar com o backend. A REST API do CaseZero trata autenticação, leitura de dados de casos, requisições forenses e envio de soluções. Consulte **[Capítulo 08 - Arquitetura Técnica]** para as especificações dos endpoints.

### ASP.NET Core

Framework web open source e multiplataforma da Microsoft usado para construir a API backend do CaseZero. A versão 9.0 é a especificada para o projeto. Consulte **[Capítulo 08 - Arquitetura Técnica]** para a justificativa da stack.

### Autenticação

Processo de verificar a identidade de um usuário por meio de usuário/e-mail e senha. O CaseZero utiliza JWT (JSON Web Tokens) para autenticação stateless. Consulte **[Capítulo 08 - Arquitetura Técnica]** para o fluxo de autenticação.

### Autorização

Processo que determina quais ações um usuário autenticado pode executar. O CaseZero usa autorização baseada em papéis (atualmente apenas "Player", com futuros "Admin" e "ContentCreator" planejados).

### Azure App Service

Plataforma como serviço (PaaS) da Microsoft para hospedar aplicações web. A API backend do CaseZero roda em Azure App Service Linux. Consulte **[Capítulo 08 - Arquitetura Técnica]** para a estratégia de deploy.

### Azure Blob Storage

Serviço de armazenamento de objetos da Microsoft para dados não estruturados. O CaseZero armazena PDFs, fotos de evidência e laudos forenses em blob storage. Utiliza tier Hot para casos ativos e Cool para arquivo. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

### Azure CDN (Content Delivery Network)

Rede distribuída de servidores que entrega conteúdo estático a partir de locais geográficos próximos ao usuário. O CaseZero usa Azure CDN para servir assets de casos (PDFs, imagens) com baixa latência global. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

### Azure Functions

Serviço serverless da Microsoft para executar código orientado a eventos. O CaseZero usa Azure Functions com Timer Triggers para processar conclusões de perícias a cada 5 minutos. Consulte **[Capítulo 08 - Arquitetura Técnica]** para a implementação de perícias em tempo real.

---

## B

### Balística

Tipo de perícia que examina armas de fogo, balas e resíduos de tiro para determinar tipo de arma, trajetória e posição do atirador. Disponível como requisição no Forensics Lab. Consulte **[Capítulo 03 - Mecânicas Centrais]** para o sistema de perícias.

### Bicep

Linguagem declarativa para deploy de recursos Azure via Infraestrutura como Código (IaC). Os templates de infraestrutura do CaseZero são escritos em Bicep. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

### Blue-Green Deployment

Estratégia de deploy que mantém dois ambientes de produção idênticos ("blue" ativo, "green" em espera). Novas versões são publicadas no ambiente green, testadas e, então, recebem o tráfego. Permite zero downtime e rollback rápido. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

---

## C

### Caso

Cenário completo de mistério de assassinato que o jogador deve resolver. Inclui informações da vítima, detalhes do crime, suspeitos, evidências, documentos, perícias, linha do tempo e solução. Identificado por um ID único (ex.: CASE-2024-001). Consulte **[Capítulo 04 - Estrutura de Caso]** para a anatomia completa.

### Case Files

Aplicativo/interface in-game onde os jogadores visualizam documentos e evidências. Faz analogia a uma pasta de arquivos de detetive. Inclui Visualizador de Documentos (PDF.js), Galeria de Evidências e Lista de Documentos. Consulte **[Capítulo 07 - Interface do Usuário]**.

### case.json

Arquivo mestre que define um caso completo. Estrutura JSON contendo metadata, vítima, crime, local, suspeitos, evidências, documentos, perícias, linha do tempo e solução. Armazenado em coluna JSONB do PostgreSQL. Consulte **[Capítulo 09 - Esquema de Dados & Modelos]** para o schema completo.

### Sessão de Caso

Registro persistente do progresso de um jogador em um caso específico, incluindo session ID, case ID, user ID, horário de início, status de conclusão, tempo gasto e solução enviada. Permite retomar casos em múltiplos dispositivos. Consulte **[Capítulo 09 - Esquema de Dados & Modelos]**.

### CDN

Consulte **Azure CDN**.

### CI/CD (Continuous Integration / Continuous Deployment)

Práticas automatizadas de desenvolvimento. CI executa testes automaticamente a cada push. CD faz deploy automático das alterações aprovadas para staging/produção. O CaseZero usa GitHub Actions para CI/CD. Consulte **[Capítulo 11 - Estratégia de Testes]**.

### Pista (Clue)

Informação inserida em documentos ou evidências que ajuda a identificar culpado, motivo ou método. Pode ser explícita (declaração direta) ou implícita (requer inferência). Consulte **[Capítulo 04 - Estrutura de Caso]** e **[Capítulo 10 - Pipeline de Conteúdo]** para a matriz de pistas.

### CORS (Cross-Origin Resource Sharing)

Mecanismo de segurança que permite ou restringe requisições web entre domínios diferentes. A API do CaseZero configura CORS para aceitar apenas domínios do frontend. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

### Cena do Crime

Local físico onde o assassinato ocorreu. Descrito com endereço, tipo (residencial, público, comercial, externo), condições ambientais e descrição física. Consulte **[Capítulo 04 - Estrutura de Caso]**.

### CSP (Content Security Policy)

Cabeçalhos HTTP que controlam quais recursos (scripts, estilos, imagens) o navegador pode carregar. Evita ataques XSS. O CaseZero aplica CSP rigoroso. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

### Culpado (Culprit)

Pessoa que cometeu o assassinato. No CaseZero, o culpado é sempre um dos suspeitos nomeados. O objetivo principal do jogador é identificar o culpado correto. Consulte **[Capítulo 04 - Estrutura de Caso]** para a estrutura da solução.

---

## D

### DTO (Data Transfer Object)

Objeto simples usado para transferir dados entre camadas, tipicamente entre API e frontend. O CaseZero usa records C# para DTOs (ex.: `CaseListDto`, `SubmitSolutionDto`). Consulte **[Capítulo 09 - Esquema de Dados & Modelos]**.

### DAU/MAU (Daily Active Users / Monthly Active Users)

Métricas-chave de engajamento. DAU mede usuários únicos por dia; MAU, por mês. A meta do CaseZero é 25-30% (indicador de retenção saudável). Consulte **[Capítulo 12 - Roadmap do Produto]**.

### Patente de Detetive (Detective Rank)

Tier de progressão baseado no XP total. Inclui Cadete (0 XP), Detetive Júnior (5.000 XP), Detetive (20.000 XP), Detetive Sênior (50.000 XP), Detetive Líder (100.000 XP), Inspetor-Chefe (200.000 XP), Detetive Lendário (500.000 XP). Consulte **[Capítulo 06 - Progressão do Jogador]**.

### Dificuldade

Classificação que indica a complexidade do caso. Quatro níveis: Easy (menos suspeitos, pistas claras), Medium (complexidade moderada), Hard (várias falsas pistas, linha do tempo intrincada) e Expert (altamente complexo, pistas sutis). Consulte **[Capítulo 04 - Estrutura de Caso]**.

### Análise de DNA (DNA Analysis)

Tipo de perícia que examina evidências biológicas (sangue, cabelo, saliva) para identificar indivíduos via correspondência genética. Maior tempo de processamento (24 horas base). Consulte **[Capítulo 03 - Mecânicas Centrais]**.

---

## E

### Testes E2E (End-to-End Testing)

Metodologia que valida fluxos completos do usuário em ambiente semelhante à produção. O CaseZero usa Playwright para testes E2E. Consulte **[Capítulo 11 - Estratégia de Testes]**.

### EF Core (Entity Framework Core)

ORM da Microsoft para .NET. Versão 9.0 usada no CaseZero para interagir com PostgreSQL. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

### Evidência

Itens físicos ou digitais ligados ao crime que o jogador pode examinar. Tipos: Evidência Física (arma, roupas), Documentos (subset legível), Fotos, Amostras Forenses (analisadas no laboratório) e Evidência Digital (expansão futura). Consulte **[Capítulo 04 - Estrutura de Caso]**.

### Galeria de Evidências (Evidence Gallery)

Componente de UI no Case Files que exibe evidências em grade, com miniaturas, nomes e descrições. O jogador clica para ampliar. Consulte **[Capítulo 07 - Interface do Usuário]**.

### XP (Experience Points)

Pontos concedidos ao resolver casos, usados para calcular Patente. XP base varia conforme dificuldade: Easy 150, Medium 300, Hard 600, Expert 1200. Bônus por primeira tentativa (+50%), eficiência de tempo (+10-20%) e sem dicas (+20%). Consulte **[Capítulo 06 - Progressão do Jogador]**.

---

## F

### Análise de Impressões Digitais (Fingerprint Analysis)

Perícia que compara digitais encontradas na cena com as dos suspeitos para confirmar presença ou descartá-los. Tempo médio (6 horas base). Consulte **[Capítulo 03 - Mecânicas Centrais]**.

### FluentValidation

Biblioteca .NET para construir regras de validação fortemente tipadas. O CaseZero utiliza FluentValidation para validar dados do case.json, DTOs de requisição API e submissões de usuários. Consulte **[Capítulo 09 - Esquema de Dados & Modelos]**.

### Análise Forense

Exame científico de evidências feito por especialistas. No CaseZero, o jogador solicita perícias via Forensics Lab, aguarda o processamento em tempo real e revisa o PDF gerado. Tipos: DNA, Impressões Digitais, Toxicologia, Balística e Forense Digital (futuro). Consulte **[Capítulo 03 - Mecânicas Centrais]**.

### Requisição Forense (Forensic Request)

Ação iniciada pelo jogador para analisar uma evidência. Gera registro com ID da requisição, case ID, user ID, evidence ID, tipo de análise, horários de solicitação e conclusão, status (Pending/Completed/Failed) e URL do laudo. Consulte **[Capítulo 09 - Esquema de Dados & Modelos]**.

### Forensics Lab

Interface in-game onde o jogador solicita e acompanha perícias. Mostra evidências disponíveis, permite escolher tipo de análise, exibe requisições pendentes com contagem regressiva e disponibiliza laudos concluídos. Consulte **[Capítulo 07 - Interface do Usuário]**.

---

## G

### GitHub Actions

Plataforma de automação CI/CD do GitHub. O CaseZero utiliza workflows para executar testes, gerar artefatos e fazer deploy no Azure a cada push. Consulte **[Capítulo 11 - Estratégia de Testes]**.

---

## H

### Tier Hot

Camada de acesso do Azure Blob Storage otimizada para dados frequentemente acessados. O CaseZero mantém assets de casos ativos na tier Hot. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

### HTTP (Hypertext Transfer Protocol)

Protocolo de aplicação usado na comunicação entre frontend e backend. A API do CaseZero utiliza exclusivamente HTTPS (HTTP criptografado). Consulte **[Capítulo 08 - Arquitetura Técnica]**.

---

## I

### IaC (Infrastructure as Code)

Prática de gerenciar infraestrutura de nuvem via arquivos de código (Bicep, Terraform) em vez de configuração manual. Permite versionamento, deploy automatizado e reprodutibilidade. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

### Testes de Integração (Integration Testing)

Metodologia que valida interações entre múltiplos componentes (ex.: API + banco). O CaseZero usa xUnit com WebApplicationFactory. Consulte **[Capítulo 11 - Estratégia de Testes]**.

---

## J

### JSON (JavaScript Object Notation)

Formato leve de troca de dados. O CaseZero usa JSON para case.json, payloads da API e estado do Redux. Consulte **[Capítulo 09 - Esquema de Dados & Modelos]**.

### JSONB

Tipo de dado binário JSON do PostgreSQL que permite armazenamento e consultas eficientes. O CaseZero guarda case.json em coluna JSONB para schema flexível e consultas rápidas. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

### JSON Schema

Vocabulário para validar a estrutura de documentos JSON. O CaseZero define um JSON Schema para garantir a validade de case.json. Consulte **[Capítulo 09 - Esquema de Dados & Modelos]**.

### JWT (JSON Web Token)

Token compacto e seguro para representar claims entre partes. O CaseZero usa JWT para autenticação stateless. O token inclui user ID, username, email, papel e expiração. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

---

## K

### Evidência-Chave (Key Evidence)

Evidências e documentos que provam diretamente a solução (culpado, motivo, método). O jogador deve selecionar as evidências-chave ao enviar a solução. Consulte **[Capítulo 04 - Estrutura de Caso]**.

### Busca por Palavra-Chave (Keyword Search)

Feature futura que permitirá pesquisar texto em documentos. Fora do escopo do MVP. Consulte **[Capítulo 12 - Roadmap do Produto]** para backlog.

---

## L

### Testes de Carga (Load Testing)

Metodologia que avalia performance sob cargas esperada e de pico. O CaseZero usa Artillery para simular usuários concorrentes. Consulte **[Capítulo 11 - Estratégia de Testes]**.

### Localização (L10n)

Processo de adaptação para diferentes idiomas e regiões. O MVP do CaseZero é apenas em inglês; Francês, Espanhol, Português e Alemão estão planejados para expansão no Ano 1. Consulte **[Capítulo 10 - Pipeline de Conteúdo]** para a estratégia.

---

## M

### Método (Method)

Forma como o assassinato foi cometido (ex.: esfaqueamento, tiro, envenenamento, estrangulamento). Integra os detalhes do crime e a solução do jogador. Consulte **[Capítulo 04 - Estrutura de Caso]**.

### Motivo (Motive)

Razão pela qual o culpado cometeu o crime (ex.: ganho financeiro, vingança, ciúmes, autoproteção). O jogador precisa deduzir e descrever o motivo na submissão. Consulte **[Capítulo 04 - Estrutura de Caso]**.

### MVP (Minimum Viable Product)

Versão inicial com as features essenciais para lançar. O MVP do CaseZero inclui autenticação, 9 casos (3 Easy, 3 Medium, 2 Hard, 1 Expert), visualização de caso, perícias, submissão e progressão básica. Consulte **[Capítulo 12 - Roadmap do Produto]**.

---

## N

### NPS (Net Promoter Score)

Métrica de satisfação medida pelo questionamento "Quão provável é recomendar este produto?" (0-10). A meta do CaseZero é >40 ao fim do Ano 1. Consulte **[Capítulo 12 - Roadmap do Produto]**.

### Sistema de Notas (Notes System)

Ferramenta in-game onde o jogador escreve notas, observações e teorias. Salva automaticamente a cada 30 segundos. Suporta formatação rich text (negrito, itálico, listas). Consulte **[Capítulo 03 - Mecânicas Centrais]** e **[Capítulo 07 - Interface do Usuário]**.

---

## O

### ORM (Object-Relational Mapping)

Técnica que converte dados entre sistemas de tipos diferentes (ex.: objetos C# ↔ tabelas de banco). O CaseZero utiliza Entity Framework Core como ORM. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

---

## P

### PDF.js

Biblioteca JavaScript open source da Mozilla para renderizar PDFs no navegador sem plugins. O CaseZero usa PDF.js para exibir documentos. Consulte **[Capítulo 07 - Interface do Usuário]** e **[Capítulo 08 - Arquitetura Técnica]**.

### Playwright

Framework de automação de navegadores para testes end-to-end. Suporta Chrome, Firefox e Safari. O CaseZero usa Playwright para fluxos E2E. Consulte **[Capítulo 11 - Estratégia de Testes]**.

### PostgreSQL

Banco de dados relacional open source. Versão 15+ hospedada no Azure Database for PostgreSQL. Escolhido pelo suporte ao JSONB e garantias ACID. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

### PWA (Progressive Web App)

Aplicação web com experiência semelhante a app (modo offline, push, instalável). O CaseZero implementa Service Worker para visualização offline de casos. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

---

## Q

### QA (Quality Assurance)

Processo de garantir padrões de qualidade por meio de testes e revisões. No CaseZero inclui testes automatizados, playthrough de conteúdo e testes cegos por QA. Consulte **[Capítulo 11 - Estratégia de Testes]** e **[Capítulo 10 - Pipeline de Conteúdo]**.

### Gate de Qualidade (Quality Gate)

Checkpoint no fluxo de desenvolvimento/deploy que exige critérios específicos antes de prosseguir. O CaseZero possui gates para pré-merge (testes e cobertura ≥80%), pré-deploy (metas de performance) e publicação de caso (playthrough QA). Consulte **[Capítulo 11 - Estratégia de Testes]**.

---

## R

### React

Biblioteca JavaScript do Meta para construção de UIs. Versão 18+ usada no frontend do CaseZero. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

### React Router

Biblioteca de roteamento para React. Versão 6 usada na navegação (Dashboard, Case Files, Forensics Lab, Submit Solution, Perfil). Consulte **[Capítulo 08 - Arquitetura Técnica]**.

### Redux Toolkit

Toolset oficial para desenvolvimento eficiente com Redux. O CaseZero usa Redux Toolkit para gerenciar estado (slices de auth, cases, documents, evidence, forensics, notes, ui). Consulte **[Capítulo 08 - Arquitetura Técnica]**.

### REST API (Representational State Transfer)

Estilo arquitetural para serviços web usando métodos HTTP (GET, POST, PUT, DELETE) e URLs por recurso. O backend do CaseZero expõe uma API RESTful. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

### RTO (Recovery Time Objective)

Tempo máximo aceitável que o sistema pode ficar indisponível após desastre. O RTO do CaseZero é 1 hora. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

### RPO (Recovery Point Objective)

Quantidade máxima de dados, em tempo, que pode ser perdida. O RPO do CaseZero é 24 horas (backups diários). Consulte **[Capítulo 08 - Arquitetura Técnica]**.

---

## S

### Service Worker

Script JavaScript que roda em background, habilitando modo offline e push. O CaseZero usa Service Worker para cache de dados de casos offline. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

### Sessão

Consulte **Sessão de Caso**.

### Solução (Solution)

Resposta correta de um caso, incluindo culpado, motivo, método, linha do tempo e evidências-chave. Armazenada em case.json, mas oculta ao jogador até a submissão. Consulte **[Capítulo 04 - Estrutura de Caso]**.

### Submissão (Submission)

Resposta do jogador enviada pelo formulário Submit Solution em cinco etapas. Inclui seleção do culpado, explicação do motivo (100-500 palavras), método (50-300 palavras), evidências-chave (3-8 itens) e revisão. Consulte **[Capítulo 03 - Mecânicas Centrais]**.

### Suspeito (Suspect)

Pessoa de interesse na investigação. Cada caso possui 2-8 suspeitos com nome, idade, ocupação, relação com a vítima, álibi, potencial de motivo, histórico e descrição física. Um deles é sempre o culpado. Consulte **[Capítulo 04 - Estrutura de Caso]**.

---

## T

### Tailwind CSS

Framework CSS utilitário para construir UIs rapidamente. O CaseZero utiliza Tailwind com configuração de tema customizada. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

### Linha do Tempo (Timeline)

Sequência cronológica de eventos antes, durante e após o crime. Cada evento tem timestamp (data/hora ou descrição relativa), descrição e participantes. Ajuda a compreender o crime. Consulte **[Capítulo 04 - Estrutura de Caso]**.

### Timeline View

Componente de UI que exibe eventos em ordem cronológica, destacando o horário da morte. Ajuda o jogador a entender a sequência. Consulte **[Capítulo 07 - Interface do Usuário]**.

### Toxicologia

Perícia que analisa fluidos corporais ou tecidos em busca de drogas/venenos. Tempo médio-longo (12 horas base). Consulte **[Capítulo 03 - Mecânicas Centrais]**.

### TypeScript

Superset tipado de JavaScript que compila para JS puro. O frontend do CaseZero é escrito em TypeScript para segurança de tipos. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

---

## U

### UGC (User-Generated Content)

Conteúdo criado por jogadores. O roadmap (Ano 2+) inclui ferramentas de edição de casos para criação e compartilhamento. Consulte **[Capítulo 12 - Roadmap do Produto]**.

### Testes Unitários (Unit Testing)

Metodologia que valida funções/componentes em isolamento. O CaseZero usa Vitest (frontend) e xUnit (backend). Meta de cobertura ≥80%. Consulte **[Capítulo 11 - Estratégia de Testes]**.

### Progresso do Usuário (User Progress)

Registro persistente das conquistas gerais do jogador, incluindo XP total, patente atual, casos resolvidos, tempo jogado, dicas usadas, conquistas obtidas e estatísticas. Consulte **[Capítulo 09 - Esquema de Dados & Modelos]**.

### UX (User Experience)

Experiência geral ao interagir com o produto. O CaseZero prioriza UX imersiva com documentos autênticos, perícias realistas e investigação sem pressão. Consulte **[Capítulo 01 - Conceito de Jogo]**.

---

## V

### Validação (Validation)

Processo de garantir que os dados atendam formato e regras de negócio. O CaseZero valida a estrutura do case.json, entradas do usuário e requisições de API. Consulte **[Capítulo 09 - Esquema de Dados & Modelos]**.

### Vítima (Victim)

Pessoa assassinada. O caso inclui nome, idade, ocupação, histórico pessoal, descrição física, relacionamentos e local/condição em que foi encontrada. Consulte **[Capítulo 04 - Estrutura de Caso]**.

### Vite

Ferramenta moderna de build frontend com dev server rápido e builds otimizados. O CaseZero usa Vite como sistema de build. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

### Vitest

Framework de testes unitários nativo do Vite. O CaseZero usa Vitest para testes de frontend. Consulte **[Capítulo 11 - Estratégia de Testes]**.

---

## W

### WCAG (Web Content Accessibility Guidelines)

Padrões internacionais de acessibilidade. O CaseZero mira conformidade WCAG 2.1 AA (contraste 4,5:1, navegação por teclado, suporte a leitores de tela, landmarks ARIA). Consulte **[Capítulo 11 - Estratégia de Testes]**.

### Testemunha (Witness)

Pessoa com informações relevantes, mas que não é suspeita. Pode ter visto eventos, ouvido conversas ou conhecer vítima/suspeitos. Geralmente documentada em depoimentos ou entrevistas. Consulte **[Capítulo 04 - Estrutura de Caso]**.

---

## X

### XP

Consulte **[XP (Experience Points)](#xp-experience-points)**.

### XSS (Cross-Site Scripting)

Vulnerabilidade em que scripts maliciosos são injetados em páginas vistas por outros usuários. O CaseZero previne XSS com cabeçalhos CSP, sanitização e escaping automático do React. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

---

## Y

### YAML (YAML Ain't Markup Language)

Linguagem de serialização legível por humanos. O CaseZero usa YAML em workflows do GitHub Actions e arquivos de configuração. Consulte **[Capítulo 11 - Estratégia de Testes]**.

---

## Z

### Deploy com Zero Downtime (Zero-Downtime Deployment)

Estratégia de deploy que atualiza produção sem indisponibilidade. O CaseZero alcança isso via blue-green deployment. Consulte **[Capítulo 08 - Arquitetura Técnica]**.

---

## Índice de Referência Cruzada

### Termos por Capítulo

**Capítulo 01 - Conceito de Jogo:**
UX, Design Imersivo, Realismo, Investigação

**Capítulo 03 - Mecânicas Centrais:**
Análise Forense, Análise de DNA, Análise de Impressões Digitais, Toxicologia, Balística, Sistema de Notas, Submissão

**Capítulo 04 - Estrutura de Caso:**
Caso, case.json, Vítima, Cena do Crime, Suspeito, Evidência, Testemunha, Documento, Linha do Tempo, Solução, Culpado, Motivo, Método, Evidência-Chave, Pista, Dificuldade

**Capítulo 06 - Progressão do Jogador:**
XP, Patente de Detetive, Achievement, Progresso do Usuário

**Capítulo 07 - Interface do Usuário:**
Case Files, Visualizador de Documentos, Galeria de Evidências, Timeline View, Forensics Lab

**Capítulo 08 - Arquitetura Técnica:**
API, REST API, ASP.NET Core, React, TypeScript, Redux Toolkit, PostgreSQL, JSONB, Azure App Service, Azure Functions, Azure Blob Storage, Azure CDN, JWT, Autenticação, Autorização, Service Worker, PWA, PDF.js, Vite, EF Core, Blue-Green Deployment, CI/CD, IaC, Bicep, CORS, CSP, XSS, Tier Hot, RTO, RPO, Deploy com Zero Downtime

**Capítulo 09 - Esquema de Dados & Modelos:**
DTO, Sessão de Caso, Requisição Forense, Progresso do Usuário, Validação, FluentValidation, JSON Schema, ORM

**Capítulo 10 - Pipeline de Conteúdo:**
QA, Localização, Pista, Criação de Conteúdo

**Capítulo 11 - Estratégia de Testes:**
Testes Unitários, Testes de Integração, Testes E2E, Testes de Carga, Playwright, Vitest, Gate de Qualidade, WCAG, GitHub Actions, CI/CD

**Capítulo 12 - Roadmap do Produto:**
MVP, UGC, DAU/MAU, NPS

### Termos por Categoria Tecnológica

**Tecnologias de Frontend:**
React, TypeScript, Redux Toolkit, Vite, Tailwind CSS, React Router, PDF.js, Service Worker, PWA, Vitest

**Tecnologias de Backend:**
ASP.NET Core, C#, EF Core, FluentValidation, JWT, REST API

**Tecnologias de Banco:**
PostgreSQL, JSONB, SQL, ORM

**Tecnologias de Nuvem:**
Azure App Service, Azure Functions, Azure Blob Storage, Azure CDN, Azure Database for PostgreSQL, IaC, Bicep

**Tecnologias de Teste:**
Vitest, xUnit, Moq, Playwright, WebApplicationFactory, Artillery, axe-playwright

**Tecnologias de DevOps:**
GitHub Actions, CI/CD, Docker, Blue-Green Deployment, YAML

**Tecnologias de Segurança:**
JWT, CORS, CSP, Prevenção XSS, HTTPS

### Termos por Conceito de Jogo

**Elementos do Caso:**
Caso, case.json, Vítima, Suspeito, Testemunha, Cena do Crime, Evidência, Documento, Linha do Tempo, Solução

**Ações de Investigação:**
Sistema de Notas, Visualizador de Documentos, Galeria de Evidências, Requisição Forense, Submissão

**Perícias:**
Análise Forense, Análise de DNA, Impressões Digitais, Toxicologia, Balística, Forensics Lab

**Progressão:**
XP, Patente de Detetive, Achievement, Progresso do Usuário

**Atributos de Qualidade:**
Dificuldade, Pista, Evidência-Chave, Culpado, Motivo, Método

---

## Referência Rápida de Siglas

- **API** - Application Programming Interface
- **CDN** - Content Delivery Network
- **CI/CD** - Continuous Integration / Continuous Deployment
- **CORS** - Cross-Origin Resource Sharing
- **CSP** - Content Security Policy
- **DAU** - Daily Active Users
- **DNA** - Deoxyribonucleic Acid
- **DTO** - Data Transfer Object
- **E2E** - End-to-End
- **EF Core** - Entity Framework Core
- **HTTP** - Hypertext Transfer Protocol
- **HTTPS** - HTTP Secure
- **IaC** - Infrastructure as Code
- **JSON** - JavaScript Object Notation
- **JSONB** - JSON Binary (tipo do PostgreSQL)
- **JWT** - JSON Web Token
- **L10n** - Localization
- **MAU** - Monthly Active Users
- **MVP** - Minimum Viable Product
- **NPS** - Net Promoter Score
- **ORM** - Object-Relational Mapping
- **PDF** - Portable Document Format
- **PWA** - Progressive Web App
- **QA** - Quality Assurance
- **REST** - Representational State Transfer
- **RPO** - Recovery Point Objective
- **RTO** - Recovery Time Objective
- **SQL** - Structured Query Language
- **UGC** - User-Generated Content
- **UI** - User Interface
- **UX** - User Experience
- **WCAG** - Web Content Accessibility Guidelines
- **XP** - Experience Points
- **XSS** - Cross-Site Scripting
- **YAML** - YAML Ain't Markup Language

---

## Guia de Pronúncia

- **Bicep** - "bái-sep" (não "bái-ceps")
- **case.json** - "keiz dot djêi-son" (pronunciar a extensão)
- **JSONB** - "djêi-son bí" (JSON Binary)
- **PostgreSQL** - "poust-grés kiu el" ou informal "poust-grés"
- **Vite** - "vít" (francês para "rápido")
- **Vitest** - "ví-tést"
- **WCAG** - "uê-cég" ou soletrar "dáblio-ci-ei-djí"

---

## Histórico de Revisões

| Versão | Data | Mudanças | Autor |
|--------|------|----------|-------|
| 1.0 | 14/11/2025 | Tradução completa para PT-BR | Assistente de IA |

---

**Status do Documento:** Completo  
**Última Atualização:** 14 de novembro de 2025  
**Próxima Revisão:** Após grandes adições de features ou mudanças na terminologia
