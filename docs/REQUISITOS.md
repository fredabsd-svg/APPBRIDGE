# REQUISITOS — AppBridge
> Entregável 2 de 7 da fase de Design · Sessão S001 · 2026-08-08
> Status: **✅ aprovado por Frederico em 2026-08-08** (RP-04)
> Depende de: `VISAO.md` (aprovado em 2026-08-08)
> Emendado em 2026-08-08 pelos ADR-0001 a ADR-0008 — ver §8

---

## 1. Como ler este documento

### 1.1 Identificação

- **RF-nnn** — requisito funcional. **RNF-nnn** — requisito não-funcional.
- Numeração **sequencial e permanente**: um ID nunca é reaproveitado. Requisito removido vira
  `[REMOVIDO — ver ADR-xxxx]`, não desaparece (RA-04).
- Toda linha tem **Origem**, apontando para IDs de `VISAO.md` (PR/PA/VP/DIF/CS), para regras do
  prompt mestre (RP/RA) ou para as respostas de descoberta (P1–P8). **Requisito sem origem não
  entra** (RA-04, RP-05).

### 1.2 Fase

`MVP-0` · `MVP-1` · `V2` · `V3`, conforme a Seção 4 do prompt mestre. Antecipar um requisito de fase
posterior exige ADR aprovado (regra de ouro do escopo).

### 1.3 MoSCoW

A prioridade é **relativa à fase indicada**, não ao produto inteiro:

