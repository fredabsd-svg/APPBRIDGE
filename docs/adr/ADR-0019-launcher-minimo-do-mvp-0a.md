# ADR-0019 — Launcher mínimo do MVP-0a
Data: 2026-09-24 · Status: **aceito** · Autor: Arquiteto de Software Principal

## Contexto

O MVP-0a exige que um usuário inicie um aplicativo pela API e pelo `mstsc`, sem exigir a interface
gráfica, atalhos nem MSIX reservados ao MVP-0b. Até a S012 havia Control Plane, mas nenhum executável
de estação que completasse esse caminho.

## Decisão

1. O MVP-0a terá um launcher de console em C#/.NET 10. Ele autentica interativamente no Entra ID
   usando um escopo delegado configurado, troca o ID token por um token do AppBridge, exibe os
   aplicativos autorizados, solicita o lançamento e abre o `.rdp` assinado com `mstsc.exe`.
2. O cliente é um public client MSAL, sem segredo de cliente. O access token obtido para o escopo de
   API não é enviado ao Control Plane; o ID token é usado na troca de sessão. Os tokens ficam apenas
   na memória do processo; o MVP-0a não persiste nem renova tokens (ADR-0017, T-303).
3. O arquivo `.rdp` é criado com nome aleatório e atributos temporários na pasta temporária do usuário.
   O launcher o remove ao expirar o prazo informado pelo servidor ou ao receber interrupção graciosa.
4. WinUI 3, MSIX, Credential Manager, cache SQLite, atalhos e manipulador `appbridge://` continuam na
   linha de base para MVP-0b; este recorte não altera ADR-0005 nem os requisitos desses componentes.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Aguardar a interface WinUI 3 | Atrasaria a primeira validação do caminho ponta a ponta e acrescentaria empacotamento antes de validar API, RDP e RDS. |
| Fazer um aplicativo WinUI 3 descartável | Tem custo de inicialização e suporte maior que um menu de console para o uso interno e de uma única estação do MVP-0a. |
| Usar credenciais coletadas em formulário | Passaria a senha pelo processo AppBridge e contrariaria ADR-0010. |

## Consequências

**Positivas:** há um cliente executável mínimo para exercitar login, catálogo e lançamento; o projeto
continua em .NET 10 e o token não é gravado em arquivo.

**Negativas:** exige um terminal interativo e autenticação Entra a cada execução; não é instalável nem
adequado ao uso diário. O usuário precisa interromper o processo ou aguardar o TTL para a remoção do
arquivo temporário.

**Riscos:** encerramento forçado do processo ou perda de energia pode impedir a limpeza local; o teste
real de MSAL, `mstsc` e exclusão segura ainda precisa ocorrer em estação Windows registrada no Entra.

## Requisitos relacionados

RF-001, RF-003, RF-011, RF-018..RF-022, RNF-002, RNF-003, RNF-043 · ADR-0005, ADR-0010, ADR-0017
