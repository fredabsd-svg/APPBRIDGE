# Roteiro de implantação — E-01 · T-102 a T-107
> Épico **E-01** · 34 pts · Caminho crítico do MVP-0a
> Decisões aplicadas: ADR-0002, ADR-0003, ADR-0008, ADR-0009, ADR-0010 · Sessão S005 · 2026-08-08

---

## 0. Como usar este roteiro

- Executar **na ordem**. Cada passo tem verificação própria; **não avançar com verificação
  reprovada** — em infraestrutura, erro adiado vira retrabalho de dias.
- Os nomes de cmdlets e caminhos de GPO abaixo são os usuais do Windows Server, mas **variam entre
  versões e idiomas do sistema**. Conferir no console real antes de executar; este roteiro indica
  *o quê* e *por quê*, não substitui a documentação do fabricante.
- Registrar cada verificação concluída. As de segurança (V-01, V-08) são **critério de aceite**, não
  formalidade (`SEGURANCA.md` §7).

---

## T-102 · Hyper-V e as duas VMs — 5 pts

**Por que:** ADR-0002. Session host no controlador de domínio obrigaria a conceder logon local no DC
a todos os usuários finais, transformando um escape de aplicativo em comprometimento do domínio.

1. Habilitar o papel Hyper-V no host físico. O host serve **apenas** para virtualização — é essa
   condição que faz a licença Standard cobrir 2 VMs.
2. Criar **AB-DC01** (2 vCPU, 4 GB, 80 GB) e **AB-RDS01** (6 vCPU, restante da RAM, 250 GB + volume
   de perfis).
3. Usar **discos de tamanho fixo**, não dinâmicos, no volume de perfis — expansão dinâmica durante
   escrita de container FSLogix é fonte conhecida de lentidão e corrupção.
4. Desabilitar checkpoints automáticos nas duas VMs. Checkpoint em VM com banco de dados ou container
   de perfil ativo cria inconsistência silenciosa.

**Verificação:** as duas VMs sobem de forma independente; a AB-RDS01 não tem papel de DC.

---

## T-103 · Domínio AD DS — 5 pts

**Por que:** ADR-0001. O RDS clássico exige domínio, e é o AD DS que autoriza a conta que abre a
sessão.

1. Na **AB-DC01**, instalar o papel AD DS e promover a controlador de uma nova floresta
   (`Install-ADDSForest`).
2. **Escolher o nome do domínio com cuidado.** Usar um subdomínio de domínio público que você
   controle — por exemplo `ad.suaempresa.com.br`. **Evitar `.local`**: ele não é roteável e obriga a
   contornos no Entra Connect adiante (ADR-0001, riscos).
3. Se o domínio interno não coincidir com o domínio de e-mail, **adicionar o sufixo de UPN roteável**
   à floresta (`Set-ADForest -UPNSuffixes`) e usá-lo nas contas de usuário. É a armadilha clássica da
   identidade híbrida, e custa muito mais corrigir depois.
4. Criar a estrutura de OU prevista em ADR-0004 — uma OU por tenant, mesmo que hoje só exista um.
5. Criar as contas de serviço com **menor privilégio** (RNF-005, PS-05): a conta do Control Plane
   precisa de leitura no diretório e uso do certificado de assinatura, **e nada mais**. Nenhuma delas
   é administradora de domínio.

**Verificação:** ingressar uma máquina de teste; o UPN dos usuários é roteável.

---

## T-104 · Pilha RDS — 5 pts

1. Na **AB-RDS01**, instalar a implantação de sessão com Connection Broker, Web Access e Session Host
   (`New-RDSessionDeployment`).
2. Instalar e ativar o **RD Licensing** em modo **por usuário**, e apontar o session host para ele.
   Sem isso, o período de carência expira em 120 dias e o ambiente para — no meio do dogfood.
3. Criar a coleção de sessão do tenant (`New-RDSessionCollection`).
4. Publicar um RemoteApp de teste (`New-RDRemoteApp`) — o Bloco de Notas serve.
5. Criar também o **SessionPrimer**, o RemoteApp mínimo do prelaunch (RF-023). Ele será usado em
   T-1002 para validar PRE-22.
6. Ajustar por GPO o **tempo de logoff de sessão RemoteApp**: se a sessão encerrar assim que o último
   aplicativo fechar, o prelaunch não se sustenta. Este é o ajuste de que PRE-22 depende.

**Verificação:** o RemoteApp de teste abre a partir de um `.rdp` gerado manualmente.

---

## T-105 · FSLogix e AppLocker — 5 pts

**FSLogix (RNF-011)**
1. Instalar o agente na AB-RDS01.
2. Configurar os containers de perfil apontando para o volume dedicado (chaves em
   `HKLM\SOFTWARE\FSLogix\Profiles`: habilitação e localização dos VHD).
3. **Aplicar as exclusões de antivírus** recomendadas para os arquivos de container. Antivírus
   varrendo VHD de perfil é causa recorrente de lentidão de logon e de corrupção.

**AppLocker ou WDAC em allowlist (RNF-006)**
4. Garantir que o serviço de identidade de aplicativo esteja em execução automática — sem ele, a
   política não é aplicada e a proteção **parece** existir sem existir.
