# T-101 — Especificação de aquisição
> Épico **E-01** · Caminho crítico do MVP-0a · Risco **R-023**
> Base: P4, PRE-04, PRE-18 · Topologia: **ADR-0002** · Sessão S005 · 2026-08-08

---

## 1. Por que este item vem primeiro

O prazo de entrega do hardware é **a única dependência externa restante** do início do MVP-0a, depois
que ADR-0014 tirou a consulta a fornecedores do caminho crítico. Tudo em E-01 espera por esta compra,
e E-02 em diante espera por E-01.

Nada aqui pode ser paralelizado com código: **comprar é o gargalo**.

## 2. O que a topologia exige

ADR-0002 decidiu: **controlador de domínio e RD Session Host não coabitam a mesma instância de sistema
operacional**. Um único host físico com Hyper-V, duas VMs.

| VM | Papel | vCPU | RAM | Disco |
|----|-------|------|-----|-------|
| **AB-DC01** | AD DS + DNS | 2 | 4 GB | 80 GB |
| **AB-RDS01** | RD Session Host + Connection Broker + Licensing + aplicativos + Control Plane (MVP-0) | 6 | restante | 250 GB + volume de perfis |

> O Control Plane coabita a AB-RDS01 **apenas no MVP-0** e precisa sair antes do piloto (ADR-0002),
> quando passar a guardar dado de outros tenants.

## 3. Especificação do host físico

Dimensionado para PRE-01 (~10 usuários simultâneos, 6–8 aplicativos), com margem para o dogfood
crescer sem nova compra.

| Item | Mínimo | Recomendado | Justificativa |
|------|--------|-------------|---------------|
| **Processador** | 8 núcleos físicos | 8–12 núcleos, geração recente | Windows Server licencia por núcleo: núcleo a mais é licença a mais. Preferir núcleos mais rápidos a mais núcleos |
| **Virtualização** | VT-x/AMD-V + SLAT habilitados na BIOS | idem | **Confirmar antes da compra — PRE-18.** Sem isso, o Hyper-V não roda e a topologia do ADR-0002 cai |
| **Memória** | 32 GB ECC | **64 GB ECC** | 4 GB para o DC, ~4 GB para o hipervisor, o resto para sessões. `PREMISSA:` (PRE-27) 2–3 GB por sessão com Domínio + Alterdata + Excel simultâneos — **a medir no dogfood** |
| **Disco — sistema** | 2 × 480 GB SSD NVMe em espelho | idem | Espelho porque host único já é ponto único de falha (R-023) |
| **Disco — perfis FSLogix** | 1 TB SSD | 2 × 1 TB SSD em espelho | `PREMISSA:` (PRE-28) 20–30 GB de container por usuário. 10 usuários ≈ 300 GB, com folga para crescer |
| **Disco — backup local** | 2 TB, **volume separado** | disco externo ou NAS | RNF-033. Backup no mesmo volume dos dados não é backup |
| **Rede** | 1 GbE | 2 × 1 GbE | RDP consome pouco; a placa dupla é para separar gerência de tráfego de sessão |
| **Energia** | — | **nobreak** | Queda durante escrita em container FSLogix corrompe perfil |

> **Sobre memória, com franqueza:** 32 GB atendem 10 usuários **se** os aplicativos se comportarem.
> Domínio, Alterdata e Excel abertos ao mesmo tempo, por 10 pessoas, é o cenário que estoura memória
> antes de estourar CPU. 64 GB custam pouco a mais agora e evitam uma segunda compra no meio do
> dogfood. **Recomendo 64 GB.**

## 4. Licenças a adquirir

| # | Item | Modalidade | Quantidade | Observação |
|---|------|-----------|------------|------------|
| 1 | **Windows Server 2025 Standard** | por núcleo | todos os núcleos físicos | Cobre **2 VMs** desde que todos os núcleos estejam licenciados e o host seja usado só para virtualização (ADR-0002). É exatamente a topologia decidida |
| 2 | **RDS CAL por usuário** | por usuário | 1 por pessoa que acessar | **Por usuário**, não por dispositivo: a mesma pessoa acessa de mais de uma máquina |
| 3 | Office, se fornecido pelo provedor | volume (LTSC) ou M365 Apps com ativação em computador compartilhado | conforme uso | **OEM/varejo não atende** servidor de sessão (P3). Se o Office for do cliente, vai para a declaração de ADR-0014 |
| 4 | Antivírus compatível com RDS/FSLogix | por servidor | 1 | Com as exclusões recomendadas para containers FSLogix |

**Não entram nesta compra:** Domínio, Alterdata e ERPs — licenças do cliente (ADR-0014).

### 4.1 Verificações antes de fechar

- [ ] O revendedor confirma que a Standard cobre 2 VMs na configuração descrita.
- [ ] RDS CAL é **por usuário** e a quantidade cobre todo o escritório, não só quem usa hoje.
- [ ] Há caminho de upgrade da quantidade de CAL sem recompra do pacote.
- [ ] `PREMISSA:` PRE-18 confirmada na ficha técnica: virtualização assistida por hardware.
- [ ] Prazo de entrega **por escrito** — é ele que define se M2a (out/2026) se sustenta.

## 5. O que perguntar ao revendedor sobre SPLA (T-003)

Aproveitar a mesma conversa. **T-003 é o que valida ou derruba PRE-05** (custo ≤ R$ 50/usuário/mês) e,
com ele, a viabilidade econômica do Caminho B.

1. Valor mensal do **SAL de RDS por usuário** sob SPLA, na faixa de 60–100 usuários (PRE-02).
2. Valor do **Windows Server sob SPLA** por processador ou por usuário.
3. Requisitos para se tornar parceiro SPLA: faturamento mínimo, relatório mensal, contrato.
4. Se há **exigência de hardware dedicado** por cliente hospedado — isso impacta diretamente a
   decisão de session host dedicado por tenant do ADR-0004 e o custo por cliente pequeno.
5. Como o Office é licenciado em ambiente hospedado para terceiros.

> A pergunta 4 é a que mais pode doer: se houver exigência de dedicação de hardware, o custo por
> cliente de 5 usuários explode e a economia de escala invertida já registrada em ADR-0004 fica pior.

## 6. Critério de aceite de T-101

- Nota fiscal emitida e equipamento recebido.
- Licenças ativadas e registradas na `matriz-licenciamento.md` §2.
- PRE-04 e PRE-18 confirmadas.
- Prazo de entrega registrado, com o impacto sobre M2a avaliado em `STATUS.md`.
- Respostas de SPLA registradas para alimentar T-003.
