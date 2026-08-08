# VISÃO — AppBridge
> Entregável 1 de 7 da fase de Design · Sessão S001 · 2026-08-08
> Status: **submetido — aguardando aprovação de Frederico** (RP-04)

*"Instale uma vez. Publique para todos."*

---

## 1. Problema

Escritórios de contabilidade e empresas de pequeno e médio porte operam sobre um conjunto de
aplicativos Windows desktop que **não têm equivalente web** — sistemas contábeis (Domínio, Alterdata),
ERPs de clientes, planilhas Excel de alta complexidade e legados internos. Distribuir esses
aplicativos hoje significa uma de três coisas, todas ruins:

| Modo atual | Custo real |
|---|---|
| **Instalar em cada PC** | Cada atualização é uma peregrinação máquina a máquina; versões divergem; um PC desatualizado corrompe base compartilhada; dado sensível fica espalhado em disco de estação. |
| **Entregar o desktop remoto inteiro** (RDP direto, TeamViewer, AnyDesk) | O usuário recebe um segundo computador em uma janela: dois menus iniciar, dois Explorer, confusão de contexto, superfície de ataque enorme, e nenhum controle de *qual* aplicativo ele pode abrir. |
| **Migrar para a nuvem do fornecedor** | Só resolve o app daquele fornecedor. O ERP do cliente, o legado e a planilha continuam presos ao PC — e a mudança é irreversível e cara. |

O problema central não é "acesso remoto" — isso está resolvido há vinte anos. É que **não existe uma
camada de distribuição, permissão e auditoria de aplicativos Windows** que trate o app como unidade
gerenciável: quem pode abrir o quê, a partir de onde, com qual certificado, consumindo qual licença, e
com que rastro.

### 1.1 Dores específicas do público-alvo

- **PR-01 · Atualização de sistema contábil.** Domínio e Alterdata publicam atualizações frequentes; a base de dados só aceita clientes na mesma versão. Um PC fora de sintonia derruba o escritório.
- **PR-02 · Certificado digital como gargalo humano.** O A1 vive no PC de quem assina. Quem precisa assinar pela empresa X depende da máquina certa, da pessoa certa, presente. O A3 em token amarra fisicamente a operação a uma mesa.
- **PR-03 · Licença de aplicativo sem visibilidade.** O escritório compra N licenças simultâneas e não tem instrumento algum para saber quantas estão em uso, nem para impedir a N+1.
- **PR-04 · Home office e multiescritório.** Acesso de fora hoje é VPN mal configurada, RDP exposto ou software de acesso remoto pessoal — cada um deles um incidente esperando data.
- **PR-05 · Ausência de trilha.** Ninguém sabe dizer quem abriu qual sistema, quando, de onde, e o que assinou. Exigência crescente sob LGPD e em auditorias de clientes.

## 2. Público-alvo

| ID | Segmento | Perfil | Papel na sequência |
|----|----------|--------|--------------------|
| **PA-01** | Escritório de contabilidade de pequeno/médio porte | 10–40 colaboradores, Domínio ou Alterdata como sistema central, certificados A1/A3 de dezenas de clientes sob sua guarda, TI terceirizada ou inexistente | **Cliente primário.** Dogfood (o escritório de Frederico) e piloto do Caminho B. |
| **PA-02** | Empresa de PMEs com ERP desktop legado | 20–200 usuários, um ERP Windows sem versão web, filiais ou home office | Expansão natural após o piloto. |
| **PA-03** | Provedor de TI / revenda que hospeda sistemas para terceiros | Já opera servidor de terminal "na raça", sem painel, sem auditoria, sem metering | **Comprador do Caminho A** (software). Conhece a dor de operar e quer o painel. |

### 2.1 Personas operacionais

- **Usuário final (contador, auxiliar fiscal).** Quer clicar num ícone e trabalhar. Não sabe — e não deve precisar saber — que o aplicativo roda em outro lugar.
- **Administrador do escritório.** Publica app, dá e tira acesso, vê quem está conectado, resolve "travou". Não é engenheiro de infraestrutura.
- **Operador do provedor (Caminho B).** Cuida de vários tenants, precisa de isolamento, atualização orquestrada e evidência de conformidade.

## 3. Proposta de valor