5. Gerar as regras padrão e acrescentar os aplicativos publicados
   (`New-AppLockerPolicy` / `Set-AppLockerPolicy`).
6. Rodar primeiro em **modo auditoria**, coletar o que seria bloqueado, ajustar, e só então passar a
   modo obrigatório. Ir direto para bloqueio quebra aplicativo legado — e sistemas contábeis antigos
   são exatamente o caso.

**Verificação — V-08 (critério de aceite):** copiar um executável não publicado para o session host e
confirmar que a execução é **bloqueada**.

---

## T-106 · Estações e GPOs — 8 pts · **o passo que mais estoura prazo**

**Por que é o mais arriscado:** depende da disponibilidade das máquinas e das pessoas, não de
técnica. Sequenciar cedo, nunca na véspera do teste de CS-01.

1. **Ingressar as estações no domínio** (ADR-0010). Verificar antes a edição do Windows de cada
   máquina — **edição Home não ingressa em domínio** (PRE-21). As que forem Home seguem pelo caminho
   degradado, com senha a cada lançamento, ou precisam de upgrade de edição.
2. **GPO de delegação de credenciais** — "Permitir a delegação de credenciais padrão", listando os
   session hosts **nominalmente** (`TERMSRV/ab-rds01.dominio`). **Nunca `TERMSRV/*`**: o curinga
   autorizaria a estação a entregar a credencial do usuário a qualquer servidor alcançável, contra
   RNF-005 (ADR-0010, item 2).
3. **GPO de publicadores confiáveis de `.rdp`** — distribuir a impressão digital do certificado de
   assinatura e **desabilitar** a permissão de abrir `.rdp` de publicador desconhecido (ADR-0009).
   Sem esse par de ajustes, a assinatura existe mas não protege ninguém.
4. **GPO de redirecionamento**, conforme a política base do ADR-0008:

| Recurso | Estado | Onde |
|---------|--------|------|
| Impressora local | **Permitido** | Contador imprime |
| Smart card / token A3 | **Permitido** | DIF-01; validar PRE-20 |
| Área de transferência | **Permitido** | Rotina diária; risco AM-17 aceito e com revisão marcada |
| Saída de áudio | Permitido | Alertas sonoros |
| **Unidades locais** | **Negado** | Principal caminho de exfiltração |
| Portas COM/LPT | Negado | Sem caso de uso |
| Entrada de áudio | Negado | Sem caso de uso |
| Demais dispositivos USB | Negado | Exceção só nominal e registrada |

5. Preparar o **certificado de assinatura** (ADR-0009): chave privada **não exportável** no
   repositório da máquina, com permissão de uso apenas para a conta de serviço do Control Plane
   (RNF-008). Registrar a impressão digital e a validade na tabela `signing_certificate`.

**Verificações:** a estação abre RemoteApp **sem pedir senha**; um `.rdp` sem assinatura é recusado
(V-04); o disco local **não** aparece na sessão.

---

## T-107 · Rede privada e varredura externa — 3 pts

**Por que:** ADR-0003. A malha privada **move** o 3389 para uma interface privada — ela não o elimina.
Sem ACL, "ninguém da internet alcança" vira "todo dispositivo cadastrado alcança".

1. Instalar o cliente de malha nas máquinas que precisarem de acesso externo.
2. **ACL explícita**, negando por padrão: apenas um grupo nomeado alcança a porta 3389 do session host
   e a porta do Control Plane.
3. **Desligar a aprovação automática de dispositivos.** Cada dispositivo é aprovado nominalmente.
4. Confirmar que **não há encaminhamento de porta** no roteador para 3389, para o session host ou
   para o Control Plane.
5. **V-01 — varredura externa** contra o IP público do escritório, com o resultado **arquivado**.

> **V-01 é critério de aceite de CS-04.** Sem a varredura registrada, o requisito RNF-001 está
> presumido, não atendido — e presumir é exatamente o que este projeto não faz.

---

## Aceite do épico E-01

Um usuário real abre um RemoteApp a partir da sua estação, **sem digitar senha**, sem `mstsc` manual,
com o disco local não redirecionado e a porta 3389 comprovadamente fechada para a internet.

| Verificação | Fonte | Estado |
|-------------|-------|--------|
| V-01 · varredura externa | T-107 | ☐ |
| V-04 · `.rdp` adulterado ou sem assinatura é recusado | T-106 | ☐ |
| V-08 · AppLocker bloqueia binário não publicado | T-105 | ☐ |
| Logon sem senha na abertura de RemoteApp | T-106 | ☐ |
| Disco local não redirecionado | T-106 | ☐ |
| DC sem logon local de usuário final | T-102 | ☐ |

## Premissas a confirmar durante a execução

| ID | Premissa | Onde se confirma |
|----|----------|------------------|
| PRE-18 | Host suporta virtualização assistida por hardware | T-101 / T-102 |
| PRE-21 | Todas as estações têm edição que ingressa em domínio | T-106 |
| PRE-20 | Token A3 funciona redirecionado | T-106 |
| PRE-22 | Prelaunch sustenta a jornada | T-104 + T-1002 |
| PRE-27 | 2–3 GB de RAM por sessão | dogfood |
| PRE-28 | 20–30 GB de container FSLogix por usuário | dogfood |
