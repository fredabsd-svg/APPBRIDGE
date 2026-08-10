#!/usr/bin/env bash
# check-docs.sh — checagem de consistência da documentação (RA-02, ADR-0015)
#
# Uso:  ./scripts/check-docs.sh
# Saída: 0 se nada encontrado; 1 se houver achado.
#
# Não substitui a leitura humana: verifica o que é mecanizável (referências
# mortas, IDs órfãos, contagens, estados contraditórios). O alinhamento de
# conteúdo às decisões vigentes continua sendo trabalho de quem fecha a sessão.

set -uo pipefail
cd "$(dirname "$0")/.."

FINDINGS=0
note() { printf '  \033[33m!\033[0m %s\n' "$1"; FINDINGS=$((FINDINGS+1)); }
ok()   { printf '  \033[32mok\033[0m %s\n' "$1"; }
head_() { printf '\n\033[1m%s\033[0m\n' "$1"; }

# ---------------------------------------------------------------- 1. estados
head_ "1. Estado de aprovação contraditório"
# Só os 7 entregáveis: CHANGELOG guarda histórico e STATUS tem a legenda de estados.
DELIVERABLES="VISAO REQUISITOS ARQUITETURA MODELO-DE-DADOS API SEGURANCA ROADMAP"
pend=0
for d in $DELIVERABLES; do
  f="docs/$d.md"
  [ -f "$f" ] || { note "$f não existe"; continue; }
  if head -6 "$f" | grep -q "aguardando aprovação" \
     && grep -q "| $d.md\` | ✅ aprovado" docs/STATUS.md 2>/dev/null; then
    note "$f diz 'aguardando aprovação' no cabeçalho, mas STATUS.md o dá como aprovado"
    pend=1
  fi
done
[ $pend -eq 0 ] && ok "cabeçalhos dos 7 entregáveis coerentes com STATUS.md"

# ------------------------------------------------------------ 2. contagem ADR
head_ "2. Contagem de ADRs"
real=$(find docs/adr -name 'ADR-[0-9]*.md' | wc -l | tr -d ' ')
cited=$(grep -rhoE '\*\*[0-9]+ ADRs\*\* estão aceitos' docs/STATUS.md 2>/dev/null | grep -oE '[0-9]+' | head -1 || true)
if [ -n "${cited:-}" ] && [ "$cited" != "$real" ]; then
  note "STATUS.md cita $cited ADRs aceitos; existem $real arquivos"
else
  ok "$real ADRs, contagem coerente"
fi

# --------------------------------------------------------- 3. ADR referenciado
head_ "3. ADR referenciado mas inexistente"
miss=0
# TEMPLATE.md usa ADR-0000 como exemplo — não é referência real.
for id in $(grep -rhoE 'ADR-[0-9]{4}' docs --include='*.md' | sort -u | grep -v 'ADR-0000'); do
  num=${id#ADR-}
  if ! ls docs/adr/ADR-"$num"-*.md >/dev/null 2>&1; then
    note "$id é referenciado mas não existe em docs/adr/"
    miss=1
  fi
done
[ $miss -eq 0 ] && ok "todos os ADRs referenciados existem"

# ------------------------------------------------------- 4. requisitos órfãos
head_ "4. Requisitos referenciados mas não definidos"
defined=$(grep -oE '^\| \*\*(RF|RNF)-[0-9]{3}\*\*' docs/REQUISITOS.md 2>/dev/null \
          | grep -oE '(RF|RNF)-[0-9]{3}' | sort -u)
used=$(grep -rhoE '\b(RF|RNF)-[0-9]{3}\b' docs --include='*.md' \
       | sort -u)
orphans=$(comm -13 <(echo "$defined") <(echo "$used"))
if [ -n "$orphans" ]; then
  echo "$orphans" | while read -r o; do
    [ -n "$o" ] && note "$o é usado mas não está definido em REQUISITOS.md"
  done
else
  ok "nenhum requisito órfão"
fi

# ----------------------------------------------------- 5. links internos mortos
head_ "5. Links internos mortos"
dead=0
while IFS= read -r line; do
  src=${line%%::*}; tgt=${line#*::}
  case "$tgt" in http*|"#"*|"") continue;; esac
  tgt=${tgt%%#*}
  dir=$(dirname "$src")
  [ -e "$dir/$tgt" ] || { note "$src apontando para '$tgt' (inexistente)"; dead=1; }
done < <(grep -rnoE '\]\([^)]+\)' docs --include='*.md' \
         | sed -E 's|^([^:]+):[0-9]+:\]\((.*)\)$|\1::\2|')
[ $dead -eq 0 ] && ok "nenhum link interno morto"

# ------------------------------------------------ 6. colisão de prefixo de ID
head_ "6. Prefixos reservados usados como identificador de risco"
# RP- e RA- pertencem ao prompt mestre (Regras do Projeto / de Auditoria).
if grep -rnE '^\| \*\*(RP|RA)-[0-9]{2}\*\* \|' docs --include='*.md' \
     | grep -v CLAUDE.md >/dev/null 2>&1; then
  note "há linhas de tabela usando RP-/RA- como ID próprio — prefixos reservados"
else
  ok "nenhuma colisão de prefixo"
fi

# ------------------------------------------------------- 7. log de fechamento
head_ "7. Log da sessão corrente"
cur=$(grep -oE 'Sessão atual:\*\* S[0-9]{3}' docs/STATUS.md 2>/dev/null | grep -oE 'S[0-9]{3}' || true)
if [ -n "${cur:-}" ]; then
  if ls docs/auditoria/*-"$cur".md >/dev/null 2>&1; then
    ok "$cur tem log de sessão"
  else
    note "STATUS.md aponta $cur, mas não há docs/auditoria/*-$cur.md (RA-02)"
  fi
fi

# ------------------------------------------------------------------- resultado
printf '\n'
if [ $FINDINGS -eq 0 ]; then
  printf '\033[32mSem achados.\033[0m A checagem mecânica passou.\n'
  printf 'Falta a parte humana: o conteúdo ainda reflete as decisões vigentes?\n'
  exit 0
else
  printf '\033[33m%d achado(s).\033[0m Corrigir antes de fechar a sessão (RA-02).\n' "$FINDINGS"
  exit 1
fi
