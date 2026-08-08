# Carta-modelo — consulta formal de licenciamento
> Modelo parametrizado (T-001). Substitua tudo entre `[ ]` antes de enviar.
> Versões prontas: `carta-dominio.md` e `carta-alterdata.md`.

**Orientações de uso (não enviar esta seção):**
- Enviar pelo **canal comercial formal**, com cópia ao gerente de conta.
- Assunto sugerido: `Consulta formal de licenciamento — execução em servidor de sessão — [EMPRESA]`
- Tom neutro e factual. **Não sugerir a resposta desejada** — uma pergunta que induz o "não" recebe o "não".
- Manter em **uma página**. Consulta longa é consulta não respondida.

---

**[CIDADE], [DATA].**

**À [NOME DO FORNECEDOR]**
A/C: [Departamento Comercial / Gerente de Conta / Departamento Jurídico]

**Assunto: Consulta formal sobre licenciamento para execução em servidor de sessão (Windows Server RDS / RemoteApp)**

Prezados,

Somos cliente de [NOME DO PRODUTO] e utilizamos a solução em nossas operações desde [ANO]. Estamos
avaliando uma mudança na forma de disponibilizar o software aos usuários e, **antes de qualquer
implementação**, gostaríamos de obter a posição formal de V.Sas. quanto ao licenciamento.

**Cenário técnico avaliado.** O software seria instalado uma única vez em servidor Windows Server com
os papéis de Serviços de Área de Trabalho Remota (RDS), e disponibilizado aos usuários no modo
**RemoteApp** — em que apenas a janela do aplicativo aparece na estação do usuário, sem entrega do
desktop do servidor. Vários usuários utilizariam o software **simultaneamente**, cada um em sua
própria sessão, com credenciais individuais de domínio e perfil isolado.

Nosso ambiente atual e previsto:

| Item | Informação |
|------|-----------|
| Produto e módulos | [PRODUTO / MÓDULOS] |
| Versão em uso | [VERSÃO] |
| Nº de licenças contratadas | [Nº] |
| Nº de usuários simultâneos previsto | [Nº] |
| Contrato / código de cliente | [IDENTIFICAÇÃO] |

**Solicitamos, por gentileza, resposta às questões abaixo.** Pedimos que, sempre que possível, seja
indicada a **cláusula do contrato ou do termo de licença** que fundamenta cada resposta.

**1. Uso pelos próprios colaboradores.** O contrato de licença vigente permite que o software seja
instalado em servidor de sessão e acessado simultaneamente por **colaboradores da própria empresa
licenciada**, no modo descrito acima?

**2. Hospedagem para terceiros.** E na hipótese de o software ser executado em servidor operado por
**um prestador de serviços**, sendo acessado por usuários de **outra pessoa jurídica** que detenha as
respectivas licenças? Há vedação, autorização condicionada ou modalidade contratual específica para
esse caso?

**3. Modalidade e custo.** Existe modalidade de licenciamento própria para esse cenário — por
servidor, por usuário simultâneo, por conexão ou equivalente? Em caso positivo, qual é e qual a
condição comercial aplicável?

**4. Suporte técnico.** O suporte técnico e a garantia de funcionamento do produto **são integralmente
mantidos** quando o software opera em servidor de sessão multiusuário? Havendo qualquer limitação de
suporte nesse cenário, pedimos que seja especificada.

**5. Requisitos e restrições técnicas.** Há requisito ou restrição técnica a observar — versão mínima,
configuração de banco de dados, componentes que exijam instalação por usuário, ou incompatibilidade
conhecida com o modo RemoteApp?

**6. Certificado digital.** O uso de certificado digital **A1 (arquivo)** e **A3 (token ou cartão
redirecionado da estação do usuário)** dentro da sessão remota é suportado? Há orientação específica
do fabricante quanto a isso?

**7. Atualizações.** Em ambiente de servidor com usuários conectados, qual o procedimento recomendado
para aplicação de atualizações — é necessário encerrar as sessões ativas, e há janela recomendada?

**8. Impressão.** Há restrição quanto ao uso de impressoras locais redirecionadas para a sessão
remota?

Por se tratar de decisão de infraestrutura com impacto contratual, pedimos que a resposta seja
encaminhada **por escrito, por este mesmo meio**, com identificação do responsável e do
departamento. Colocamo-nos à disposição para prestar qualquer esclarecimento adicional sobre o
ambiente técnico.

Agradecemos a atenção e aguardamos retorno.

Atenciosamente,

**[NOME COMPLETO]**
[CARGO] — [RAZÃO SOCIAL]
CNPJ [CNPJ] · [E-MAIL] · [TELEFONE]
