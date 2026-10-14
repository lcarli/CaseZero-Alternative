# Retomada: geracao de casos e piloto Rookie

**Atualizado:** 2026-10-14. **Branch:** `lcarli/feat-casegen-quality-gate`.

Este documento e o ponto de retomada em outra maquina ou conversa. O plano
completo esta em [CASE_GENERATION_IMPLEMENTATION_PLAN.md](CASE_GENERATION_IMPLEMENTATION_PLAN.md);
o fluxo atual esta em [CASE_GENERATION_PIPELINE.md](CASE_GENERATION_PIPELINE.md).
Os numeros PR 01-10 sao entregas do plano, nao pull requests ja abertos/mergeados.

## Estado da implementacao

| Entrega | Estado salvo nesta branch |
|---------|--------------------------|
| 01 | Gate de qualidade bloqueante e status de falha coerente entre Functions e UI. |
| 02 | Contratos fechados, validacao local das microtarefas e retries limitados. |
| 03 | Draft canonico e reparo atomico direcionado, seguido de recompilacao/revisao. |
| 04 | Bundles imutaveis, manifest/checksums e publicacao condicional. |
| 05 | Pericia real, relogio persistido e descoberta cumulativa na API/frontend. |
| 06 | CaseBible privada, autoria restrita, grafo de provas e suporte ancorado. |
| 07-10 | Planejados: blind-solver, skills de runtime/dificuldade, materiais variados e operacao/checkpoints. |

Nao houve deploy, merge na main, publicacao de caso de piloto ou aplicacao da
migration a um banco real. Antes de publicar a API do PR 05, avaliar/aplicar a
migration aditiva `PersistForensicGameTimeAndTriggerHistory` no processo normal
de deploy; nao aplica-la apenas para executar o console abaixo.

Na preparacao deste handoff passaram 335 testes das Functions (.NET 9),
106 testes de servicos da API (.NET 8), 66 testes frontend selecionados e os
builds frontend/runner. O lint dos arquivos alterados ainda aponta nove erros
preexistentes, reproduzidos tambem no commit-base `9cb2218`: seis de Fast Refresh
em `CaseContext.tsx` e tres de `no-explicit-any` em `services/api.ts`.
Nao foram corrigidos como parte desta entrega. Isso nao e uma avaliacao de um
caso real nem prova de jogabilidade completa.

## Decisao imediata do usuario

O usuario autorizou chamadas ao modelo real. Comecar por **um unico Rookie em
pt-BR**, analisar o resultado e so considerar Detective se o Rookie passar.
Nao iniciar lotes ou implementar PR 07-10 antes dessa analise.

O piloto deve usar o gerador e os renderers reais, com ID/diretorio novos e
**publicacao desabilitada**. Nao enfraquecer contratos, gates ou limites de retry
para fabricar um sucesso. Um bloqueio real do pipeline tambem e um resultado a
ser analisado.

### Bloqueio encontrado

Nenhum modelo retornou conteudo e nenhum caso/PDF/imagem foi gerado:

1. O provider normal leu a chave local, mas o endpoint respondeu
   `HTTP 403 AuthenticationTypeDisabled`: autenticacao por chave desativada.
2. Uma tentativa isolada via Entra usou o tenant padrao errado.
3. Com o tenant configurado, a credencial local estava expirada
   (`AADSTS700082`); o login interativo foi interrompido.

O usuario vai verificar a configuracao do recurso no portal a partir de uma
sessao com acesso ao tenant. Se permitido pela organizacao, o recurso precisa
ter `disableLocalAuth=false` para aceitar a chave existente. A propagacao nao
e imediata. Se Azure Policy proibir chaves, nao remover/contornar a politica:
alinhar uma alternativa autorizada com o usuario.

Nao houve mudanca de permissoes, politicas ou rede no Azure. O runner versionado
usa o provider normal por chave; nao depende dos experimentos Entra da sessao
anterior. Nao assumir que apenas ter um `TenantId` configura autenticacao Entra
no provider atual.

## Retomar na outra maquina

1. Buscar e fazer checkout de `lcarli/feat-casegen-quality-gate`.
2. Instalar/usar **.NET SDK 9** para Functions, testes e piloto.
3. Recriar localmente `functions/CaseGen.Functions/local.settings.json` com
   credenciais autorizadas. Esse arquivo e ignorado e nao viaja pelo Git.
4. Confirmar com o usuario a resolucao do bloqueio de autenticacao e executar
   **uma** geracao no diretorio novo escolhido.

O console aceita configuracao na raiz e em `Values`, incluindo chaves com
`:` ou `__`; variaveis de ambiente podem sobrescreve-las. Exemplo de estrutura,
**somente com placeholders**:

```json
{
  "Values": {
    "AzureFoundry__Endpoint": "https://RESOURCE.openai.azure.com/openai/v1/",
    "AzureFoundry__ApiKey": "REPLACE_LOCALLY_DO_NOT_COMMIT",
    "AzureFoundry__ModelName": "TEXT_DEPLOYMENT",
    "AzureFoundry__ImageDeploymentName": "IMAGE_DEPLOYMENT"
  }
}
```

