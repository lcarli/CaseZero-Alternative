# Plano de implementacao: geracao de casos consistente

**Status:** entregas 01-06 reconciliadas com a arquitetura canonica de main;
07-10 implementadas parcialmente na feature e ainda em validacao. O usuario
aprovou concluir a arquitetura antes de publicar somente as Functions DEV e
executar um piloto real **Rookie, pt-BR** separado. Nao houve deploy, caso real
aprovado ou aplicacao de migration a um banco real.

**Retomada em outra maquina:** ler
[CASE_GENERATION_HANDOFF.md](CASE_GENERATION_HANDOFF.md), que registra o estado,
os resultados atuais, bloqueadores e roteiro de publicacao em outra maquina.
Nao usar os resultados historicos da feature como prova da reconciliacao.

**Base:** auditoria da `main` em `9cb2218`, incluindo o pipeline v2, os
consumidores no backend/frontend, documentos versionados e reproducoes locais
com respostas controladas, sem chamadas pagas a IA.

## 1. Resultado esperado

Produzir casos solucionaveis a partir dos materiais realmente entregues ao
jogador, com fatos consistentes, procedimentos plausiveis e documentos completos
que tenham identidade propria.

A ordem e: **impedir publicacao indevida -> alinhar gerador e jogo -> estabelecer
verdade canonica -> integrar skills -> ampliar materiais -> avaliar e operar**.

O primeiro marco nao e "mais documentos": e um caso v2 completo, coerente dentro
das verificacoes existentes e jogavel de ponta a ponta.

## 2. Limites e invariantes

| Tema | Regra |
|------|-------|
| Framework | `CaseGen.Functions` e `CaseGen.Functions.Tests` permanecem em .NET 9. A API continua em seu framework atual. |
| Contrato publico | Preservar `case.json` v2, `case://` e leitura de casos existentes. Novos contratos de autoria sao internos. |
| Idiomas | Toda UI adicionada ou alterada deve ter `pt-BR`, `en-US`, `es-ES` e `fr-FR`. Conteudo e labels dos materiais respeitam o idioma solicitado. |
| Fonte de verdade | JSON publico, documentos, emails, perguntas e explicacao derivam da mesma revisao interna aprovada. |
| Correcao | Normalizar formato nao pode inventar fatos, culpados, resultados periciais ou respostas corretas. |
| Aprovacao | Nenhum erro estrutural, referencia invalida, bloqueador semantico conhecido ou arquivo anunciado ausente pode chegar a publicacao. |
| Falhas | Erro de revisor, renderizacao, persistencia ou publicacao deve ser explicito. Nao retornar sucesso aparente. |
| Avaliacao | O solver nao recebe culpado, gabarito, regras internas nem fatos que o jogador nao pode descobrir. |
| Compatibilidade | Nao reescrever nem republicar casos existentes automaticamente; registrar problemas e tratar migracoes separadamente. |
| Custo | CI usa fixtures e replay local. Pilotos com IA real sao opt-in e exigem um teto aprovado de chamadas/custo. |

Skills orientam autoria; nao substituem contratos, validadores, simulacao e
verificacao dos arquivos. Uma opiniao positiva de outro modelo nao prova a
consistencia de toda a prosa.

## 3. Arquitetura alvo

```text
Request validada + perfil de dificuldade
  -> CaseBible: verdade, alegacoes, entidades e cronologia
  -> EvidencePlan: provas, contraprovas, contexto e descoberta
  -> Autoria por skill sobre uma revisao imutavel
  -> Validacao estrutural, factual e de progressao
  -> Renderizacao + manifest dos arquivos reais
  -> Investigacao pelo blind-solver + revisao semantica
  -> Reparo direcionado, se necessario, e nova verificacao
  -> Aprovacao do pacote
  -> Publicacao: assets primeiro, case.json por ultimo
```

**CaseBible:** separa o que aconteceu, o que cada pessoa alega e o que cada autor
poderia saber na data de seu documento. Inclui identidade, relacoes, locais,
horarios/fusos, motivo, metodo e limites de certeza.

**EvidencePlan:** associa cada material aos fatos que demonstra ou contesta,
origem, autoria, data, confiabilidade, papel investigativo e condicoes de acesso.
Mentiras e divergencias intencionais ficam registradas; erros acidentais nao.

**Estado interno:** evoluir `CaseDraft` antes de criar abstracoes adicionais.
`case.json` e uma saida compilada, nao uma segunda fonte de verdade.

