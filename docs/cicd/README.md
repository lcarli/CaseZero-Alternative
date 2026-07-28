# CI/CD — CaseZero

Esta pasta documenta os workflows GitHub Actions e a infraestrutura Azure
existentes no repositório.

## Documentos

- [`azure-setup.md`](azure-setup.md): preparação da assinatura e implantação da infraestrutura;
- [`variables-and-secrets.md`](variables-and-secrets.md): referência exata de segredos e variáveis;
- [`implementation-summary.md`](implementation-summary.md): estado atual e limitações.

## Workflows atuais

O repositório contém dois workflows:

| Workflow | Responsabilidade |
|---|---|
| `.github/workflows/cd-dev.yml` | Build, testes e deploy da aplicação em DEV |
| `.github/workflows/infrastructure-3tier.yml` | Validar, implantar ou destruir a infraestrutura Bicep |

Não existem atualmente workflows separados chamados `ci.yml`, `cd-prod.yml`,
`functions-deploy.yml` ou `infrastructure.yml`.

## Build, testes e deploy de DEV

`cd-dev.yml` é acionado por:

- pull request direcionado a `main`: executa somente build e testes;
- push para `develop` ou `main`: executa build, testes e deploy em DEV;
- `workflow_dispatch`: permite execução manual para `development`.

Alterações somente em Markdown, `docs/`, GDD, backlog ou `.gitignore` são
ignoradas no gatilho de push.

### Validações executadas

1. frontend com Node.js 20;
2. API e testes com .NET 8;
3. `CaseGen.Functions` e testes com .NET 9;
4. build de produção do frontend;
5. testes unitários da API;
6. testes de integração da API;
7. testes da Function;
8. testes do frontend;
9. validação dos `cases/*/case.json` contra o schema v2;
10. publicação dos três artefatos.

### Deploy de DEV

Após sucesso no build:

| Componente | Destino |
|---|---|
| API | App Service `casezero-api-dev` |
| Function | Function App `casegen-func-dev` |
| Frontend | Static Web App `casezero-web-dev` |

A Function é publicada com `Azure/functions-action`. O pacote inclui arquivos
ocultos para preservar `.azurefunctions`, necessário pelo runtime usado no
ambiente atual.

O workflow não implementa hoje slots, blue/green, rollback automático,
notificação do Teams ou criação de release.

## Infraestrutura

`infrastructure-3tier.yml` é manual e aceita:

- ambiente: `dev` ou `prod`;
- ação: `validate`, `deploy` ou `destroy`;
- criação opcional do Azure SQL;
- confirmação explícita `CONFIRM` para destruição.

O workflow:

1. autentica no Azure;
2. compila `infrastructure/main.bicep`;
3. valida uma implantação em nível de assinatura;
4. executa `what-if` para ações diferentes de `validate`;
5. implanta com `infrastructure/parameters.<environment>.json`;
6. publica o JSON de output como artifact.

A região configurada pelo workflow é `canadacentral`.

## Arquitetura implantada

A infraestrutura é separada por camadas e resource groups:

- compartilhado: Key Vault, observabilidade e rede;
- dados: Storage e Azure SQL opcional;
- API: App Service e identidade gerenciada;
- Functions: Function App .NET 9 e Storage;
- frontend: Azure Static Web Apps.

Storage e serviços internos usam rede privada, private endpoints, DNS privado e
RBAC por identidade gerenciada conforme os módulos Bicep.

## Ambientes

O IaC aceita `dev` e `prod`, mas apenas o deploy de aplicação para DEV está
automatizado por um workflow dedicado neste repositório. Não descreva PROD como
automaticamente implantado, com blue/green ou rollback enquanto um workflow
correspondente não existir.

## Segurança

- Nunca registre valores de segredos em logs ou artifacts.
- Use GitHub Environments para limitar credenciais por ambiente.
- Prefira identidade gerenciada para acesso entre aplicações e recursos Azure.
- Mantenha Key Vault e Storage protegidos pela topologia privada.
- Restrinja o service principal do workflow ao menor escopo compatível com a
  implantação em nível de assinatura.
- Revise cuidadosamente o `what-if` antes de executar `deploy`.
- A ação `destroy` remove resource groups identificados pelas tags do ambiente;
  use somente com confirmação e revisão do output.

## Operação

### Executar o pipeline da aplicação

1. Abra **Actions** no GitHub.
2. Selecione **Deploy to DEV Environment**.
3. Use **Run workflow** e escolha `development`.
4. Confirme build, testes e os três passos de deploy.

### Executar infraestrutura

1. Abra **Deploy 3-Tier Infrastructure**.
2. Escolha `dev` ou `prod`.
3. Execute primeiro com `validate`.
4. Revise o job de `what-if`.
5. Execute `deploy` somente após validar as mudanças.

## Diagnóstico

### Azure login falhou

Valide o JSON de `AZURE_CREDENTIALS_DEV` ou `AZURE_CREDENTIALS_PROD`, a
assinatura e as atribuições RBAC do service principal.

### Build da Function falhou

Confirme que o runner instalou .NET 9 e que
`CaseGen.Functions`/`CaseGen.Functions.Tests` continuam em `net9.0`.

### Function App não foi encontrada

O workflow ignora o deploy da Function quando `casegen-func-dev` não existe em
`casezero-func-dev-rg`. Implante a infraestrutura primeiro.

### Azure Static Web Apps falhou

Confirme `AZURE_STATIC_WEB_APPS_API_TOKEN_DEV` e o nome do recurso
`casezero-web-dev`.

### Infraestrutura falhou

Revise:

- `infrastructure/parameters.<environment>.json`;
- quotas e providers registrados;
- permissões em nível de assinatura;
- erros do `az deployment sub validate`;
- conflitos de nomes globais;
- políticas Azure que possam modificar ou negar propriedades.
