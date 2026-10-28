# 🏎️ F1Cache

Biblioteca extensível de cache em memória desenvolvida em **C# / .NET 8** para a disciplina de Design Patterns

## Objetivo

Desenvolver uma biblioteca reutilizável que permita armazenar e recuperar objetos em memória, oferecendo mecanismos de expiração, políticas de remoção configuráveis e extensibilidade através de Design Patterns

A Fórmula 1 será utilizada como cenário de demonstração, mas a biblioteca poderá ser utilizada em diferentes aplicações .NET.

## Estrutura do projeto

- `src/F1Cache.Core` — Biblioteca principal
- `tests/F1Cache.Tests` — Testes unitários
- `examples/F1Cache.Demo` — Demonstração de utilização

## Estado atual — 28/10/2026

**Implementado:**
- Interface genérica `ICache<TKey, TValue>`
- Classe `CacheEntry<TValue>` com suporte a TTL
- `CacheManager<TKey, TValue>` com operações basicas
- Demonstração de armazenamento, recuperação e remoçao

**Próximas etapas:**
- Configuração de TTL pelo consumidor
- Limites de memória
- Políticas de evicção FIFO e LRU
- Implementação dos Design Patterns
- Testes unitários e documentação completa

## Como executar

Requisitos: .NET SDK 8.

```bash
dotnet build
dotnet run --project examples/F1Cache.Demo
```