Usar os deployments do recurso correto. Nao colar chaves na conversa, gravar
segredos em prompts ou comitar configuracoes locais, `bin`, `obj` ou traces.
O build das Functions pode copiar configuracao local para sua saida; nao
compartilhar o diretorio de build como se fosse um pacote publico.

Da raiz do repositorio, em Bash ou PowerShell:

```text
dotnet run --project tools/CaseGen.Pilot/CaseGen.Pilot.csproj -- . ../casezero-pilot-rookie-001
```

O primeiro argumento e a raiz do repositorio. O segundo e um diretorio **ainda
inexistente**, preferencialmente fora do repositorio e nunca a pasta de casos
de producao. Usar outro nome para cada execucao; nao sobrescrever resultados.
O tema inicial e um furto nao violento em um museu ficticio de Curitiba.

O runner instancia `AzureFoundryLLMProvider` explicitamente (nunca o mock),
usa os templates/renderers de producao, um reporter local e um publicador
desabilitado que lanca erro se chamado. `writeToDisk=true` e necessario para
renderizar; `false` nao produz os documentos para inspecao.

### Artefatos e resultado

| Artefato | Conteudo |
|----------|----------|
| `request.json`, `execution.json` | Request efetiva e modo de execucao, sem copiar credenciais. |
| `phase.json`, `phases.jsonl` | Fase atual e historico local. |
| `calls/` | Prompts, schemas, respostas brutas, uso e duracao de cada chamada. |
| `prepared_assets_*`, `draft_*`, `review_*`, `consistency_*` | Material/drafts capturados quando a geracao chega a essas etapas. |
| `response.json`, `candidate.case.json` | Resultado e candidato, inclusive quando reprovado pelo gate. |
| `failure.json` | Falha de execucao, incluindo fase, quando o pipeline foi iniciado. |
| `usage.json` | Chamadas concluidas/falhas, tokens retornados e duracao; nao e uma fatura. |
| `rendering.json`, `rendered-evidence/` | Diagnosticos e copias de arquivos renderizados, preservadas antes da limpeza de staging. |
| `cases/<caseId>/` | Caso local somente se aprovado e persistido, com bundle verificado. |

Exit codes: `0` aprovado/persistido localmente; `1` falha de execucao;
`2` bloqueio de qualidade; `64` argumentos invalidos. Nem toda falha chega a
produzir todos os arquivos. Nao interpretar a mera existencia de
`candidate.case.json` ou de PDFs parciais como aprovacao/publicacao.

Os traces brutos da maquina anterior ficaram apenas na sessao local. Eles nao
sao dependencia para retomar: o codigo do runner e o diagnostico sem segredos
estao nesta branch. A antiga ferramenta sintetica `casegen-audit` da sessao nao
e compativel com o gerador atual e nao deve ser usada como piloto real.

## Analise obrigatoria apos o piloto

Comparar a Bible/plano com o texto integral entregue: documentos, tabelas,
transcricoes, emails, perfis, perguntas e explicacao. Conferir identidade,
autoria/conhecimento, datas de evento/emissao/abertura, alegacoes intencionais,
limites periciais e suficiencia do conjunto de provas.

Inspecionar os PDFs reais, nao apenas `description`, `bodyDoc` ou prompts de
imagem. Quando disponiveis, `pdftotext` e `pdftoppm` ajudam na extracao e revisao
visual. Avaliar variacao, legibilidade e realismo; labels tecnicos/idiomas
indevidos ou documentos repetitivos sao achados, nao motivos para ignorar gates.

Se o pipeline falhar antes de renderizar, analisar request/resposta original,
retry e contrato especifico. Distinguir erro de modelo, contrato/prompt fragil,
inconsistencia real e falha de provider/runtime. Nao repetir indefinidamente.

Limites importantes:

- Trechos `support` ancorados provam presenca textual, nao equivalencia semantica.
- O solver atual e **diagnostico**, nao executa uma investigacao completa.
- Rookie usa `all_initial`: sucesso nao comprova descoberta gated.
- Imagens geradas nao podem ser prova decisiva apenas pelo texto de seu prompt.
- Nenhum caso real aprovado foi demonstrado ate este handoff.

## Depois da analise

Se Rookie passar, considerar um Detective separado para exercitar descoberta
gated, solicitacao pericial, prazos, resultados e envio de solucao. Corrigir
primeiro problemas comprovados pelo piloto, sem implementar varios PRs no escuro.

Retomar entao o plano: PR 07 (blind-solver/paridade de jogo), PR 08 (skills de
runtime e budgets de dificuldade), PR 09 (jornais, revistas, bilhetes e outros
layouts originais), PR 10 (checkpoints, telemetria e operacao).

Manter Functions/testes em .NET 9, API em .NET 8, contrato publico v2 e os
quatro idiomas da UI (`pt-BR`, `en-US`, `es-ES`, `fr-FR`). Nao publicar, aplicar
migrations ou fazer merge/deploy automaticamente como parte deste piloto.
