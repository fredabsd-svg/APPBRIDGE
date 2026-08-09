# T-001 — Registro de licenciamento dos aplicativos
> Tarefa **T-001** · Portão **G-01** · Risco **R-001**
> **Reorientada em 2026-08-08 por ADR-0014.** Origem: P3 · Sessões S003 e S004

---

## 1. O que esta tarefa é — e o que deixou de ser

**Decisão vigente (ADR-0014): o licenciamento dos aplicativos hospedados é responsabilidade do
cliente. O cliente adquire, instala e usa suas próprias licenças.**

Em consequência:

| Antes (S003) | Agora (ADR-0014) |
|--------------|------------------|
| Consultar Domínio e Alterdata por carta formal | **Não se consulta fornecedor.** As cartas foram removidas |
| G-01 = confirmação escrita do fornecedor | **G-01 = declaração de titularidade e conformidade assinada pelo cliente**, anexa ao contrato |
| Matriz = resultado da consulta | **Matriz = registro do que o cliente declarou** |

Isso é coerente com **NO-04** (`VISAO.md`): o AppBridge distribui aplicativos, não licencia software
de terceiros. O ADR-0014 apenas restringe a formulação — passa a ser **sempre** pelo cliente.

## 2. O que a matriz serve agora

`matriz-licenciamento.md` deixa de ser instrumento de consulta e passa a ter três usos operacionais:

1. **Dimensionamento** — saber quantos usuários simultâneos cada aplicativo terá.
2. **Metering** — alimentar o teto por aplicativo (RF-063), quando o metering chegar no MVP-1.
3. **Evidência** — registrar o que o cliente declarou possuir, e quando.

## 3. O que precisa existir antes do piloto

**A declaração de titularidade e conformidade de licença** (ADR-0014, item 3). Sem ela, a alocação de
responsabilidade decidida existe apenas na intenção. Conteúdo mínimo sugerido:

- Identificação do cliente e dos aplicativos que solicita publicar, com versão e quantidade de licenças.
- Declaração de que **detém as licenças** desses aplicativos e responde por sua conformidade.
- Reconhecimento de que os aplicativos serão executados em **servidor de sessão multiusuário**, e de
  que cabe ao cliente verificar essa condição junto ao respectivo fornecedor.
- Menção explícita ao **suporte**: se o fabricante limitar ou recusar suporte por causa desse modo de
  uso, quem fica sem suporte é o cliente.
- Compromisso de manter a informação atualizada quando trocar de versão ou de quantidade de licenças.

> A minuta deve passar por advogado, junto com T-002 (termo de custódia de certificado). Não redigimos
> instrumento contratual aqui.

## 4. O que continua sendo responsabilidade do provedor

O ADR-0014 desloca a licença **dos aplicativos hospedados**, não a da plataforma. Continuam com o
provedor, e entram na matriz com origem "revendedor":

- **Windows Server 2025** e **RDS CAL por usuário** (T-101).
- No Caminho B, avaliação de **SPLA**, que é o que T-003 apura e o que valida ou derruba PRE-05.
- **Office**, quando fornecido pelo provedor: exige licença por volume (LTSC) ou M365 Apps com
  ativação em computador compartilhado — OEM/varejo não atende (P3). Se o Office for do cliente, cai
  na declaração da §3.

## 5. Critério de conclusão de T-001

T-001 está concluída quando a matriz tiver, para cada aplicativo do inventário do dogfood, a linha
preenchida com aplicativo, versão, tipo e quantidade de licença — e, para o piloto, quando cada
cliente tiver assinado a declaração da §3.

## 6. O resíduo que a decisão não elimina — leia antes do piloto

Quem instala não altera o que a licença permite. Se algum fornecedor vedar execução em servidor de
sessão, a vedação continua existindo; o que muda é **quem responde por ela**.

Há ainda um resíduo específico do Caminho B: alguns termos de licença restringem execução em
**infraestrutura operada por terceiro**, independentemente de quem detenha a licença. Nesse caso, a
declaração do cliente não protege o provedor de um questionamento do fornecedor.

Está registrado como **R-001 (reescrito)** em `STATUS.md`, e é assunto para o advogado que revisar a
minuta da §3 — não para ser resolvido aqui.
