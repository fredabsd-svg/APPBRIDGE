# Matriz de licenciamento — registro declarado
> Tarefa **T-001** · Portão **G-01** · **Reorientada por ADR-0014**: registro do que o cliente
> declarou possuir, e não resultado de consulta a fornecedor.
> Formato exigido em P3: **aplicativo × versão × tipo de licença × multiusuário S/N**.

---

## 1. Aplicativos do cliente

Licença de titularidade do cliente, que a adquire, instala e usa (ADR-0014).

| # | Aplicativo | Versão | Tipo de licença | Qtd. | Usuários simultâneos | Declarado por | Data |
|---|-----------|--------|-----------------|------|----------------------|---------------|------|
| 1 | Domínio — Contábil | | | | | | |
| 2 | Domínio — Folha | | | | | | |
| 3 | Domínio — Escrita Fiscal | | | | | | |
| 4 | Alterdata — [módulo] | | | | | | |
| 5 | ERP de cliente — [nome] | | | | | | |
| 6 | [legado interno] | | | | | | |

**Coluna "Declarado por":** quem prestou a informação. No dogfood, o próprio escritório; no piloto, o
responsável de cada cliente que assinou a declaração (`README.md` §3).

> A coluna **"usuários simultâneos"** não é burocracia: é ela que alimenta o teto por aplicativo do
> metering (RF-063) quando o MVP-1 chegar, e é o instrumento com que o cliente demonstra respeitar o
> que declarou.

## 2. Licenças da plataforma — responsabilidade do provedor

| # | Item | Modalidade | Qtd. | Origem | Observação |
|---|------|-----------|------|--------|------------|
| 7 | Windows Server 2025 | por núcleo, Standard | | revendedor | Licencia 2 VMs (ADR-0002) |
| 8 | RDS CAL | por usuário | | revendedor | Uma por pessoa que acessar (T-101) |
| 9 | SPLA (Caminho B) | por usuário/mês | | revendedor | **T-003** — valida ou derruba PRE-05 |
| 10 | Office, se fornecido pelo provedor | volume (LTSC) ou M365 Apps com ativação em computador compartilhado | | revendedor | **OEM/varejo não atende** (P3). Se o Office for do cliente, vai para a §1 |

## 3. Controle das declarações — piloto

| Cliente | Aplicativos declarados | Declaração assinada em | Arquivo |
|---------|------------------------|------------------------|---------|
| | | | |

A declaração é o portão **G-01** na forma vigente (ADR-0014). **Cliente sem declaração assinada não
entra no piloto** — não por formalismo, mas porque é o único instrumento que materializa a alocação
de responsabilidade decidida.

## 4. Critério de conclusão

- **Dogfood:** §1 preenchida para todos os aplicativos que rodarão no MVP-0b.
- **Piloto:** §3 com declaração assinada por cada cliente, e §2 com as licenças de plataforma
  contratadas.

> **Resíduo conhecido:** alguns termos de licença restringem execução em infraestrutura operada por
> terceiro, independentemente de quem detenha a licença. Nesse caso, a declaração não protege o
> provedor. Ver `README.md` §6 e R-001 em `STATUS.md`.