| Grau | Significado |
|------|-------------|
| **M** (Must) | Sem ele a fase não é entregável. É critério de aceite da fase. |
| **S** (Should) | Importante; sai da fase só por decisão explícita registrada. |
| **C** (Could) | Entra se sobrar espaço na fase, sem renegociar prazo. |
| **W** (Won't) | Declarado fora de escopo — ver §5. |

### 1.4 Marcação de premissa

Valores numéricos que **não foram informados** por Frederico aparecem marcados `PREMISSA:` e estão
consolidados em §6 e em `STATUS.md`. Nenhum foi assumido silenciosamente (RP-05).

---

## 2. Requisitos funcionais

### 2.1 Identidade, autenticação e autorização

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RF-001** | O launcher autentica o usuário contra o provedor de identidade e obtém um token do Control Plane. Nenhuma função além da tela de login é acessível sem autenticação. | MVP-0 | **M** | VP-03, PA-01 |
| **RF-002** | O Control Plane suporta **AD DS on-premises como diretório-base** e mantém o vínculo entre a identidade autenticada e a conta AD que abrirá a sessão RDS (**ADR-0001**). | MVP-0 | **M** | P2, **ADR-0001** |
| **RF-003** | O Control Plane suporta identidade híbrida via Entra ID (autenticação OIDC, com Entra Connect Sync espelhando o AD DS e mapeamento para a conta AD). No MVP-0 admite-se autenticar direto contra o AD DS enquanto o tenant Entra não estiver pronto, sem que isso altere o vínculo de RF-002 (**ADR-0001**). | MVP-0 | **M** | P2, **ADR-0001** |
| **RF-004** | O token de sessão emitido pelo Control Plane tem expiração curta e é renovável sem novo login interativo enquanto a sessão do usuário for válida. | MVP-0 | **M** | RP-06 |
| **RF-005** | Credenciais e tokens persistidos no cliente ficam no **Windows Credential Manager**; nunca em arquivo de configuração, banco local em claro ou log. | MVP-0 | **M** | RP-06, stack §3 |
| **RF-006** | O usuário pode encerrar a sessão no launcher (logout), o que invalida o token local e exige novo login. | MVP-0 | **M** | VP-03 |
| **RF-007** | **Revogação de acesso:** desabilitar o usuário no diretório ou remover sua permissão impede qualquer novo lançamento de aplicativo dentro do prazo de propagação definido em RNF-030. | MVP-0 | **M** | VP-03, PR-05 |
| **RF-008** | A revogação de acesso **encerra também as sessões ativas** do usuário nos session hosts. | MVP-1 | **M** | VP-03 |
| **RF-009** | Acesso a partir da internet exige **MFA no RD Gateway**. | V2 | **M** | RP-06, P5 |
| **RF-010** | Permissão de aplicativo é atribuída **por grupo** (grupo de diretório ou grupo do Control Plane), não usuário a usuário. | MVP-0 | **M** | PA-01, VP-03 |

### 2.2 Catálogo de aplicativos

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RF-011** | Endpoint do Control Plane retorna o **catálogo de aplicativos autorizados ao usuário autenticado** — e somente eles. Aplicativo não autorizado não aparece nem é referenciável. | MVP-0 | **M** | VP-03, §4 prompt |
| **RF-012** | O catálogo do MVP-0 é populado por **seed** (JSON ou tabela), sem painel administrativo. | MVP-0 | **M** | §4 prompt (MVP-0) |
| **RF-013** | Cada aplicativo do catálogo tem, no mínimo: identificador estável, nome de exibição, ícone, descrição, alias do RemoteApp, host/pool de destino e grupo de permissão. | MVP-0 | **M** | VP-02 |
| **RF-014** | O launcher mantém **cache local do catálogo** (SQLite) para exibir a interface sem esperar a rede; o cache nunca substitui a autorização server-side de RF-021. | MVP-0 | **M** | VP-02, stack §3 |
| **RF-015** | O launcher sincroniza o catálogo na abertura e periodicamente enquanto estiver em execução. | MVP-0 | **S** | VP-01 |
| **RF-016** | O usuário pode buscar/filtrar aplicativos no catálogo. | MVP-0 | **C** | PA-01 |
| **RF-017** | O usuário pode marcar aplicativos como favoritos, que aparecem em destaque. | MVP-1 | **S** | §4 prompt (MVP-1) |

### 2.3 Lançamento de aplicativo

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RF-018** | O Control Plane **gera dinamicamente** um arquivo `.rdp` por lançamento, específico do par usuário × aplicativo. Não existem arquivos `.rdp` estáticos distribuídos. | MVP-0 | **M** | §2.2 prompt |
| **RF-019** | Todo `.rdp` entregue é **assinado** (`rdpsign`) antes da entrega ao cliente. | MVP-0 | **M** | RP-06, §2.4 prompt |
| **RF-020** | O `.rdp` é **temporário**: gravado em área do perfil do usuário, com validade curta, e removido após o lançamento ou ao expirar. `PREMISSA:` validade de 60 s a partir da emissão. | MVP-0 | **M** | RP-06 |
| **RF-021** | A **autorização é verificada no servidor a cada pedido de lançamento**. O Control Plane nunca confia em identificador de aplicativo, host ou permissão vindos do cliente. | MVP-0 | **M** | RP-06, VP-03 |
| **RF-022** | O launcher abre o `.rdp` via **`mstsc`**. O AppBridge não implementa cliente RDP próprio. | MVP-0 | **M** | NO-01 |
| **RF-023** | **Prelaunch:** o launcher estabelece a sessão RDS antecipadamente, de modo que o primeiro aplicativo do dia abra sem o custo de criação de sessão. | MVP-0 | **M** | §2.4 prompt, VP-02 |
| **RF-024** | Lançar um segundo aplicativo quando já existe sessão ativa **reutiliza a sessão**, sem criar sessão adicional nem pedir credencial de novo. | MVP-0 | **M** | VP-02 |
| **RF-025** | Falhas de lançamento produzem **mensagem acionável em português** distinguindo, no mínimo: sem permissão, host indisponível, sessão expirada e erro interno. | MVP-0 | **M** | PA-01, VP-02 |
| **RF-026** | O launcher exibe **indicador de latência** da conexão com o host. | MVP-0 | **S** | §2.1 prompt |
| **RF-027** | O launcher **reconecta automaticamente** após queda de rede, retomando a sessão existente em vez de criar nova. | MVP-1 | **M** | §4 prompt (MVP-1) |
| **RF-028** | Existe modo de **fallback "desktop confinado"** — sessão com o shell substituído pelo launcher — habilitável por aplicativo, para aplicativos que não se comportem corretamente como RemoteApp. É exceção documentada por aplicativo, nunca o modo padrão. | MVP-1 | **S** | §2.4 prompt, NO-02 |

### 2.4 Integração com o desktop do usuário

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RF-029** | O launcher registra o protocolo **`appbridge://launch/<app>`** no Windows e trata a invocação abrindo o aplicativo correspondente. | MVP-0 | **M** | §2.1 prompt |
| **RF-030** | Função **"Instalar meus aplicativos"**: cria atalhos no **Desktop** e no **Menu Iniciar** para todos os aplicativos autorizados ao usuário. | MVP-0 | **M** | VP-02, §2.1 prompt |
| **RF-031** | Os atalhos usam o **ícone do aplicativo** e apontam para o protocolo `appbridge://`, de modo que o usuário abra o aplicativo sem passar pela janela do launcher. | MVP-0 | **M** | VP-02 |
| **RF-032** | Na sincronização do catálogo, atalhos de aplicativos **cujo acesso foi revogado são removidos** do Desktop e do Menu Iniciar. | MVP-0 | **S** | VP-03, RF-007 |
| **RF-033** | O launcher pode iniciar com o Windows e permanecer na área de notificação, para viabilizar o prelaunch e a sincronização. | MVP-0 | **S** | RF-023 |
| **RF-034** | O launcher é distribuído como **MSIX**. | MVP-0 | **S** | stack §3 |
| **RF-035** | O launcher se **atualiza automaticamente**, sem intervenção do usuário nem do administrador em cada estação. | MVP-1 | **M** | §4 prompt (MVP-1), VP-01 |

### 2.5 Auditoria — eventos registrados

> Os requisitos de **retenção, imutabilidade e conteúdo mínimo** da trilha estão em §3.2 (RNF).
> Aqui ficam os eventos que o produto é obrigado a gerar.

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RF-036** | Registrar toda **tentativa de autenticação**, com resultado (sucesso/falha) e motivo da falha. | MVP-0 | **M** | RA-07, PR-05 |
| **RF-037** | Registrar todo **lançamento de aplicativo**: quem, qual aplicativo, quando, de onde (endereço de rede e identificação da estação) e resultado. | MVP-0 | **M** | RA-07, VP-04, CS-03 |
| **RF-038** | Registrar **início e fim de sessão** RDS, com duração e host que atendeu. | MVP-0 | **M** | RA-07, VP-04 |
| **RF-039** | Registrar toda **negativa de autorização** (tentativa de lançar aplicativo sem permissão). | MVP-0 | **M** | RA-07, PR-05 |
| **RF-040** | Expor endpoint **somente-leitura** de consulta da trilha, para uso administrativo antes de existir painel. | MVP-0 | **S** | CS-03 |
| **RF-041** | Registrar toda **ação administrativa** — publicar, alterar, despublicar aplicativo; conceder e revogar permissão; criar, alterar e desativar usuário/grupo — com autor, momento e valores antes/depois. | MVP-1 | **M** | RA-07 |
| **RF-042** | Registrar **cada uso de certificado digital** do cofre: quem assinou, por qual titular, com qual certificado, em qual aplicativo, quando. | V2 | **M** | RA-07, DIF-01 |

### 2.6 Painel administrativo

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RF-043** | Publicar, alterar e despublicar aplicativos pelo painel, substituindo o seed de RF-012. | MVP-1 | **M** | §4 prompt (MVP-1), VP-01 |
| **RF-044** | Gerenciar usuários e grupos e atribuir permissões de aplicativo por grupo. | MVP-1 | **M** | §4 prompt (MVP-1), VP-03 |
| **RF-045** | Listar **sessões ativas** (usuário, aplicativo, host, início, duração) e encerrar sessão à força. | MVP-1 | **S** | PR-05, RF-008 |
| **RF-046** | Consultar a trilha de auditoria com filtro por usuário, aplicativo, tipo de evento e período. | MVP-1 | **M** | RA-07, VP-04 |
| **RF-047** | Listar **servidores/hosts** e seu estado (online, sessões, recursos). | V2 | **M** | §2.1 prompt |
| **RF-048** | Definir **políticas por aplicativo/tenant** de redirecionamento de recursos (impressora, área de transferência, unidades locais, portas USB). | V2 | **S** | §2.1 prompt, RP-06 |
| **RF-049** | **Branding por cliente** no launcher e no painel. | V3 | **S** | §4 prompt (V3) |

### 2.7 Agent nos hosts RDS

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RF-050** | O Agent estabelece conexão **reversa outbound** (WebSocket) com o Control Plane. Nenhuma porta de entrada é aberta no session host para o Control Plane alcançá-lo. | V2 | **M** | §2.1 prompt, RP-06 |
| **RF-051** | O Agent envia **telemetria periódica**: CPU, memória, disco e número de sessões. | V2 | **M** | §2.1 prompt |
| **RF-052** | O Agent expõe **canal de comando** para: publicar aplicativo, coletar logs e executar ações de manutenção. | V2 | **M** | §2.1 prompt |
| **RF-053** | O Agent tem **credencial própria por host**, emitida no enrollment e revogável individualmente. | V2 | **M** | RP-06 |
| **RF-054** | O Control Plane detecta host offline por ausência de heartbeat e deixa de rotear lançamentos para ele. | V2 | **M** | §2.1 prompt |

### 2.8 DIF-01 · Cofre de certificados digitais

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RF-055** | Armazenar certificados **A1 (PFX) cifrados em repouso**, com a senha de importação guardada em local separado do arquivo e igualmente protegida. | V2 | **M** | DIF-01, P6 |
| **RF-056** | Cada certificado no cofre é vinculado ao registro do **termo de autorização de custódia e uso** assinado pelo titular, com finalidades, usuários autorizados, vigência e condições de revogação. | V2 | **M** | DIF-01, P6, LGPD art. 7º V |
| **RF-057** | **Política de uso:** definir qual usuário pode assinar por qual titular, com qual certificado, em qual aplicativo e durante qual vigência. | V2 | **M** | DIF-01 |
| **RF-058** | O certificado é **injetado na sessão conforme a política**, sem que o arquivo PFX nem sua senha fiquem acessíveis ao usuário final. | V2 | **M** | DIF-01, RP-06 |
| **RF-059** | **Revogação imediata:** remover um certificado ou encerrar sua vigência impede novo uso e é refletido nas sessões ativas. | V2 | **M** | DIF-01, PR-02 |
| **RF-060** | **A3/token USB** é suportado como cidadão de primeira classe no redirecionamento para a sessão, com política de quem pode redirecionar. | V2 | **M** | DIF-01, §2.3 prompt |
| **RF-061** | Alertar sobre certificados com vencimento próximo. | V2 | **C** | PR-02 |

### 2.9 DIF-02 · Medição e limitação de licenças

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RF-062** | Contar, **em tempo real**, o uso simultâneo por aplicativo e por tenant. Exige detecção confiável de fim de sessão, obtida por consulta periódica ao Connection Broker enquanto não houver Agent, atrás da interface de RNF-035. | **MVP-1** | **M** | DIF-02, PR-03, **ADR-0006** |
| **RF-063** | Definir **teto de uso simultâneo** por aplicativo e por tenant. | **MVP-1** | **M** | DIF-02, PR-03, **ADR-0006** |
| **RF-064** | **Bloquear o lançamento** quando o teto for atingido, com mensagem clara ao usuário indicando a causa. O administrador consegue reconciliar ou zerar o contador. | **MVP-1** | **M** | DIF-02, **ADR-0006** |
| **RF-065** | **Fila de espera:** o usuário bloqueado entra em fila e é notificado quando houver vaga. | V2 | **S** | DIF-02, §2.3 prompt |
| **RF-066** | Relatório histórico de uso e de **pico simultâneo** por aplicativo, para dimensionar compra de licença. | V2 | **S** | DIF-02, PR-03 |

> **Sequenciamento (R-004) — resolvido por ADR-0006:** RF-062, RF-063 e RF-064 foram **antecipados
> de V2 para MVP-1**, para que o piloto do Caminho B tenha diferencial verificável. RF-065 (fila de
> espera) e RF-066 (relatório histórico) permanecem em V2.

### 2.10 DIF-03 · Orquestrador de atualizações

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RF-067** | Agendar **janela de manutenção** por host e por tenant. | V3 | **M** | DIF-03, PR-01 |
| **RF-068** | **Drenagem de sessões:** impedir novos lançamentos no host, avisar usuários conectados e aguardar o encerramento (com prazo e encerramento forçado ao fim). | V3 | **M** | DIF-03 |
| **RF-069** | **Snapshot antes** da atualização, com registro do ponto de restauração. | V3 | **M** | DIF-03 |
| **RF-070** | Executar a atualização do sistema hospedado via Agent, com registro de saída e código de retorno. | V3 | **M** | DIF-03, RF-052 |
| **RF-071** | **Rollback** para o snapshot em caso de falha, manual ou automático por critério configurado. | V3 | **M** | DIF-03, PR-01 |
| **RF-072** | Registrar o resultado completo da orquestração na trilha administrativa. | V3 | **M** | DIF-03, RA-07 |

### 2.11 Multi-tenant

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RF-073** | **Toda entidade do Control Plane pertence a um tenant.** Nenhuma consulta de dados de tenant retorna registro de outro tenant, em nenhuma circunstância. | MVP-0 | **M** | §2.5 prompt, P7 |
| **RF-074** | O usuário é roteado para o(s) **session host(s) do seu tenant**; nunca para host de outro tenant. | MVP-0 | **M** | P7 |
| **RF-075** | A administração é **escopada ao tenant**. O papel de operador do provedor, capaz de atravessar tenants, é distinto, nominal e tem toda ação registrada na trilha administrativa. | MVP-1 | **M** | P7, RA-07 |
| **RF-076** | Cobrança e licenciamento comercial por tenant. | V3 | **M** | §4 prompt (V3) |

---

## 3. Requisitos não-funcionais

### 3.1 Segurança (RP-06 — inegociável)

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RNF-001** | **A porta 3389 nunca é exposta à internet**, em nenhuma fase, em nenhum ambiente. Verificável por varredura externa. | todas | **M** | RP-06, NO-08, CS-04 |
| **RNF-002** | Nenhum arquivo `.rdp` não assinado é entregue ao cliente. A política do cliente exige assinatura válida para abrir. | todas | **M** | RP-06, RF-019 |
| **RNF-003** | Todo tráfego é cifrado em trânsito com **TLS 1.2 ou superior** — launcher↔Control Plane, Agent↔Control Plane, painel↔Control Plane. | todas | **M** | RP-06 |
| **RNF-004** | **Segredos jamais aparecem** em código-fonte, documentação, mensagem de commit, log ou telemetria. Origem: variáveis de ambiente ou cofre. | todas | **M** | RP-06 |
| **RNF-005** | **Menor privilégio:** contas de serviço do Control Plane e do Agent não são administradoras de domínio; usuários finais não são administradores nos session hosts. | todas | **M** | RP-06 |
| **RNF-006** | Session hosts operam com **AppLocker ou WDAC em modo allowlist**: só executa o que foi explicitamente publicado. | MVP-0 | **M** | RP-06, §2.4 prompt |
| **RNF-007** | **Usuários finais não recebem direito de logon local em controlador de domínio.** Session host e controlador de domínio não coabitam a mesma instância de sistema operacional (topologia em **ADR-0002**). | MVP-0 | **M** | R-005, RP-06, **ADR-0002** |
| **RNF-008** | A **chave privada do certificado de assinatura RDP** é protegida contra exportação, com procedimento de rotação e de resposta a comprometimento documentado. | MVP-0 | **M** | RP-06, RF-019 |
| **RNF-009** | No MVP-0 **não há exposição à internet**: acesso apenas por rede interna ou pela rede privada em malha, com ACL que restringe o alcance ao 3389 e aprovação nominal de dispositivo (**ADR-0003**). Verificação por varredura externa é obrigatória (CS-04). RD Gateway + MFA entram na V2. | MVP-0 | **M** | P5, **ADR-0003** |
| **RNF-010** | Endpoints de autenticação e de lançamento têm **limitação de taxa** e bloqueio progressivo contra tentativa de força bruta e enumeração. | MVP-0 | **S** | RP-06 |
| **RNF-011** | **FSLogix** (profile containers + App Masking) é a base de perfil dos session hosts; o perfil do usuário não persiste em disco local do host. | MVP-0 | **M** | §2.4 prompt |
| **RNF-012** | Dados em repouso do Control Plane que contenham dado pessoal ou material sensível são cifrados. | MVP-0 | **S** | RP-06, LGPD |
| **RNF-013** | O material do cofre de certificados é cifrado com chave gerenciada fora do banco de dados; comprometer o banco não basta para usar um certificado. | V2 | **M** | DIF-01, R-002 |
| **RNF-014** | Redirecionamento é **negado por padrão** e liberado apenas por exceção declarada. A **política base do MVP-0**, aplicada por GPO na OU do tenant, está definida em **ADR-0008**: permitidos impressora local, token/smart card USB, área de transferência bidirecional e saída de áudio; negados unidades locais, portas COM/LPT, entrada de áudio e demais dispositivos USB. Toda exceção é nominal e documentada. | **MVP-0** | **M** | RP-06, RF-048, **ADR-0008** |

### 3.2 Auditoria (RA-07 — obrigatório)

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RNF-015** | **Log de acesso:** todo evento de acesso registra, no mínimo, **quem** (identidade), **o quê** (recurso/aplicativo), **quando** (timestamp) e **de onde** (endereço de rede e identificação da estação). | MVP-0 | **M** | RA-07 |
| **RNF-016** | **Trilha de uso de certificado digital:** cada uso é registrado individualmente e o registro é suficiente para responder "quem assinou o quê, por qual titular, quando". | V2 | **M** | RA-07, DIF-01 |
| **RNF-017** | **Trilha administrativa:** quem publicou, permissionou, revogou ou alterou configuração, com valores antes/depois. | MVP-1 | **M** | RA-07 |
| **RNF-018** | **Retenção configurável por tenant**, com expurgo automatizado e o próprio expurgo registrado. Prazos definidos em **ADR-0007**: log de acesso e lançamento — padrão 12 meses (mín. 6, máx. 60); trilha administrativa — padrão 24 meses (mín. 12, máx. 60); trilha de uso de certificado — padrão e mínimo 60 meses. O tenant não pode configurar abaixo do mínimo. | MVP-0 | **M** | RA-07, LGPD, **ADR-0007** |
| **RNF-019** | Registros de auditoria são **append-only**: a aplicação não oferece caminho para alterar ou apagar evento individual; expurgo só ocorre por política de retenção e é ele próprio registrado. | MVP-0 | **M** | RA-07 |
| **RNF-020** | Todos os timestamps são gravados em **UTC**, com relógio dos hosts sincronizado por NTP. Divergência de relógio é condição de alerta. | MVP-0 | **M** | RA-07 |
| **RNF-021** | A trilha é **exportável em formato legível por máquina** (CSV/JSON), para entrega em auditoria. | MVP-1 | **S** | RA-07, VP-04 |
| **RNF-022** | **A auditoria de evento de segurança é bloqueante** (ADR-0007): o registro é gravado na mesma transação que concede o acesso; se não puder ser gravado, o acesso **não é concedido**. Vale para RF-036, RF-037, RF-039, RF-041 e RF-042. Não vale para telemetria, diagnóstico e fim de sessão (RF-038). A falha gera alerta, é registrada no log estruturado e **não afeta sessões já abertas** (RNF-032). | MVP-0 | **M** | RA-07, **ADR-0007** |

### 3.3 Privacidade e LGPD

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RNF-023** | **Minimização:** coletar apenas o dado pessoal necessário à operação e à trilha. Nada de conteúdo de tela, teclas digitadas ou arquivos do usuário. | todas | **M** | RP-06, LGPD |
| **RNF-024** | O acesso administrativo a dado pessoal é ele próprio registrado (quem consultou a trilha de quem). | MVP-1 | **M** | RP-06, RA-07 |
| **RNF-025** | Finalidade e base legal de cada categoria de dado tratado ficam documentadas; para o cofre de certificados, a base é a execução do contrato de custódia (RF-056). | V2 | **M** | P6, LGPD art. 7º V |

### 3.4 Desempenho e capacidade

> Dimensionamento definido em P1: RNFs projetados para **500 usuários**, ainda que a meta de 12 meses
> seja ~250 usuários e ~30 aplicativos.

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RNF-026** | O Control Plane é dimensionado para **500 usuários simultâneos** e `PREMISSA:` 100 aplicativos publicados, sem redesenho de arquitetura. | MVP-0 | **M** | P1, PRE-02 |
| **RNF-027** | Do clique no atalho até a janela do aplicativo visível: `PREMISSA:` **≤ 5 s com prelaunch ativo** e ≤ 20 s sem prelaunch, em rede local. | MVP-0 | **M** | VP-02, RF-023 |
| **RNF-028** | Endpoint de catálogo responde em `PREMISSA:` **p95 ≤ 300 ms** na carga de RNF-026. | MVP-0 | **S** | VP-02 |
| **RNF-029** | Geração e assinatura do `.rdp` completam em `PREMISSA:` **p95 ≤ 1 s**. | MVP-0 | **S** | RF-018, RF-019 |
| **RNF-030** | **Propagação da revogação:** a partir da revogação, nenhum novo lançamento é autorizado em até `PREMISSA:` **60 s**. | MVP-0 | **M** | RF-007, VP-03 |
| **RNF-031** | A telemetria do Agent consome `PREMISSA:` **≤ 2% de CPU** do session host em regime permanente. | V2 | **S** | RF-051 |

### 3.5 Disponibilidade e continuidade

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RNF-032** | **Indisponibilidade do Control Plane não derruba sessões em andamento.** O usuário conectado continua trabalhando; apenas novos lançamentos ficam indisponíveis. | MVP-0 | **M** | PA-01, CS-01 |
| **RNF-033** | **Backup diário** do banco do Control Plane, com procedimento de restauração testado antes do piloto. | MVP-0 | **M** | R-006, CS-05 |
| **RNF-034** | Disponibilidade-alvo do Control Plane no piloto: `PREMISSA:` **99,5% mensal**, medida em horário comercial. | Piloto/V2 | **S** | CS-05 |

### 3.6 Arquitetura, portabilidade e evolução

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RNF-035** | O **backend de sessão é abstraído por interface**. Trocar RDS por AVD é implementar a interface, não reescrever o Control Plane. Nenhuma regra de negócio depende de detalhe do RDS. | MVP-0 | **M** | §2.5 prompt, RM-07 |
| **RNF-036** | **`tenant_id` em toda tabela desde o MVP-0**, com o filtro aplicado por **filtro global de consulta no `DbContext`**, alimentado pelo contexto de tenant resolvido do token — nunca por parâmetro do chamador. Travessia deliberada de tenant é explícita e registrada. A suíte de testes contém casos que tentam violar o isolamento e exigem falha (**ADR-0004**). | MVP-0 | **M** | §2.5 prompt, P7, RF-073, **ADR-0004** |
| **RNF-037** | O MVP-0 roda **inteiramente on-premises**, sem dependência de serviço de nuvem específico para funcionar. | MVP-0 | **M** | P4, PA-01 |
| **RNF-038** | Isolamento de tenant na camada RDS é **por session host, OU e GPO dedicados** por escritório; o domínio único do provedor com OU por cliente atende o piloto. | Piloto/V2 | **M** | P7 |

### 3.7 Observabilidade e operação

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RNF-039** | Log estruturado com **identificador de correlação ponta a ponta** (launcher → Control Plane → host), para reconstruir um lançamento inteiro. | MVP-0 | **M** | R-006, PR-05 |
| **RNF-040** | O Control Plane expõe **health check** e métricas operacionais básicas. | MVP-0 | **S** | RNF-034 |
| **RNF-041** | O launcher consegue **coletar e enviar diagnóstico** de um problema sem exigir que o usuário navegue em pastas ou no Visualizador de Eventos. | MVP-1 | **S** | PA-01 |

### 3.8 Usabilidade

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RNF-042** | **O usuário final não precisa saber que o aplicativo é remoto.** Nenhum fluxo normal exige digitar nome de servidor, abrir `mstsc` ou lidar com arquivo `.rdp`. | MVP-0 | **M** | VP-02, CS-01 |
| **RNF-043** | Toda mensagem de interface e de erro é em **português do Brasil**, acionável, e não expõe detalhe técnico interno (host, caminho, stack trace) ao usuário final. | MVP-0 | **M** | RP-01, RP-06 |
| **RNF-044** | A instalação do launcher na estação `PREMISSA:` **não exige privilégio de administrador** (MSIX por usuário) — a confirmar contra o registro do protocolo `appbridge://` e a criação de atalhos. | MVP-0 | **S** | RF-029, RF-030 |

### 3.9 Compatibilidade

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RNF-045** | Estações suportadas: `PREMISSA:` **Windows 10 22H2 e Windows 11**, 64 bits. | MVP-0 | **M** | PA-01 |
| **RNF-046** | Servidores: **Windows Server 2025** com a pilha RDS (Session Host, Connection Broker, Licensing; Web Access e Gateway conforme fase). | MVP-0 | **M** | §2.2 prompt, P4 |
| **RNF-047** | Cliente web. | V2 | **S** | §4 prompt (V2), NO-10 |

### 3.10 Qualidade e manutenibilidade (RP-03, RP-08)

| ID | Requisito | Fase | MoSCoW | Origem |
|----|-----------|------|--------|--------|
| **RNF-048** | **Cobertura de testes ≥ 80%** no Control Plane e no Agent, aferida em cada build da fase de implementação. | implementação | **M** | RP-08 |
| **RNF-049** | Toda lógica de negócio nasce com **plano de teste** escrito antes ou junto do código. | implementação | **M** | RP-08 |
| **RNF-050** | A **definição de pronto inclui documentação atualizada** — requisito, ADR e changelog. | todas | **M** | RP-08, RA-02 |
| **RNF-051** | **SemVer independente por componente** (launcher, control-plane, agent, admin). | todas | **M** | RP-03 |
| **RNF-052** | Migrações de banco são **versionadas, aplicadas automaticamente e reversíveis**. | MVP-0 | **M** | RP-08 |
| **RNF-053** | Nenhum requisito, entidade, endpoint ou componente existe sem **rastreabilidade** a um RF/RNF e, quando aplicável, a um ADR. | todas | **M** | RA-04 |

---

## 4. Rastreabilidade — Visão → Requisitos

Cobertura de cada item de `VISAO.md`. **Nenhuma dor, ganho ou diferencial ficou sem requisito.**

| Origem | Descrição | Requisitos derivados |
|--------|-----------|----------------------|
| **PR-01** | Atualização do sistema contábil | RF-035, RF-043, RF-067..RF-072, VP-01 |
| **PR-02** | Certificado digital como gargalo | RF-055..RF-061, RNF-016 |
| **PR-03** | Licença sem visibilidade | RF-062..RF-066 |
| **PR-04** | Home office e multiescritório | RF-009, RNF-001, RNF-009 |
| **PR-05** | Ausência de trilha | RF-036..RF-042, RNF-015..RNF-022 |
| **VP-01** | Instale uma vez | RF-012, RF-043, RF-035, RF-067..072 |
| **VP-02** | Parece local | RF-022..RF-034, RNF-027, RNF-042 |
| **VP-03** | Controle central | RF-007, RF-008, RF-010, RF-021, RF-044, RNF-030 |
| **VP-04** | Rastro | RF-036..RF-042, RNF-015..RNF-021 |
| **DIF-01** | Cofre de certificados | RF-055..RF-061, RNF-013, RNF-016, RNF-025 |
| **DIF-02** | Metering de licenças | RF-062..RF-066 |
| **DIF-03** | Orquestrador de atualizações | RF-067..RF-072 |
| **CS-01** | Dia de trabalho real no dogfood | RF-018..RF-034, RNF-027, RNF-042 |
| **CS-02** | Atualização única no servidor | RF-012, RF-043 |
| **CS-03** | Registro auditável de todo lançamento | RF-037, RF-040, RNF-015 |
| **CS-04** | Nenhuma porta RDP exposta | RNF-001, RNF-009 |
| **CS-05** | Piloto com isolamento e custo apurado | RF-073..RF-076, RNF-036, RNF-038, RNF-033 |
| **R-005** | DC + session host juntos | RNF-007 |
| **RM-07** | Concorrência AVD/Windows 365 | RNF-035 |

---

## 5. Fora de escopo (Won't)

Derivado dos não-objetivos de `VISAO.md`. Trazer qualquer item para dentro exige ADR (RP-07).

| ID | Item | MoSCoW | Origem |
|----|------|--------|--------|
| **RF-077** | Implementação de cliente ou servidor RDP próprio | **W** | NO-01 |
| **RF-078** | Entrega do desktop completo do servidor como oferta de produto | **W** | NO-02 |
| **RF-079** | Controle remoto de estações de trabalho (suporte tipo AnyDesk/TeamViewer) | **W** | NO-03 |
| **RF-080** | Revenda, substituição ou emulação dos aplicativos hospedados | **W** | NO-04 |
| **RF-081** | VDI com desktop persistente por usuário | **W** | NO-05 |
| **RF-082** | Publicação de aplicativos Linux, macOS ou móveis nativos | **W** | NO-06 |
| **RF-083** | Backup, antivírus, EDR ou firewall próprios | **W** | NO-07 |
| **RF-084** | Qualquer exposição da porta 3389 à internet | **W** | NO-08, RNF-001 |
| **RF-085** | Painel administrativo, acesso externo, cofre, metering ou Agent completo **no MVP-0** | **W** | NO-09 |
| **RF-086** | Cliente web no MVP-0 e MVP-1 | **W** | NO-10, RNF-047 |

---

## 6. Premissas introduzidas por este documento (RP-05)

Valores não informados por Frederico, assumidos explicitamente. **Cada um precisa de confirmação
antes da implementação**; todos estão replicados em `STATUS.md`.

| ID | Premissa | Onde | Impacto se errada |
|----|----------|------|-------------------|
| **PRE-07** | Validade do arquivo `.rdp` temporário: 60 s | RF-020 | Muito curto quebra lançamento em máquina lenta; muito longo amplia janela de reuso indevido |
| ~~**PRE-08**~~ | ~~Retenção de logs: mínimo 6 meses, padrão 12~~ | RNF-018 | **Resolvida por ADR-0007** — tabela de prazos por categoria de trilha |
| ~~**PRE-09**~~ | ~~Falha de auditoria alerta mas não bloqueia~~ | RNF-022 | **Resolvida por ADR-0007** — auditoria de evento de segurança é **bloqueante** |
| **PRE-10** | Capacidade de 100 aplicativos publicados no dimensionamento para 500 usuários | RNF-026 | Dimensionamento de catálogo e de sincronização |
| **PRE-11** | Abertura ≤ 5 s com prelaunch, ≤ 20 s sem, em rede local | RNF-027 | É o número que define percepção de "parece local" — merece medição real no dogfood |
| **PRE-12** | Catálogo p95 ≤ 300 ms; geração+assinatura do `.rdp` p95 ≤ 1 s | RNF-028, RNF-029 | Metas de engenharia; `rdpsign` pode não caber em 1 s sob carga |
| **PRE-13** | Propagação da revogação em até 60 s | RNF-030 | Define se a autorização pode ser cacheada e por quanto tempo |
| **PRE-14** | Telemetria do Agent ≤ 2% de CPU | RNF-031 | Frequência de coleta |
| **PRE-15** | Disponibilidade-alvo de 99,5% mensal no piloto | RNF-034 | Vira cláusula contratual no Caminho B |
| **PRE-16** | Estações Windows 10 22H2 e Windows 11, 64 bits | RNF-045 | Windows 10 saiu do suporte padrão em out/2025 — confirmar o parque real do escritório |
| **PRE-17** | Instalação do launcher sem privilégio de administrador | RNF-044 | Registro de protocolo e criação de atalhos podem exigir elevação; muda o plano de implantação |
| **PRE-18** | O host físico do MVP-0 suporta Hyper-V com virtualização assistida por hardware | ADR-0002 | Sem isso, a separação DC × session host exige segunda máquina |
| **PRE-19** | Prazos de 24 e 60 meses de retenção são escolha de engenharia, não parecer jurídico | ADR-0007 | A trilha de certificado é evidência potencial em disputa — confirmar em T-002 |
| **PRE-20** | Os tokens A3 do escritório funcionam redirecionados para a sessão RDS | ADR-0008 | Histórico de instabilidade dependente de driver; testar no dogfood antes de virar promessa comercial |

---

## 7. Questões abertas — situação

As cinco questões levantadas na versão original foram decididas por delegação de Frederico
("você decide", 2026-08-08), cada uma com seu ADR. Restam duas que dependem de fato do mundo, não de
decisão de projeto.

| # | Questão | Situação |
|---|---------|----------|
| 1 | Auditoria bloqueante? | ✅ **Decidida — ADR-0007.** Sim, bloqueante para eventos de segurança. O argumento decisivo: a autorização já depende do mesmo banco, então o custo em disponibilidade é quase nulo. |
| 2 | Prazo de retenção | ✅ **Decidida — ADR-0007.** Três categorias com prazos distintos; configurável por tenant, com mínimos que o tenant não pode furar. |
| 3 | Parque em Windows 10 | ⏳ **Aberta — depende de levantamento.** É fato sobre o escritório, não decisão de arquitetura. RNF-045 mantém Windows 10 22H2 e Windows 11 como suportados; risco R-008 registrado. |
| 4 | Aplicativo que exija desktop confinado | ⏳ **Aberta — depende de T-001.** RF-028 permanece em MVP-1 (Should). **Gatilho:** se o inventário revelar aplicativo que não funcione como RemoteApp, RF-028 sobe para MVP-0 e vira Must, via ADR. |
| 5 | Redirecionamento de periféricos | ✅ **Decidida — ADR-0008.** Política base definida: impressora, token USB, área de transferência e saída de áudio permitidos; unidades locais, COM/LPT, entrada de áudio e demais USB negados. RNF-014 antecipado para MVP-0. |

## 8. Emendas a este documento

Registro das alterações posteriores à primeira submissão, cada uma com o ADR que a autoriza (RA-06 —
nenhuma mudança silenciosa).

| Data | Requisito | Alteração | ADR |
|------|-----------|-----------|-----|
| 2026-08-08 | RF-002, RF-003 | Detalhamento do modelo híbrido e do vínculo identidade→conta AD | ADR-0001 |
| 2026-08-08 | RNF-007 | Referência à topologia de VMs separadas | ADR-0002 |
| 2026-08-08 | RNF-009 | Acrescentadas ACL, aprovação nominal de dispositivo e varredura externa obrigatória | ADR-0003 |
| 2026-08-08 | RNF-036 | Isolamento passa a ser exigido via filtro global no `DbContext` e teste de violação obrigatório | ADR-0004 |
| 2026-08-08 | RF-062, RF-063, RF-064 | **Fase alterada de V2 para MVP-1** (metering mínimo antecipado) | ADR-0006 |
| 2026-08-08 | RNF-018 | Prazos de retenção definidos por categoria; PRE-08 resolvida | ADR-0007 |
| 2026-08-08 | RNF-022 | **Passa de Should não-bloqueante para Must bloqueante**; PRE-09 resolvida | ADR-0007 |
| 2026-08-08 | RNF-014 | **Fase alterada de V2 para MVP-0**, com política base de redirecionamento definida | ADR-0008 |