**Para** escritórios contábeis e PMEs presos a aplicativos Windows desktop,
**que** precisam entregar esses aplicativos a usuários dispersos sem perder controle, versão ou rastro,
**o AppBridge** é uma plataforma de distribuição de aplicativos remotos
**que** faz o app do servidor aparecer como aplicativo local no PC do usuário — com atalho próprio,
janela própria, permissão central e auditoria — **sem entregar o desktop do servidor**.
**Diferente de** RDP direto, ferramentas genéricas de acesso remoto ou da nuvem de um fornecedor
específico, **o AppBridge** governa o parque inteiro de aplicativos, incluindo os que nenhum
fornecedor vai levar para a nuvem.

### 3.1 Os quatro ganhos, na ordem em que o cliente sente

| ID | Ganho | Evidência que o cliente percebe |
|----|-------|--------------------------------|
| **VP-01** | **Instale uma vez.** Atualização de sistema contábil deixa de ser tarefa de 30 PCs e vira tarefa de 1 servidor. | Fim do "atualiza aí que a base pediu". |
| **VP-02** | **Parece local.** Atalho no Desktop e no Menu Iniciar, janela própria, sem desktop remoto no meio. | O usuário não muda de hábito. Adoção sem treinamento. |
| **VP-03** | **Controle central.** Quem pode abrir o quê é decisão administrativa, revogável em segundos. | Demissão vira um clique, não uma caçada por senhas. |
| **VP-04** | **Rastro.** Quem, o quê, quando, de onde — e, na V2, com qual certificado assinou. | Resposta pronta para auditoria e LGPD. |

## 4. Diferenciais estratégicos (o fosso)

Estes **não são melhorias incrementais** — são a razão de o produto existir depois que o RemoteApp já
funciona. Tratados como requisitos de produto (Seção 2.3 do prompt mestre), cada um gerará RFs
próprios em `REQUISITOS.md`.

### DIF-01 · Cofre de certificados digitais *(fase V2)*
A1 armazenado centralmente e cifrado em repouso, com a senha guardada separada do arquivo, injetado
na sessão conforme política ("usuário X assina pela empresa Y"), com registro de **cada uso**.
A3/token USB tratado como cidadão de primeira classe no redirecionamento, não como acidente.

Modelo jurídico definido em P6: custódia mediante **termo de autorização de custódia e uso de
certificado digital** assinado pelo titular, anexo ao contrato de prestação de serviços, especificando
finalidades, usuários autorizados, vigência, revogação e compromisso de trilha de auditoria; base legal
LGPD art. 7º, V (execução de contrato). **A minuta precisa passar por advogado antes do piloto**
(pendência registrada, ver §7 e R-002).

*Por que é fosso:* resolve PR-02, que é dor diária e específica do público brasileiro. Nenhum
concorrente genérico de RDS trata certificado ICP-Brasil como objeto de primeira classe, porque
nenhum deles foi construído para escritório contábil brasileiro.

### DIF-02 · Medição e limitação de licenças por aplicativo *(fase V2)*
Consumo simultâneo em tempo real por app, teto configurável e fila de espera quando o teto é atingido.

*Por que é fosso:* resolve PR-03 e é o único mecanismo que transforma "acho que temos licença
suficiente" em número. Também é o instrumento que protege o próprio provedor do Caminho B de
inadimplência contratual com os fornecedores de software.

### DIF-03 · Orquestrador de atualizações dos sistemas hospedados *(fase V3)*
Drenagem de sessões, janela de manutenção, snapshot antes, rollback depois.

*Por que é fosso:* resolve PR-01 na raiz e é o que separa "servidor de terminal com um painel" de
"plataforma operada". É também o diferencial que o comprador PA-03 entende imediatamente, porque
ele já sofreu a atualização que deu errado às 8h da manhã.

### 4.1 Alerta de sequenciamento — o fosso chega tarde

Os três diferenciais estão previstos para V2 e V3. **MVP-0 e MVP-1 não contêm fosso algum**: um
sysadmin competente entrega RemoteApp + FSLogix + atalhos com RDS puro, sem AppBridge. Isso é
aceitável para dogfood (o objetivo lá é provar a fundação e resolver a dor do próprio escritório), mas
**é insuficiente para o piloto comercial do Caminho B** se o piloto for vendido como produto e não
como serviço operado. Recomendação: antecipar ao menos o **DIF-02 em versão mínima** (contagem e
teto, sem fila) para o piloto — decisão que exige ADR (RP-07). Registrado como R-004.

