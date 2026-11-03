Giovani Nota Simões
Enzo Ricardo de Oliveira Leitão
Sarah Maneira
Luan Vieira Lucho

YouTube Link: PLACEHOLDER

# 🏎️ F1Cache

SDK extensível de dados da Fórmula 1 com motor de cache em memória de alto desempenho, desenvolvido em **C# / .NET 10** para a disciplina de **Design Patterns**.

---

## 📌 Objetivo do Projeto

Desenvolver uma biblioteca in-process reutilizável (distribuível via NuGet/DLL) que forneça acesso a dados da Fórmula 1 integrando a **API Jolpica (Ergast F1)** e a **API da Wikipedia**, estruturada sobre um **motor de cache em memória extensível** para evitar requisições redundantes, suportar políticas de expiração, estratégias de remoção configuráveis e demonstrar na prática os **8 padrões de projeto GoF** (2 por integrante).

> **Restrição Acadêmica:** O projeto é estritamente uma biblioteca/SDK em processo (não é uma API REST ou serviço executável independente).

---

## 🏛️ Mapeamento Arquitetural: 8 Design Patterns (2 por Integrante)

| Integrante | Padrão GoF | Alvo no Código | Problema Arquitetural Resolvido |
| :--- | :--- | :--- | :--- |
| **Integrante 1** | **Facade** | `F1Client` | Fornece uma API pública simplificada e ergonômica unificando cache, requisições Jolpica e enriquecimento de mídia. |
| | **Builder** | `F1ClientBuilder` | Configuração fluente do SDK (TTL, capacidade, timeouts, interceptores e estratégias de evicção) sem construtores telescópicos. |
| **Integrante 2** | **Strategy** | `IEvictionStrategy`<br>(`LruStrategy`, `FifoStrategy`) | Desacopla o algoritmo de substituição/evicção de itens quando a capacidade máxima de memória for atingida (aberto a OCP). |
| | **Observer** | `ICacheObserver`<br>`CacheSubject` | Emite eventos de ciclo de vida do cache (`OnHit`, `OnMiss`, `OnEvict`, `OnExpire`) para telemetria, logs e métricas desacopladas. |
| **Integrante 3** | **Decorator** | `CachedDriverProvider` | Envolve fetchers brutos (`IDriverProvider`) com verificação transparente de cache em memória e suporte a *Negative Caching*. |
| | **Adapter** | `JolpicaDriverProvider`<br>`WikipediaThumbnailAdapter` | Converte payloads JSON heterogêneos de APIs externas nos contratos de domínio tipados da biblioteca (`Driver`). |
| **Integrante 4** | **Chain of Responsibility** | `ResiliencyPipeline`<br>(Handlers) | Encadeia etapas de tratamento de requisições de forma modular (ex.: Validação → RateLimit → CacheLookup → NetworkDispatch). |
| | **Template Method** | `DriverDataEnricher`<br>(`WikipediaDriverEnricher`) | Define o esqueleto invariante do pipeline de resolução (`FetchBase()` → `Validate()` → `EnrichMedia()` → `Normalize()`). |

---

## 📁 Estrutura da Solução

* `Directory.Packages.props` — Gerenciamento centralizado de dependências NuGet (**Central Package Management - CPM**)
* `src/F1Cache.Core/` — Núcleo da biblioteca reutilizável
  * `Models/` — Modelos de domínio imutáveis (`Driver.cs`, `CacheEntry.cs`)
  * `Interfaces/` — Contratos e abstrações (`ICache.cs`, `IDriverProvider.cs`)
  * `Managers/` — Implementação do motor de cache em memória (`CacheManager.cs`)
  * `Clients/Jolpica/` — Adaptador HTTP Jolpica (`JolpicaDriverProvider.cs`, DTOs e Mappers)
  * `Services/` — Decorator de cache transparente (`CachedDriverProvider.cs`)
* `tests/F1Cache.Tests/` — Suíte de testes automatizados unitários via xUnit (sem dependência externa de rede)
* `examples/F1Cache.Demo/` — Demonstração simplificada de consumo da biblioteca

---

## 🚀 Estado Atual da Implementação

### Concluído:
- [x] **Gerenciamento Central de Pacotes (CPM):** Migração completa de versões para `Directory.Packages.props`.
- [x] **Modelo de Domínio `Driver`:** `record` posicional imutável contendo metadados completos de pilotos da F1.
- [x] **Motor de Cache In-Memory (`CacheManager<TKey, TValue>` & `ICache<TKey, TValue>`):**
  - Armazenamento em tabela hash $O(1)$.
  - **Expiração Passiva / Lazy:** Remoção automática no momento da leitura (`TryGet`).
  - **Negative Caching:** Proteção nativa contra *Cache Penetration* (armazena ausência de registros sem requisições repetidas ao backend).
- [x] **Container de Metadados (`CacheEntry<TValue>`):**
  - Timestamps absolutos UTC via `DateTimeOffset.UtcNow`.
  - Validação estrita de TTL positivo com `ArgumentOutOfRangeException.ThrowIfLessThanOrEqual`.
- [x] **Adaptador Jolpica (`JolpicaDriverProvider`):**
  - Consumo da API Ergast/Jolpica (`/drivers/{id}.json` e paginação com `/drivers.json`).
  - Injeção de dependência via *Primary Constructor* de `HttpClient`.
  - Desacoplamento do mapeamento DTO em classe estática dedicada (`JolpicaDriverMapper`).
- [x] **Decorator de Cache (`CachedDriverProvider`):**
  - Implementação pura do padrão Decorator interceptando `GetDriverByIdAsync` com Cache-Aside e Negative Caching.
  - Pass-through em `GetDriversPageAsync` para evitar poluição de cache (*Cache Pollution*).
- [x] **Testes Automatizados:** 12 testes unitários cobrindo fluxo de hit/miss, negative caching, validações de limites/offsets e deserialização de payloads.

### Próximos Passos:
- [ ] **Strategy:** Implementar `IEvictionStrategy` (`LruStrategy`, `FifoStrategy`) e limite de capacidade no cache.
- [ ] **Observer:** Implementar `ICacheObserver` para notificar `OnHit`, `OnMiss`, `OnExpire` e `OnEvict`.
- [ ] **Builder & Facade:** Criar `F1Client` e `F1ClientBuilder` para inicialização fluente e configuração pronta para uso.
- [ ] **Adapter Wikipedia & Template Method:** Implementar busca de thumbnails de pilotos e fluxo invariante de enriquecimento de dados.
- [ ] **Chain of Responsibility:** Pipeline de resiliência e validação de requisições.

---

## 🛠️ Como Executar

Requisitos: **.NET SDK 10.0** (ou 8.0).

### Compilar a solução:
```bash
dotnet build
```

### Executar os testes unitários:
```bash
dotnet test
```

### Executar a aplicação de demonstração:
```bash
dotnet run --project examples/F1Cache.Demo
```
