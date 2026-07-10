# ADR 0001 — Monólito modular

- **Status:** aceito
- **Data:** 10/07/2026

## Decisão

Separar a aplicação em Domain, Application, Infrastructure e Web, implantados
como um único processo ASP.NET Core.

## Motivo

A separação mantém regras testáveis e persistência substituível, enquanto o
monólito preserva a simplicidade adequada a uma aplicação local de um usuário.

## Consequências

- As dependências devem apontar para dentro, em direção ao domínio.
- Mudanças que cruzem módulos continuam na mesma transação.
- Microsserviços, mensageria e CQRS completo ficam fora do escopo.