## 5. Não-objetivos

Declaração explícita do que o AppBridge **não é** e não pretende ser. Alterar qualquer item desta
lista exige ADR (RP-07).

| ID | Não-objetivo | Razão |
|----|--------------|-------|
| **NO-01** | **Não reimplementamos o protocolo RDP.** | Usamos a pilha RDS da Microsoft e o `mstsc` como cliente. Escrever cliente RDP é projeto de anos que não gera valor para o cliente. |
| **NO-02** | **Não entregamos desktop do servidor como produto.** | O "desktop confinado" (shell substituído pelo launcher) existe apenas como *fallback* para apps que se comportem mal como RemoteApp — é exceção documentada, nunca a oferta. |
| **NO-03** | **Não somos ferramenta de suporte/acesso remoto a estações** (TeamViewer, AnyDesk, RustDesk). | Publicamos aplicativos de servidor; não assumimos o controle da máquina de ninguém. |
| **NO-04** | **Não substituímos, revendemos nem emulamos os aplicativos hospedados.** | Domínio, Alterdata, Office e ERPs continuam sendo licenciados pelo cliente ou pelo provedor, sob os contratos dos respectivos fornecedores. |
| **NO-05** | **Não somos VDI (um desktop persistente por usuário).** | O modelo é sessão multiusuário em session host compartilhado. VDI/AVD é evolução possível de backend, não a proposta. |
| **NO-06** | **Não suportamos aplicativos Linux, macOS ou móveis nativos** nesta fase. | O problema atacado é especificamente o legado Windows desktop. |
| **NO-07** | **Não somos backup, antivírus, EDR nem firewall.** | Integramos com o que o cliente já tem; não competimos nessa camada. |
| **NO-08** | **Não expomos a porta 3389 à internet, em nenhuma circunstância, em nenhuma fase.** | RP-06. Não é escolha de produto — é restrição inegociável. |
| **NO-09** | **MVP-0 não tem painel administrativo, acesso externo pela internet, cofre de certificados, metering nem agent completo.** | Escopo de fase (Seção 4 do prompt mestre). Antecipação exige ADR. |
| **NO-10** | **Não oferecemos cliente web no MVP-0/MVP-1.** | Previsto para V2. O cliente é o launcher Windows nativo. |

## 6. Riscos de mercado

### 6.1 Concorrentes — plataformas genéricas de publicação de aplicativos sobre RDS

Categoria madura e povoada. Nomes recorrentes: **Parallels RAS**, **TSplus**, **Cameyo**,
**Inuvika OVD**, **Thinfinity**, **Awingu**, **Kasm**, além do próprio **Citrix** na faixa alta.
Todos entregam publicação de aplicativo remoto com painel — vários há mais de uma década.

| ID | Risco | Severidade | Mitigação prevista |
|----|-------|-----------|--------------------|
| **RM-01** | O núcleo funcional do AppBridge (publicar app, atalho, permissão) **já é commodity**; competir por funcionalidade genérica é competir em preço com produto maduro. | **Alta** | Não competir ali. Vender o fosso (DIF-01..03) e a especificidade brasileira/contábil. Ver R-004. |
| **RM-02** | Concorrente incumbente pode reduzir preço ou adicionar recurso equivalente mais rápido do que um projeto solo consegue construir. | Média | Nicho estreito e profundo. Cofre ICP-Brasil e metering por app contábil não estão no roadmap de produto global. |
| **RM-03** | O comprador PA-03 (provedor de TI) pode já ter Parallels/TSplus instalado e amortizado — custo de troca alto. | Média | Foco inicial em PA-01 (escritório), que compra serviço e não software, e onde não há incumbente instalado. |

### 6.2 Concorrentes — nuvem dos próprios fornecedores de software

Thomson Reuters (Domínio) e Alterdata vêm empurrando ofertas SaaS/nuvem próprias.
`PREMISSA:` a existência e o estágio comercial dessas ofertas precisam ser verificados diretamente com
os fornecedores — não estão confirmados neste documento (RP-05).

