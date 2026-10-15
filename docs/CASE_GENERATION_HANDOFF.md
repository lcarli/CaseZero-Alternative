# Retomada: CaseGen, reconciliacao e publicacao DEV

**Branch:** `lcarli/feat-casegen-quality-gate`.
**Estado:** trabalho salvo para continuar em outra maquina; ainda nao pronto para deploy.

Este e o ponto de retomada. O usuario pediu commit/push antes de mudar de
computador. A publicacao sera por CI/CD ou pela maquina autorizada a acessar o
tenant. Nao tentar autenticar neste computador nem mudar permissoes, politicas,
rede ou autenticacao do modelo.

## Decisoes que devem ser preservadas

- Incorporar `origin/main` (`ff78eb5`) somente na feature. A main nao foi alterada.
- Usar a CaseBible rica de main como arquitetura canonica, com os gates,
  autoria restrita, reparos protegidos e bundles imutaveis da feature.
- Concluir a arquitetura e a validacao offline antes de uma unica publicacao.
- Publicar **somente** `casegen-func-dev`, no resource group
  `casezero-func-dev-rg`, preservando recursos e app settings existentes.
- Functions e testes permanecem em **.NET SDK 9**; API permanece em .NET 8.
- Nao publicar API/frontend, aplicar migrations ou fazer merge na main.
- Nao trocar silenciosamente o provider por managed identity: preservar a
  autenticacao por chave existente, que o usuario confirmou funcionar no Azure.
- O piloto e separado do deploy: **um Rookie, pt-BR**, com modelo real e custo
  autorizado, ID novo e publicacao desabilitada. Nao iniciar Detective ou lotes.

## O que esta implementado

| Area | Implementacao atual |
|------|---------------------|
| Fonte canonica | Uma CaseBible rica; autores recebem apenas observacoes atribuidas da sua fonte, nao culpado/gabarito. |
| Qualidade | Schema unico versionado; contratos locais; veto explicito do revisor preservado; reparos nao mudam verdade, respostas ou achados periciais. |
| Materiais | 21 layouts, incluindo jornal, revista, bilhete, diario e escala; cabecalhos/assinaturas completos nos quatro idiomas. |
| PDFs reais | Extracao do arquivo produzido, conferindo todo conteudo autorado e citacao atribuida completa. Corrigida a substituicao de hifens/dois-pontos por glifos privados do Inter, sem normalizar fatos incorretos para aprova-los. |
| Investigacao | Blind investigator usa materiais publicos, acoes oferecidas e relogio virtual; testemunha de grafo permanece apenas diagnostica. Scoring compartilhado entre Functions/API. |
| Progressao | Historico cumulativo, anexos, pericias pendentes/concluidas, emails sem achados e acoes de apresentacao. IDs sinteticos de email seguem o Int32 positivo do produtor real. |
| Skills/runtime | Seis skills carregadas; identidade de provider/deployments, codigo, prompts/schema; teto de saida enviado ao SDK real atraves do wrapper; falhas mantem estimativas conservadoras, imagens registram unidades/uso desconhecido. |
| Checkpoints | Sete macroetapas privadas e imutaveis; checksum; vinculo job/request/predecessor; limites de 20 MiB; limpeza dos workspaces; replay de etapa confirmada sem nova chamada. |
| Execucao incerta | Reserva privada gravada antes da etapa. Sem checkpoint confirmado, nao repetir automaticamente uma etapa que pode ter consumido chamadas pagas. Relatorio de falha e uso fica no armazenamento privado quando possivel; reserva permanece mesmo se esse relatorio falhar. |

Uma reserva ou estimativa de tokens nao e fatura. O custo real de uma chamada
sem resposta e de imagens pode continuar desconhecido. A retomada confirma
etapas concluidas; nao garante recuperar/reexecutar uma etapa interrompida.

## Validacao mais recente e bloqueadores

A ultima execucao completa offline das Functions teve **702 testes: 682
passaram e 20 falharam**, sem testes ignorados. Compilacao das Functions/testes
em net9.0 concluida. Os novos testes de PDF real, simulador, SDK limitado e
execucao de checkpoints passaram nessa execucao.

As falhas completas estao nos testes de RedTeam, contratos periciais,
AssetPortfolio, normalizacao da Bible, TaskRunner, status e fixtures de
solver/golden/Phase9; um cenario de revisor indisponivel agora esgota o teto
conservador de tokens. Reproduzir com o comando abaixo, nao considerar essas
falhas resolvidas ou a suite verde. Migrar expectativas obsoletas (aprovacao
somente pelo grafo, fabricacao/renomeacao de fatos) sem enfraquecer gates; separar
isso de bugs reais de contrato/validacao.

