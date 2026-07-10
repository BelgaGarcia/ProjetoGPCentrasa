# ADR 0004 — Design system local

- **Status:** aceito
- **Data:** 10/07/2026

## Decisão

Usar Bootstrap apenas como fundação local de formulários e grid, com identidade
visual implementada por CSS próprio, tokens semânticos e SVGs locais.

## Motivo

A aplicação precisa funcionar offline e manter proximidade com o Gamma e o
Obsidian sem herdar aparência genérica ou depender de CDN e bibliotecas de
ícones.

## Consequências

- Cores, espaços, bordas, sombras e tipografia possuem variáveis centralizadas.
- Gestão e apresentação usam layouts distintos, mas compartilham componentes.
- JavaScript fica limitado à navegação móvel e à Fullscreen API.
- Novos módulos devem reutilizar os componentes antes de criar variações.