| ID | Risco | Severidade | Mitigação prevista |
|----|-------|-----------|--------------------|
| **RM-04** | **Risco existencial parcial:** se o fornecedor do sistema contábil central levar seu produto para a nuvem com boa experiência, some a principal razão de hospedá-lo. | **Alta** | O AppBridge nunca foi sobre um app só. O escritório continua com ERP de cliente, Excel pesado e legado — que nenhum fornecedor vai hospedar. Posicionar como camada do **parque restante**, complementar e não concorrente da nuvem do fornecedor. |
| **RM-05** | O mesmo fornecedor pode **vedar contratualmente** a execução multiusuário em servidor de terminal por terceiros, para proteger sua nuvem. | **Alta** | Verificação contratual por escrito antes do piloto (tarefa T-001, ver R-001). É verificação de viabilidade, não de conformidade — o resultado pode redefinir o Caminho B. |
| **RM-06** | Migração parcial de clientes para a nuvem do fornecedor reduz o número de usuários por escritório e corrói a receita por conta. | Média | Precificação por usuário ativo, não por escritório; expandir para PA-02 (ERPs), onde não há nuvem de fornecedor. |

### 6.3 Concorrentes — a plataforma da própria Microsoft

| ID | Risco | Severidade | Mitigação prevista |
|----|-------|-----------|--------------------|
| **RM-07** | **Azure Virtual Desktop** e **Windows 365** resolvem o mesmo problema com o peso da Microsoft, e a Microsoft investe em AVD enquanto o RDS clássico envelhece. | Média-alta | Restrição 2.5 já responde: backend RDS abstraído por interface, com AVD como implementação futura. AppBridge é a camada de governança **acima** do backend, não o backend. Além disso, AVD exige Azure e cobra em dólar — barreira real para o PA-01 brasileiro. |
| **RM-08** | Mudança de licenciamento da Microsoft (RDS CAL, SPLA/SAL) pode alterar a estrutura de custo do Caminho B unilateralmente. | Média | Cotação SPLA atualizada antes do piloto; contrato com cláusula de repasse; monitorar margem contra a meta de custo ≤ R$ 50/usuário/mês. |

### 6.4 Concorrentes — o hospedador nacional "sem marca"

Datacenters e provedores regionais que vendem "servidor em nuvem para contabilidade" — na prática,
um terminal server com desktop compartilhado, sem painel, sem metering, sem auditoria.

| ID | Risco | Severidade | Mitigação prevista |
|----|-------|-----------|--------------------|
| **RM-09** | **É o concorrente direto real do Caminho B**, com preço já formado no mercado (referência R$ 70–150/usuário/mês) e relacionamento local estabelecido. | **Alta** | Diferenciação por experiência (app local, não desktop) e por governança (auditoria, metering, cofre). O comprador precisa **ver** a diferença em demonstração — a Visão exige que o MVP-0 já pareça um produto, não um RDS pintado. |
| **RM-10** | Guerra de preço puxa a margem para baixo antes de o fosso estar pronto. | Média | Não perseguir o piso de preço. Ver §7, meta de custo. |

### 6.5 Riscos não-mercadológicos que atingem a viabilidade da Visão

| ID | Risco | Severidade |
|----|-------|-----------|
| **R-001** | Licença de Domínio/Alterdata pode vedar execução multiusuário em servidor de terminal — **invalida o Caminho B** se confirmado. | **Crítica** |
| **R-002** | Custódia centralizada de A1 tem exposição jurídica (ICP-Brasil, responsabilidade por uso indevido) que precede a técnica. Minuta sem parecer jurídico = DIF-01 não entra em produção. | **Alta** |
| **R-003** | SPLA/RDS SAL é custo fixo por usuário/mês; sem cotação atual não há como afirmar que a meta de ≤ R$ 50/usuário/mês é alcançável. | Alta |
| **R-004** | Fosso só chega em V2/V3; MVP-0 e MVP-1 não se distinguem de RDS bem configurado. | Alta |
| **R-005** | Acumular DC + RD Session Host na mesma máquina (P4) obriga a conceder logon local no controlador de domínio a usuários finais — desaconselhado pela Microsoft e desproporcionalmente arriscado. Proposta: separar em duas VMs no mesmo host (Windows Server Standard licencia 2 VMs). **Exige ADR.** | Alta |
| **R-006** | Projeto conduzido por uma pessoa, com quatro componentes (launcher, control plane, agent, admin) e prazo de MVP-0 em ~2 meses. Risco de execução é o mais provável de todos. | Alta |

## 7. Premissas declaradas (RP-05)