Passaram tambem 20 integracoes selecionadas da API (scoring e email pericial
sem achados) e o build frontend. Isso nao e suite completa da API/frontend.
Os resultados historicos de `11afbcc` (335 Functions, 106 API, 66 frontend)
nao certificam esta reconciliacao.

Pendencias antes de publicar:

1. Resolver as falhas completas e confirmar suites afetadas, incluindo API/frontend.
2. Fechar admissao atomica global: o starter ainda tem singleton best-effort e
   pode prosseguir quando a consulta falha.
3. Provar cancelamento/terminacao e a barreira final de publicacao, inclusive
   corrida com activity em andamento; nao basta cancelar o token local.
4. Validar o store Blob real com round-trip/checksum/tamanho/concorrencia e
   reservas. Os testes de execucao usam um store em memoria.
5. Fechar compilacoes com imagens embutidas e regressao contra citacao parcial
   ou suporte emprestado de outra fonte.
6. Resolver compatibilidade de orchestrator/versao e exigir ausencia de jobs
   ativos antes do deploy.
7. Preparar e validar uma rota de CI/CD **Functions-only**.

O workflow atual `.github/workflows/cd-dev.yml` publica a pilha completa e
nao tem seletor Functions-only. **Nao o disparar como esta.**
A `.azure/deployment-plan.md` continua Draft.

## Retomar no outro computador

Com a worktree local limpa (nao descartar trabalho local):

```bash
git fetch origin
git switch lcarli/feat-casegen-quality-gate
git pull --ff-only
git rev-parse HEAD
```

Usar .NET SDK 9 e manter chamadas reais desabilitadas durante a validacao:

```bash
CASEZERO_RUN_REAL_GENERATION=0 dotnet test functions/CaseGen.Functions.Tests/CaseGen.Functions.Tests.csproj --verbosity minimal
```

Em PowerShell, definir `$env:CASEZERO_RUN_REAL_GENERATION = "0"` e executar
o mesmo `dotnet test` sem o prefixo Bash.

Prompt sugerido para a nova conversa:

> Continue a partir de docs/CASE_GENERATION_HANDOFF.md nesta branch. Termine
> as pendencias e a validacao sem enfraquecer gates nem inventar fatos.
> Functions/testes devem ficar em .NET SDK 9; preserve os quatro idiomas.
> Depois prepare e valide uma publicacao somente das Functions para
> casegen-func-dev, resource group casezero-func-dev-rg, por CI/CD ou por esta
> maquina autorizada. Nao publique API/frontend, nao aplique migrations,
> nao modifique permissoes/rede/autenticacao nem a main. Mostre o plano
> validado antes de executar o deploy. O piloto real Rookie pt-BR deve ser
> separado, isolado, com ID novo e publicacao de caso desabilitada.

## Publicacao e piloto: somente depois dos bloqueadores

Confirmar subscription, tenant, runtime net9.0/dotnet-isolated, armazenamento
privado e nomes/presenca dos app settings sem exibir valores secretos.
Preservar a chave existente no Azure; `local.settings.json` e ignorado e nunca
deve entrar no pacote/Git. Verificar que nao ha jobs Durable ativos.

Para CI/CD, implementar um caminho manual Functions-only reutilizando as
credenciais e o environment DEV existentes, mantendo defaults full-stack
inalterados. Deve falhar se o app/pacote nao existir e nao executar migrations,
deploy API/frontend ou chamadas ao modelo. Validar o pacote e registrar
o resultado no plano antes de disparar.

Se for necessario publicar da outra maquina, preparar/validar primeiro o
pacote e o contexto Azure, depois usar o mecanismo suportado pelo Function App
existente. Nao executar comandos de deploy copiados sem confirmar plano,
subscription e compatibilidade do hosting.

O runner `tools/CaseGen.Pilot` permanece como ferramenta local opt-in, mas
o usuario confirmou que **o teste real deve acontecer na Function Azure**.
Nao voltar a tentativas locais de chave/Entra nem usar o runner local como
substituto para o piloto remoto.

Antes da invocacao remota, assegurar um modo isolado de diagnostico que gere
os PDFs/imagens sem publicar/listar o caso. `writeToDisk=false` e preview
e **nao renderiza materiais**, portanto nao substitui esse modo. Nao presumir
que o endpoint atual ja oferece toda a isolacao necessaria.

Depois analisar Bible, texto integral, arquivos efetivos, identidade/autoria,
datas, alegacoes intencionais, certeza pericial e suficiencia das provas.
Uma falha de qualidade tambem e resultado valido; nao repetir indefinidamente.
Somente considerar Detective depois de Rookie aprovado e analisado.

**Nenhum deploy ou caso real aprovado foi demonstrado nesta branch.**