**Manifest:** registra asset ID, arquivo efetivamente produzido, MIME, tamanho,
checksum e revisao. Conteudo de autoria e gabaritos permanecem no armazenamento
interno, fora do payload sanitizado.

Paralelismo fica restrito a tarefas independentes lendo o mesmo snapshot;
cronologia e fatos compartilhados sao definidos antes da escrita dos materiais.

## 4. Entregas e dependencias

A sequencia da tabela e a ordem recomendada de merge. Dependencias representam
prerequisitos tecnicos, nao apenas a posicao na lista.

| PR | Prioridade | Entrega | Depende de |
|----|------------|---------|------------|
| [01](#pr-01---gate-de-qualidade-e-status-honesto) | P0 | Gate de qualidade e status honesto | Nenhum |
| [02](#pr-02---contratos-estritos-e-validacao-local) | P0 | Contratos estritos e validacao local | Nenhum |
| [03](#pr-03---estado-unico-e-reparo-direcionado) | P0 | Estado unico e reparo direcionado | 01, 02 |
| [04](#pr-04---integridade-dos-arquivos-e-publicacao-segura) | P0 | Manifest, integridade e publicacao segura | 01, 03 |
| [05](#pr-05---pericia-e-progressao-v2-no-jogo) | P0 | Pericia e progressao v2 no jogo | Nenhum |
| [06](#pr-06---casebible-e-grafo-de-provas) | P1 | CaseBible e grafo de provas | 02, 03 |
| [07](#pr-07---blind-solver-e-avaliacao-da-jogabilidade) | P1 | Blind-solver e avaliacao da jogabilidade | 04, 05, 06 |
| [08](#pr-08---skills-carregadas-pelo-runtime-e-dificuldade) | P1 | Skills versionadas e perfis de dificuldade | 02, 06 |
| [09](#pr-09---materiais-variados-e-documentos-autenticos) | P2 | Catalogo de materiais e novos layouts | 04, 06, 08 |
| [10](#pr-10---checkpoints-telemetria-e-piloto-controlado) | P2 | Checkpoints, telemetria e piloto | 03, 04, 07, 08 |

### PR 01 - Gate de qualidade e status honesto

**Objetivo:** parar de tratar validacao de schema como aprovacao completa.

Transformar as reproducoes da auditoria em testes permanentes, usando fakes das
interfaces existentes. Introduzir um resultado de qualidade com codigos, area,
referencias e bloqueadores. **Politica confirmada:** `needs_review`, `reject`,
achados medios/altos e revisao invalida/indisponivel bloqueiam publicacao
automatica. Diagnosticos de renderizacao nao ficam apenas na resposta interna.

Renderizar em uma pasta de trabalho isolada e tornar `case.json` visivel aos
leitores somente depois do gate. Isso evita que a persistencia local anterior
ao render exponha um caso reprovado. O PR 04 completa esse mecanismo com
manifest, arquivos imutaveis e verificacao integral do pacote.

Alinhar a activity e o GET de status: um resultado com bloqueadores deve ser
exposto como `failed`, mesmo se o Durable tiver concluido a execucao normalmente.
Preservar o resultado e seus motivos para diagnostico. Na UI, traduzir os novos
estados e mensagens nos quatro idiomas.

O solver atual permanece **diagnostico**, identificado como limitado, ate o PR
07. A divergencia do refine e a integridade completa dos arquivos sao fechadas
nos PRs 03 e 04; este PR nao deve alegar que resolveu esses problemas.

**Arquivos principais:** `CaseV2GeneratorService.cs`,
`CaseV2GenerationOrchestrator.cs`, `RedTeamTask.cs`, modelos de resposta,
`CaseGenerationPage.tsx` e locales.

**Aceite:** publicador nao chamado em rejeicao alta, erro de revisor ou falha de
renderizacao; caso reprovado nao aparece na listagem local;
GET/atividade/UI concordam sobre falha; modos sem publicacao continuam
explicitos, sem anunciar que um caso foi publicado.

**Implementado na primeira entrega:** gate tipado, validacao local do contrato
do red-team, bloqueio conservador de refine aceito ainda nao sincronizado,
renderizacao isolada e verificacao de existencia/tamanho dos alvos das URIs,
upload de `case.json` por ultimo, falhas logicas refletidas na activity/GET,
modos e diagnosticos na UI nos quatro idiomas e regressoes permanentes.
Functions e testes continuam em .NET 9, instalado antes da compilacao/testes
no CI. Nao inclui novas skills/layouts, revisao dos corpos dos documentos,
manifest completo ou publicacao imutavel/transacional.

### PR 02 - Contratos estritos e validacao local

**Objetivo:** rejeitar saidas incompletas antes que contaminem etapas seguintes.

Completar schemas das microtarefas, incluindo tipos, campos obrigatorios,
enums, limites e discriminacao das secoes de `bodyDoc`. `TaskRunner` valida o
JSON recebido antes de desserializar. Usar structured outputs nativos quando o
deployment suportar, mantendo validacao local obrigatoria em qualquer caminho.

Validar request, ranks e idiomas; JSON malformado retorna erro de entrada, nao
uma geracao com defaults. Preservar os defaults documentados de requests validas.
Retries sao limitados e recebem o erro especifico; cancelamento nao e retry.

Remover escolha arbitraria de culpado/resposta e resultados periciais forcados.
IDs que mudem exigem reconciliacao de todas as referencias e da autoria interna.
Usar `schemas/case.schema.json` como contrato publico canonico, assegurando sua
distribuicao para as Functions sem criar uma terceira copia divergente.

**Arquivos principais:** `TaskRunner.cs`, `AzureFoundryLLMProvider.cs`,
`Tasks/*.cs`, `Schemas/`, `CaseGen.Functions.csproj` e starter HTTP.

**Aceite:** objeto `{status: generated}` nao vira um suspeito vazio; tabela com
dimensoes invalidas e secao sem conteudo falham cedo; gabarito invalido nao vira
a primeira opcao; cancelamento e entrada invalida nao disparam novas chamadas.

**Implementado na segunda entrega:** schemas compartilhados e fechados para as
microtarefas, secoes de `bodyDoc` discriminadas por conteudo, limites de tamanho,
validacao local anterior a desserializacao e verificacoes de identidade,
referencias, compatibilidade pericial e gabarito antes de atualizar o draft.
Nao se escolhe outro culpado/resposta nem se fabrica resultado pericial.
IDs divergentes sao rejeitados, nao renomeados parcialmente.

Entrada HTTP invalida retorna 400 (413 acima de 16 KiB), sem agendar job;
requests vazias/`{}` preservam defaults e nomes de propriedades validos
continuam case-insensitive. A tela omite campos opcionais vazios e compartilha
os limites de entrada. Microtarefas criticas e red-team tem no maximo uma
tentativa extra com o erro de contrato; solver e refine nao repetem chamadas.
Cancelamento e falhas permanentes nao geram retry; o SDK nao multiplica as
tentativas. Structured outputs nativos sao opt-in por
`AzureFoundry:UseStructuredOutputs`, com projecao compativel e validacao local
integral obrigatoria. Schemas abertos/maiores usam o caminho por prompt,
explicitamente registrado, sem repetir uma chamada rejeitada pelo deployment.

`schemas/case.schema.json` agora e linkado pelo build/publish para
`Schemas/case.v2.schema.json`; a copia versionada nas Functions e a copia
manual pelos scripts foram removidas. O contrato publico v2 e o .NET 9 nao
mudaram. Reparo canonico e revisao dos corpos sao implementados no PR 03;
publicacao imutavel continua no PR 04.

### PR 03 - Estado unico e reparo direcionado

**Objetivo:** impedir que JSON corrigido e documentos representem casos diferentes.

Reparos atuam sobre a revisao interna e indicam componentes afetados. Recompilar
o JSON dessa revisao, repetir consistencia e revisores afetados e regenerar os
materiais dependentes. Nao aceitar um JSON refinado apenas porque tem menos
erros de schema.

Uma correcao semantica com zero erros estruturais pode ser aceita se resolver os
bloqueadores apontados, preservar invariantes e nao criar novos bloqueadores.
Manter inicialmente o limite existente de um passe de reparo; esgotamento gera
falha explicita. So ampliar o limite com evidencia do piloto.

Eliminar os resumos que omitem justamente os fatos necessarios para revisar
um componente. Contextos especificos continuam compactos, mas completos para
a tarefa, com referencias verificaveis.

**Arquivos principais:** `CaseDraft.cs`, `CaseV2GeneratorService.cs`,
`RefineCaseTask.cs`, `SchemaErrorAutoFixer.cs` e tarefas afetadas.

**Aceite:** refine nao introduz culpado inexistente; JSON, emails e PDFs usam a
mesma revisao; reparo 0 -> 0 pode resolver uma rejeicao semantica; avaliacao
anterior nao aprova uma revisao posterior sem nova verificacao.

**Implementado na terceira entrega:** `CaseDraft` canonico com revisao e copia
profunda; contrato fechado de substituicoes de componentes existentes, com
IDs/invariantes/referencias e dependencias verificadas antes de aceitar uma
candidata. Alteracoes de fatos exigem corpos/e-mails/conclusoes/solucao
dependentes no mesmo reparo. Templates preparam os documentos antes da revisao
e nao sao reaplicados durante o render. O auto-fix isolado de JSON saiu do
pipeline; correcoes deterministicas atuam no draft antes da compilacao.

Revisao adversarial recebe corpos completos, regras aninhadas, eventos,
explicacao e JSON compilado. Pericias/perguntas/explicacao compartilham fatos
completos; solver recebe apenas corpos visiveis ao jogador e segue diagnostico.
O reparo unico tambem cobre achados medios/`needs_review`; revisor invalido ou
indisponivel nao provoca reescrita de conteudo. Uma candidata so substitui a
revisao anterior depois de nova consistencia, compilacao, contratos e revisao,
permitindo 0 -> 0 sem bloqueadores. Falhas preservam conteudo/arquivos anteriores
e nao publicam. `Repair` e revisoes analisadas chegam ao status e UI, traduzidos
nos quatro idiomas. Contrato publico v2 e Functions/testes .NET 9 preservados.
Ainda nao inclui CaseBible/proof graph, leitura dos PDFs renderizados, solver
investigativo, novas skills/layouts nem publicacao imutavel.

**Ajuste aprovado durante a entrega:** substituir `Newtonsoft.Json.Schema`,
cujo teto de 1.000 validacoes/hora interrompeu a suite ampliada e poderia afetar
geracoes, pelo pacote MIT `JsonSchema.Net 7.4.0`. Validacao compartilhada de
schemas/formatos/referencias permanece obrigatoria, com regressoes acima do
antigo teto e reutilizacao concorrente. Versao fixada deliberadamente para
evitar os termos adicionais de manutencao dos binarios mais recentes.

### PR 04 - Integridade dos arquivos e publicacao segura

**Objetivo:** publicar um pacote completo ou deixar o caso anterior intacto.

Ampliar o staging introduzido no PR 01. Construir URIs a partir dos arquivos
produzidos, nao de extensoes presumidas. Verificar existencia, MIME, tamanho nao
vazio, checksums e cobertura integral do manifest.

Subir assets com nomes imutaveis por bundle/revisao e atualizar `case.json`
somente depois de todas as verificacoes. Nao sobrescrever arquivos referenciados
pela revisao anterior antes do commit final; proteger concorrencia por case ID.
Blob nao oferece transacao multiarquivo: o JSON final e o ponto de visibilidade,
com publicacao repetivel e limpeza limitada aos arquivos pertencentes ao job.

No MVP, planejar transcricoes explicitamente como documentos enquanto audio real
nao for implementado. Nao anunciar um `.mp3` inexistente. Tipos nao suportados
nao podem passar como arquivos de placeholder.

**Arquivos principais:** `AssetRenderingService.cs`, `CaseV2BlobPublisher.cs`,
assembler e modelos de renderizacao/manifest.

**Aceite:** JPEG e URI concordam; arquivo ausente impede commit; falha no upload
preserva o caso anterior; dashboard nao lista staging; retry nao mistura revisoes.

**Implementado na quarta entrega:** o renderer informa os arquivos reais e MIME;
o pacote verifica assinaturas/terminadores basicos, tamanho, SHA-256, IDs e
cobertura exata. `manifest.json` registra cada evidencia, caminho, MIME, tamanho,
checksum e revisao em `assets/<bundleId>/`. URIs/checksums/metadados publicos sao
derivados desses arquivos e o contrato v2 e validado novamente. O checksum do
bundle e o SHA-256 dos bytes do manifesto, sem ciclo com `case.json`.

Assets e manifesto sao imutaveis por nome; Blob usa `If-None-Match: *`, confere
metadados e baixa/verifica SHA-256 antes do commit condicional de `case.json`.
O snapshot ETag da primeira tentativa acompanha o bundle em memoria: retry
valida arquivos existentes, nao reverte um commit mais novo e pode reconhecer
um commit proprio cuja resposta HTTP se perdeu. Persistencia local prepara
apenas a nova pasta imutavel, usa lock por case ID e troca atomica somente do
JSON, preservando arquivos anteriores e leitores em cache. Conflitos/falhas
ficam explicitos; staging e a copia local nao comitada pertencem ao job.

Transcricoes agora sao planejadas como documentos; audio/video e outros tipos
sem renderer nao geram placeholders. Status/painel mostram verificacao,
revisao, arquivos, tamanho e checksum, nos quatro idiomas. Casos/URIs legados
continuam legiveis; o consumidor tambem reconhece WebP.

Nao inclui transacao entre Blob e disco, retencao/GC automatico de bundles
antigos ou orfaos remotos, retomada persistente de checkpoints entre processos,
extracao/certificacao do conteudo visual de PDFs/imagens nem novas familias de
documentos. Em falha incerta de commit remoto, os arquivos nao sao apagados;
um sucesso remoto seguido de falha local permanece diagnosticado como parcial.

### PR 05 - Pericia e progressao v2 no jogo

**Objetivo:** permitir ao jogador executar a investigacao planejada.

Substituir evidencias `CASE-2024-*` e simulacao local do modulo pericial por assets
visiveis e configuracao `forensicsDefaults` do caso v2. API valida compatibilidade
de analise e evidencia, calcula prazo conforme a politica temporal vigente e
persiste requests; UI acompanha resultados reais.

Alinhar tipos e duracoes entre gerador, API e frontend, incluindo tipos
configurados por caso. Decidir e documentar uma unica base de tempo para prazos,
pois hoje o cliente e o worker usam referencias diferentes.

Cobrir condicoes AND/OR entre eventos distintos, referencias aninhadas, eventos
temporais com payload e idempotencia. O validador de progressao deve espelhar a
semantica efetivamente implementada no runtime.

**Arquivos principais:** `ForensicModule.tsx`, `forensicsService.ts`,
`ForensicsQueue.tsx`, `ForensicRequestController.cs`,
`ForensicsBackgroundService.cs`, `RulesEngineService.cs` e `CaseTriggersController.cs`.

**Aceite:** caso gated permite solicitar a analise correta, receber email/PDF,
desbloquear a prova e enviar a solucao; pedido incompativel e rejeitado;
reabrir a sessao nao perde requests nem duplica reveals.

**Implementacao local:** modulo e fila pericial usam requests persistidos,
assets autorizados e tipos/duracoes do caso. API, worker e UI compartilham um
relogio de jogo persistido a 60x; pausa e retomada preservam minutos, prazos e
historico. Resultados liberam os documentos/emails reais, ou email sem achados,
sem concluir pela mera passagem do prazo no cliente.

AND/OR acumulam eventos entre chamadas e sessoes reabertas. O gerador verifica
alcancabilidade sem promover provas ocultas; a solucao considera provas
autorizadas e analises efetivamente concluidas. UI nos quatro idiomas e
contribuicoes numericas de pontuacao alinhadas ao contrato da API.

A publicacao desta etapa exige a migration aditiva
`PersistForensicGameTimeAndTriggerHistory` junto da API. Nao foi aplicada a um
banco real nem realizado deploy; gerador e testes continuam em .NET 9.

### PR 06 - CaseBible e grafo de provas

**Objetivo:** planejar uma investigacao antes de escrever seus documentos.

Criar contratos internos para entidades, fatos, alegacoes, cronologia e plano
de evidencias. Estabelecer quem fez o que, onde e quando, antes da expansao em
paralelo. Distinguir horarios exatos, intervalos e estimativas de testemunhas.

Cada material cita os fatos que pode representar e o conhecimento de seu autor.
Perguntas, alibis verificados e explicacao apontam para fontes entregues ao
jogador. Perfil de suspeito nao pode afirmar inocencia ou culpa por conhecimento
privado do gerador.

Validar IDs unicos, analises cadastradas/compativeis, referencias completas e
alcancabilidade a partir do estado inicial, incluindo ciclos e prerequisites.
Nao tornar uma prova escondida visivel como atalho para mascarar progressao
impossivel.

Trocar a obrigacao de uma unica "smoking gun" por suficiencia de um conjunto de
provas. Ausencia de achados nao prova inocencia; semelhanca visual nao constitui
identificacao positiva. Atualizar as heuristicas narrativas diretamente afetadas.

**Arquivos principais:** `Models/CaseV2/`, `CaseDraft.cs`, tarefas de planejamento,
`ConsistencyValidator.cs`, builder de regras e specs internas.

**Aceite:** toda pergunta e afirmacao decisiva tem suporte; nenhuma prova
obrigatoria depende de um ciclo; cronologia de documentos e limites tecnicos
nao contradizem fatos canonicos; divergencias intencionais sao rastreaveis.

**Implementacao local:** CaseBible privada validada antes da autoria, com ato
criminal/local/vitima, fatos, alegacoes, conhecimento por autor, fontes e
conjuntos suficientes de provas. Autores recebem apenas o contexto autorizado;
cronologia e descobertas sao compiladas do plano. Documentos, emails, perguntas,
conclusoes e explicacao carregam referencias e trechos ancorados no texto real.

IDs, horarios, remetentes, anexos, compatibilidade pericial, verificacao de
alibis e alcancabilidade sao conferidos contra o plano, sem descartar requisitos
invalidos. Removida a exigencia de uma unica identificacao pericial do culpado;
provas documentais complementares podem bastar, sem analise obrigatoria.
O v2 ainda exige ao menos um metodo pericial disponivel.

Reparos preservam a Bible e ajustam apenas material suportado, com nova
validacao/revisao. Templates nao acrescentam fatos a documentos canonicos.
Schema publico v2 preservado, sem expor Bible ou suporte interno; nova fase
localizada nos quatro idiomas. Trechos ancorados nao provam equivalencia
semantica: a revisao bloqueante compara texto integral e fatos, sem prometer
certificacao cientifica ou jogabilidade completa. Nao houve geracao paga,
deploy ou migracao nesta etapa.

### PR 07 - Blind-solver e avaliacao da jogabilidade

**Objetivo:** avaliar a investigacao, nao adivinhar o culpado a partir de resumos.

Dar ao solver os materiais acessiveis e acoes permitidas: ler, abrir emails,
solicitar analises, avancar tempo e revisar resultados. Registrar um trace com
acoes e fontes usadas. Um estado intermediario sem provas suficientes nao e
tratado como o estado final de um caso gated.

Usar o conteudo integral dos documentos e conferir sua equivalencia com o PDF
renderizado. Nao entregar prompts de imagem como se fossem observacoes da imagem.
Prova visual decisiva exige verificacao multimodal ou representacao acessivel ao
jogador cujo conteudo tenha sido validado; ate isso existir, nao depende
exclusivamente de uma imagem gerada.

Verificar culpado, fontes, caminho de descoberta e pontuacao conforme o runtime.
Manter paridade com `SolutionService` por vetores de teste comuns; nao manter
uma formula divergente no solver. Falha tecnica do solver fica distinta de
"caso sem suporte suficiente".

**Arquivos principais:** `SolverTask.cs`, `RedTeamTask.cs`, projecoes de contexto,
gate de qualidade e testes de paridade com backend.

**Aceite:** solver pode descobrir resultado gated sem receber segredos; fontes
citadas sao reais e acessiveis; gate recusa caminho impossivel e resposta correta
sem suporte. So aqui a avaliacao completa vira requisito de aprovacao.

### PR 08 - Skills carregadas pelo runtime e dificuldade

**Objetivo:** transformar diretrizes em componentes efetivamente usados.

Criar packs versionados em `functions/CaseGen.Functions/Skills/CaseV2/`, copiados
para o build, com instrucoes, contratos, exemplos positivos/negativos e criterios
de qualidade. Loader/registry pequeno vincula cada pack as tarefas existentes;
nao depende de skills instaladas no CLI do desenvolvedor.

| Skill | Responsabilidade |
|-------|------------------|
| `case-architect` | Verdade canonica, causalidade, local e procedimento plausiveis. |
| `timeline-alibi` | Cronologia, deslocamentos, alegacoes e verificacoes. |
| `evidence-designer` | Provas, contraprovas, ambiguidade e descoberta. |
| `forensic-reviewer` | Metodo, amostra, limites e linguagem tecnica defensavel. |
| `artifact-author` | Autoria restrita aos fatos autorizados e voz da fonte. |
| `blind-solver` | Investigacao com conhecimento e acoes do jogador. |

Injetar perfil de dificuldade em planejamento, autoria e QA. Quantidade de
suspeitos/materiais, profundidade de correlacao, pistas falsas e ambiguidades
seguem budgets verificaveis. Dificuldade nao e apenas mais arquivos.

Separar `difficulty` de `requiredRank`; preservar a politica atual de
Rookie/`all_initial` ate uma mudanca de comportamento ser explicitamente aprovada.
Registrar skill, versao, schema e hash do prompt usados em cada tarefa.

**Aceite:** mudar um pack muda o prompt executado; pack ausente/invalido falha
explicitamente; testes comprovam budgets por nivel e contexto autorizado; nao
existem skills documentadas que nunca sejam carregadas.

### PR 09 - Materiais variados e documentos autenticos

**Objetivo:** criar evidencias completas com identidade de origem.

Separar formato (`pdf`, `photo` etc.), tipo documental e papel investigativo.
Estender `EvidenceDocument` apenas no necessario e reutilizar QuestPDF/registry.
O template controla aparencia; o modelo escreve conteudo autorizado. Nenhum
template pode acrescentar fatos tecnicos ou verificacoes que nao foram planejados.

| Lote | Materiais | Identidade |
|------|-----------|------------|
| A | Recorte de jornal, artigo de revista, bilhete | Colunas/manchete/autoria, composicao editorial, papel informal. |
| B | Recibo, escala de trabalho, carta/diario | Campos preenchidos, valores/horarios reconciliados e voz pessoal. |
| C | Conversas, logs e compilacoes de imagens | Dados completos e imagens realmente incorporadas, nao indices ficticios. |

Usar veiculos e conteudo originais ficticios. Jornal anterior ao crime nao conhece
o laudo posterior. Material civil nao ganha selo policial ou tarja confidencial
automaticamente; anotacoes investigativas ficam separadas do documento de origem.

Corrigir roteamento dos laudos e eliminar datas do relogio de renderizacao nos
fatos do caso. Proibir placeholders em campos essenciais, exceto redacoes
intencionais planejadas. Garantir widths das tabelas, totais, texto legivel e
identidade visual realmente distinta.

Selecionar extras pelo papel de prova, corroboracao, pista falsa ou contexto,
com budget de leitura. Nao adicionar volume aleatorio. Gerar no idioma solicitado;
versoes traduzidas do mesmo caso reutilizam fatos/IDs, sem inventar novas historias.
`localizations` declara somente variantes que realmente existem.

**Aceite:** lote A tem tres layouts visualmente distintos e completos; valores,
datas e referencias concordam; nenhum jornal/bilhete explica ao jogador sua funcao
como pista; os quatro idiomas funcionam sem labels fixos em ingles.

### PR 10 - Checkpoints, telemetria e piloto controlado

**Objetivo:** recuperar etapas sem regenerar o caso inteiro e medir qualidade.

Dividir o Durable em etapas suficientemente grandes para checkpoints de
planejamento, autoria, avaliacao/renderizacao e publicacao, em vez de uma activity
unica. Persistir snapshots internos com versao e referencias; manter corpos e
binarios fora do historico Durable.

Implementar limite de concorrencia das chamadas, backoff para erros transitorios,
cancelamento, timeout e budgets. Retomada reaproveita saidas confirmadas sem
misturar versoes de skills/modelos nem republicar componentes incorretos.

Registrar tokens, latencia, retries, reparos, codigos de falha, versoes e motivos
de rejeicao. `seed` controla escolhas deterministicas do harness; nao prometer
igualdade bit a bit entre novas respostas do LLM. Reprodutibilidade vem do replay
de respostas e snapshots registrados.

**Aceite:** falha apos autoria retoma do checkpoint certo; cancelamento nao
publica pacote parcial; budget esgotado fica explicito; cada publicacao e
rastreavel ate revisoes, arquivos e avaliacoes.

## 5. Verificacao e marcos

| Marco | Condicao de conclusao |
|-------|----------------------|
| M1 - Geracao segura e jogavel | PRs 01-05; bloqueadores impedem publicacao, pacote completo e pericia v2 ponta a ponta. |
| M2 - Harness canonico | PRs 06-08; fatos/provas rastreaveis, solver operacional e skills executadas. |
| M3 - Variedade real | PR 09 lote A; jornal, revista e bilhete com conteudo completo e estilos distintos. |
| M4 - Operacao controlada | PR 10; recuperacao, budgets, telemetria e piloto autorizado. |

**Suite offline:** promover os cenarios da auditoria para xUnit; casos validos e
invalidos em fixtures; testes do schema, referencias, ciclos, reparos, manifest,
revisores, status, regras, sanitizacao e experiencia pericial. Usar o Vitest
existente e testes de backend, sem introduzir outro runner.

**Corpus inicial:** cobrir Rookie, Detective e um nivel avancado; variar cenario,
procedimento e familias documentais; incluir os quatro idiomas. Casos deliberadamente
invalidos devem ser rejeitados, nao "consertados" inventando a resposta.

**Metas objetivas:** zero arquivos anunciados ausentes, zero referencias orfas,
zero bloqueadores conhecidos publicados e zero segredos no contexto do solver.
Todas as perguntas exigem fontes identificadas. Medir taxa de aprovacao sem
reparo, taxa de reparo/rejeicao, sucesso do solver, variedade, tokens e latencia.
Metas de custo e de qualidade narrativa ficam definidas com o corpus/piloto,
nao com percentuais arbitrarios antes da medicao.

**Documentos renderizados:** validar PDF nao vazio, texto esperado e ausencia de
placeholders; inspecionar amostras visuais e verificar imagens incorporadas quando
previstas. Validar o JSON ou o nome do layout sozinho nao demonstra autenticidade.

**CI:** aproveitar `.github/workflows/cd-dev.yml` e garantir instalacao explicita
do SDK 9 no job que compila/testa as Functions, junto ao SDK exigido pela API.

**Documentacao por entrega:** atualizar pipeline/spec v2 conforme os PRs 01-07,
perfis de dificuldade no PR 08, templates no PR 09 e operacao local no PR 10.
Mudancas de comportamento tambem atualizam os capitulos pertinentes do GDD em
ingles e seu espelho em portugues; nao documentar funcionalidades apenas
planejadas como implementadas.

Comandos existentes a usar conforme o escopo de cada PR:

```bash
dotnet test functions/CaseGen.Functions.Tests/CaseGen.Functions.Tests.csproj --no-restore --verbosity minimal
dotnet test backend/CaseZeroApi.Tests/CaseZeroApi.Tests.csproj --no-restore --verbosity minimal
dotnet test backend/CaseZeroApi.IntegrationTests/CaseZeroApi.IntegrationTests.csproj --no-restore --verbosity minimal
cd frontend && npm run test:run
cd frontend && npm run build
```

Selecionar testes relacionados em cada entrega; suite completa nos marcos.
Preparar dependencias ausentes somente quando necessario ao comando escolhido.

## 6. Rollout e decisoes antes de executar

Manter o caminho de leitura v2. Ativar o novo planejamento/autoria primeiro em
dev, com casos novos isolados; gates de integridade nao podem ser desativados por
rollback do pipeline criativo. Revisoes reprovadas nunca aparecem como casos
publicos. No PR 01, diagnosticos ficam no resultado do job e arquivos temporarios
sao removidos; retencao versionada de rascunhos para revisao e trabalho posterior.

As seguintes politicas orientam o MVP. A revisao semantica ja foi confirmada;
as demais devem ser confirmadas antes do PR correspondente:

| Decisao | Proposta | Momento |
|---------|----------|---------|
| Revisao semantica | Confirmado: bloquear `needs_review`, `reject`, achados medios/altos e revisao invalida/indisponivel; nao criar UI de aprovacao manual nesta entrega. | PR 01 |
| Tempo pericial | Uma base de tempo compartilhada por API/UI/worker, alinhada ao mecanismo de tempo do jogo. | PR 05 |
| Dificuldade e unlock | Manter compatibilidade Rookie/`all_initial`; ajustar politica somente com aprovacao e cenarios de regressao. | PR 08 |
| Idiomas | Suportar qualquer um dos quatro idiomas por geracao; nao multiplicar automaticamente todo job por quatro. Traducoes posteriores preservam a mesma verdade. | PR 09 |
| Audio/video reais | Fora do primeiro MVP; transcricoes e frames sao materiais explicitos, completos e acessiveis. | PR 04/09 |
| Piloto pago | Definir quantidade de casos, modelos e teto financeiro antes de chamadas reais. | PR 10 |

Fora do escopo inicial: novo contrato v3, editor visual de casos, infraestrutura
nova, mudanca do sistema de pontuacao e migracao automatica do acervo existente.

## 7. Primeira entrega para comecar

Abrir uma branch dedicada a partir da `main` e executar **somente o PR 01**.

1. Criar fixtures/fakes pequenos nas Functions.Tests para rejeicao alta/media, erro de revisor e falha de renderizacao.
2. Definir codigos de bloqueio e resultado de qualidade, preservando diagnosticos completos.
3. Conectar o gate ao gerador; usar pasta isolada para render e impedir persistencia visivel/publicacao quando houver bloqueadores.
4. Corrigir activity/GET para expor falha logica mesmo com Durable `Completed`.
5. Atualizar DTOs, tela de geracao e os quatro locales; distinguir execucao concluida de caso aprovado/publicado.
6. Cobrir caminho aprovado, caminho rejeitado e modo sem publicacao; explicitar a limitacao do solver legado.
7. Atualizar `CASE_GENERATION_PIPELINE.md` apenas com o comportamento realmente entregue e revisar o PR antes do proximo.

Nao incluir skills, novos layouts ou reescrita do planejamento nesse primeiro
PR. Sua conclusao e uma barreira de publicacao testavel, com status honesto, nao
a promessa de que toda a qualidade narrativa ja foi resolvida.