Todas replicadas em `STATUS.md`. Cada uma precisa de confirmação antes de virar requisito.

| ID | Premissa | Origem | Confirmar com |
|----|----------|--------|---------------|
| **PRE-01** | Dogfood: ~10 usuários simultâneos, 6–8 apps (Domínio Contábil/Folha/Escrita Fiscal, Alterdata, Office/Excel, 1–2 ERPs de clientes). | P1 | Frederico (inventário real) |
| **PRE-02** | Piloto: 60–100 simultâneos (3–5 escritórios × 15–20). Horizonte 12 meses: ~250 usuários, ~30 apps. RNFs dimensionados para **500**. | P1 | — (meta de projeto) |
| **PRE-03** | O escritório do dogfood está hoje em workgroup, sem domínio. Criar o AD DS é tarefa do MVP-0. | P2 | Frederico |
| **PRE-04** | Não existem hoje Windows Server 2025 nem RDS CALs no escritório; ambos serão adquiridos para o MVP-0. | P4 | Frederico / cotação |
| **PRE-05** | Custo total por usuário (infra + SPLA + Windows + suporte) ≤ **R$ 50/usuário/mês**, contra preço de venda de referência de R$ 70–150. | P8 | Cotação SPLA em revendedor |
| **PRE-06** | Ofertas de nuvem própria de Domínio/Thomson Reuters e Alterdata existem e avançam, mas seu estágio comercial **não está verificado**. | Análise de risco RM-04 | Fornecedores |

## 8. Decisões de contorno já fixadas por P1–P8

Registradas aqui para rastreabilidade; **cada uma exigirá seu ADR** antes de entrar em `ARQUITETURA.md`
(RP-07). Nenhuma foi ratificada ainda.

| Tema | Direção definida | ADR previsto |
|------|------------------|--------------|
| Identidade | AD DS como base (RDS clássico exige domain join) + Entra Connect para híbrido; Control Plane autentica via Entra/JWT e mapeia para contas AD | ADR-0001 |
| Topologia MVP-0 | 1 host Windows Server 2025, 8 vCPU, 32–64 GB — **com DC e session host separados em duas VMs** (ver R-005) | ADR-0002 |
| Acesso externo MVP-0 | Sem RD Gateway; rede interna + Tailscale para acesso remoto. RD Gateway + MFA na V2; relay tipo túnel reverso como candidato comercial | ADR-0003 |
| Isolamento multi-tenant | Híbrido: session host(s) + OU + GPO dedicados por escritório na camada RDS; `tenant_id` em toda tabela no Control Plane desde o MVP-0 | ADR-0004 |

## 9. Critérios de sucesso da Visão

| ID | Critério | Marco | Prazo (P8) |
|----|----------|-------|-----------|
| **CS-01** | O escritório do dogfood opera **um dia inteiro de trabalho real** com Domínio e Alterdata via AppBridge, sem usuário abrir `mstsc` manualmente. | MVP-0 | out/2026 |
| **CS-02** | Atualização do sistema contábil executada **uma vez no servidor** reflete para todos os usuários sem intervenção em estação. | MVP-0 | out/2026 |
| **CS-03** | Todo lançamento de aplicativo gera registro auditável de quem, o quê, quando, de onde. | MVP-0 | out/2026 |
| **CS-04** | Nenhuma porta RDP exposta à internet em nenhum momento, verificável por varredura externa. | MVP-0 | out/2026 |
| **CS-05** | 3–5 escritórios em piloto pagante, com isolamento por tenant e custo apurado dentro da meta PRE-05. | Piloto Caminho B | 1º tri/2027 |

## 10. Rastreabilidade

Este documento estabelece os IDs abaixo. `REQUISITOS.md` (entregável 2) deve derivar **cada RF/RNF de
pelo menos um destes** e nenhum requisito pode existir sem origem aqui (RA-04).

- **PR-01..PR-05** — dores do público-alvo
- **PA-01..PA-03** — segmentos-alvo
- **VP-01..VP-04** — ganhos da proposta de valor
- **DIF-01..DIF-03** — diferenciais estratégicos
- **NO-01..NO-10** — não-objetivos (delimitam o que *não* vira requisito)
- **RM-01..RM-10** — riscos de mercado
- **R-001..R-006** — riscos de viabilidade
- **PRE-01..PRE-06** — premissas abertas
- **CS-01..CS-05** — critérios de sucesso
