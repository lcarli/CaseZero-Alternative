# Estado atual da implementação CI/CD

Este documento resume o que existe no repositório em julho de 2026. Ele não é
um roadmap nem uma declaração de funcionalidades futuras.

## Implementado

### Pipeline da aplicação DEV

`.github/workflows/cd-dev.yml`:

- build do frontend com Node.js 20;
- build da API com .NET 8;
- build de `CaseGen.Functions` com .NET 9;
- testes unitários e de integração da API;
- testes da Function;
- testes do frontend;
- validação dos casos versionados contra o schema v2;
- artifacts separados para frontend, API e Function;
- deploy no App Service, Function App e Static Web Apps de DEV.

Pull requests para `main` executam validação sem deploy. Pushes elegíveis para
`develop` ou `main` implantam em DEV.

### Infraestrutura como código

`.github/workflows/infrastructure-3tier.yml`:

- compilação e validação Bicep;
- deployment em nível de assinatura;
- parâmetros separados para `dev` e `prod`;
- análise `what-if`;
- deploy manual;
- destruição protegida por confirmação;
- output do deployment como artifact.

Os módulos Bicep provisionam camadas separadas para frontend, API, Functions,
dados, rede e recursos compartilhados.

### Segurança de runtime

- HTTPS e TLS mínimo;
- identidades gerenciadas;
- Key Vault;
- Storage privado;
- VNet e private endpoints;
- DNS privado;
- RBAC para Blob, Queue e Key Vault;
- segredos fora do código;
- CORS configurado por ambiente.

### Observabilidade

- Application Insights;
- Log Analytics;
- logs dos workflows;
- artifacts de build/deployment;
- status detalhado dos jobs de geração;
- telemetria de fases, retries, tokens, solver e publicação.

## Não implementado como workflow

O repositório não possui atualmente:

- `ci.yml` independente;
- `cd-prod.yml`;
- deploy blue/green;
- slots com swap automático;
- rollback automático;
- CodeQL dentro dos dois workflows existentes;
- notificações do Teams;
- release automática;
- canary deployment.

Esses itens não devem ser descritos como concluídos.

## Ambientes

O IaC suporta parâmetros `dev` e `prod`. O pipeline automatizado da aplicação
existente é específico para DEV. Operações de PROD dependem da infraestrutura e
de um processo de deploy que ainda não está representado por um workflow
dedicado neste repositório.

## Toolchain

| Área | Versão |
|---|---|
| Frontend | Node.js 20 |
| API | .NET 8 |
| CaseGen.Functions | .NET 9 |
| Azure Functions | v4, isolated worker |
| Infraestrutura | Bicep + Azure Verified Modules |

## Critérios de sucesso atuais

Uma execução do pipeline da aplicação somente é bem-sucedida quando:

1. todos os projetos compilam;
2. testes da API, Function e frontend passam;
3. casos versionados validam contra o schema;
4. artifacts são publicados;
5. os três deployments de DEV terminam sem erro.

Para a geração de casos, sucesso também exige validação final, solver mínimo de
`0.90`, renderização obrigatória e publicação completa no Blob quando
configurada.

## Limitações conhecidas

- Deploy de PROD não está automatizado em workflow dedicado.
- Rate limiting da API é em memória e não distribuído.
- As rotas HTTP diretas da Function usam autorização `Anonymous`; a proteção
  depende da rede e do proxy autenticado da API.
- O singleton de geração é best-effort, não um lock distribuído transacional.
- Mudanças somente em documentação não acionam deploy automático.

## Referências

- [`README.md`](README.md)
- [`azure-setup.md`](azure-setup.md)
- [`variables-and-secrets.md`](variables-and-secrets.md)
- [`../DEPLOYMENT.md`](../DEPLOYMENT.md)
- [`../CASE_GENERATOR_SETUP.md`](../CASE_GENERATOR_SETUP.md)
