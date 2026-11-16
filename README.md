Giovani Nota Simões
Enzo Ricardo de Oliveira Leitão
Sarah Maneira
Luan Vieira Lucho

YouTube Link: PLACEHOLDER

# 🏎️ F1Cache

SDK em **C# / .NET 10** para consultar dados de pilotos de Fórmula 1. Combina a **API Jolpica (Ergast F1)** com a **Wikipedia** (descrição e foto do piloto) e guarda o resultado em um **cache em memória** configurável, para que consultas repetidas não façam nenhuma requisição de rede.

---

## 🚀 Uso rápido

```csharp
using F1Cache.Core;

using IF1Client f1 = F1Client.CreateBuilder().Build();

var alonso = await f1.GetDriverAsync("alonso");

Console.WriteLine(alonso?.FullName);      // Fernando Alonso
Console.WriteLine(alonso?.Description);   // Primeiro parágrafo do artigo da Wikipedia
Console.WriteLine(alonso?.ThumbnailUrl);  // Foto do piloto

await f1.GetDriverAsync("alonso");        // Vem do cache: nenhuma requisição
Console.WriteLine(f1.Statistics.HitRatio); // 0.5
```

### API

| Membro | Descrição |
| :--- | :--- |
| `GetDriverAsync(id)` | Piloto pelo id da Jolpica (`"alonso"`, `"hamilton"`, `"max_verstappen"`; sem diferenciar maiúsculas). Enriquecido com a Wikipedia e cacheado. Retorna `null` se o piloto não existir. |
| `GetDriversPageAsync(limit, offset)` | Página de pilotos (até 100 por página). Não é cacheada nem enriquecida, útil para descobrir ids. |
| `Statistics` | Contadores do cache: `Hits`, `Misses`, `Evictions`, `Expirations`, `HitRatio`. |
| `ClearCache()` | Esvazia o cache. |

Dependa de `IF1Client` (e não de `F1Client`) para poder usar um fake nos testes do seu código.

### Configuração

Todas as opções têm valor padrão:

```csharp
using var f1 = F1Client.CreateBuilder()
    .WithCacheTimeToLive(TimeSpan.FromMinutes(5))
    .WithCacheCapacity(100)
    .WithEvictionPolicy(EvictionPolicy.Lfu)
    .WithObserver(new MeuObserver())
    .WithHttpHandler(() => new LoggingHandler(Console.WriteLine))
    .Build();
```

| Opção | Padrão | Descrição |
| :--- | :--- | :--- |
| `WithCacheTimeToLive` | 1 min | Tempo que um piloto encontrado fica no cache. Pilotos inexistentes ficam 10 s. |
| `WithCacheCapacity` | 50 | Máximo de pilotos no cache antes de remover algum. |
| `WithEvictionPolicy` | `Lru` | Qual piloto sai quando o cache enche: `Lru` (usado há mais tempo), `Lfu` (usado menos vezes) ou `Fifo` (inserido primeiro). |
| `WithEvictionStrategy` | | Estratégia de remoção própria (`IEvictionStrategy<string>`). |
| `WithObserver` | | Recebe os eventos do cache (`OnHit`, `OnMiss`, `OnExpire`, `OnEvict`). |
| `WithEnricher` | Wikipedia | Troca o enriquecimento da Wikipedia por uma subclasse própria de `DriverDataEnricher`. |
| `WithRetryPolicy` | 2 tentativas | Repete requisições que falharam com 429, 5xx ou erro de rede, com espera crescente. `0` desliga. |
| `WithRateLimit` | 250 ms | Intervalo mínimo entre requisições ao mesmo host. `TimeSpan.Zero` desliga. |
| `WithHttpHandler` | | Adiciona um `DelegatingHandler` próprio à cadeia HTTP. |
| `WithHttpTimeout` | 10 s | Tempo máximo de cada requisição, incluindo as novas tentativas. |
| `WithHttpClient` | | Usa o seu `HttpClient` como está (não pode ser combinado com as opções HTTP acima). |

### Quando a Wikipedia falha

A consulta do piloto nunca falha por causa da Wikipedia:

| Situação | `Description` | `ThumbnailUrl` |
| :--- | :--- | :--- |
| Artigo encontrado | Primeiro parágrafo | Foto do artigo |
| Artigo sem foto | Primeiro parágrafo | Imagem de placeholder.co |
| Sem artigo | `"N/A"` | Imagem de placeholder.co |
| Erro de rede ou timeout | `"Failed to fetch"` | Imagem de placeholder.co |

---

## 🧩 Estendendo a biblioteca

Todo comportamento abaixo é adicionado com classes suas, sem alterar o código da biblioteca.

### Estratégia de remoção própria

Implemente `IEvictionStrategy<TKey>`: o cache avisa quando uma chave é adicionada, lida ou removida, e pede uma vítima quando enche.

```csharp
public sealed class RandomEvictionStrategy<TKey> : IEvictionStrategy<TKey> where TKey : notnull
{
    private readonly List<TKey> _keys = [];

    public void OnAdded(TKey key) => _keys.Add(key);
    public void OnAccessed(TKey key) { }
    public void OnRemoved(TKey key) => _keys.Remove(key);
    public void Clear() => _keys.Clear();

    public TKey Evict()
    {
        var victim = _keys[Random.Shared.Next(_keys.Count)];
        _keys.Remove(victim);
        return victim;
    }
}

// Recebe uma fábrica: cada cliente construído precisa da sua própria instância.
builder.WithEvictionStrategy(() => new RandomEvictionStrategy<string>());
```

### Observer de eventos do cache

```csharp
public sealed class ConsoleLogObserver : ICacheObserver<string>
{
    public void OnHit(string key) => Console.WriteLine($"[cache] hit {key}");
    public void OnMiss(string key) => Console.WriteLine($"[cache] miss {key}");
    public void OnExpire(string key) => Console.WriteLine($"[cache] expirou {key}");
    public void OnEvict(string key) => Console.WriteLine($"[cache] removido {key}");
}

builder.WithObserver(new ConsoleLogObserver());
```

### Handler na cadeia HTTP

Qualquer `DelegatingHandler` entra na cadeia depois de Retry e RateLimit, e vale para Jolpica e Wikipedia.

```csharp
public sealed class ElapsedTimeHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        var response = await base.SendAsync(request, cancellationToken);   // repassa ao próximo elo
        Console.WriteLine($"{request.RequestUri} levou {(DateTime.UtcNow - start).TotalMilliseconds:F0} ms");
        return response;
    }
}

builder.WithHttpHandler(() => new ElapsedTimeHandler());
```

### Enriquecimento próprio

Herde de `DriverDataEnricher` e implemente as etapas. A ordem (Fetch → Validate → Enrich → Normalize) é garantida pela biblioteca, e o resultado continua sendo cacheado.

```csharp
public sealed class LocalBioEnricher(IDriverProvider inner, IReadOnlyDictionary<string, string> bios)
    : DriverDataEnricher(inner)
{
    protected override bool Validate(Driver driver) => bios.ContainsKey(driver.Id);

    protected override Task<Driver> EnrichMediaAsync(Driver driver, CancellationToken cancellationToken) =>
        Task.FromResult(driver with { Description = bios[driver.Id] });

    protected override Driver Normalize(Driver driver) =>
        driver with { Description = driver.Description ?? "Sem biografia." };
}

var bios = new Dictionary<string, string> { ["alonso"] = "Bicampeão mundial (2005 e 2006)." };
builder.WithEnricher(jolpica => new LocalBioEnricher(jolpica, bios));
```

---

## 🗄️ Usando só o cache

O motor de cache é genérico e funciona sozinho, para qualquer tipo de dado:

```csharp
using F1Cache.Core.Caching;
using F1Cache.Core.Caching.Eviction;

var cache = new CacheManager<int, string>(
    defaultTimeToLive: TimeSpan.FromSeconds(30),
    capacity: 100,
    evictionStrategy: new LfuStrategy<int>());

cache.Attach(new MeuObserver());                                        // qualquer ICacheObserver<int>
cache.Set(42, "resposta");                                             // usa o TTL padrão (30 s)
cache.Set(7, "temporário", timeToLive: TimeSpan.FromSeconds(5));       // TTL por item

if (cache.TryGet(42, out var valor))
    Console.WriteLine(valor);
```

Itens expirados são removidos quando são lidos (expiração passiva, como no Redis) e disparam `OnExpire`. A memória fica limitada pela capacidade: ao atingi-la, a estratégia de remoção escolhe quem sai e dispara `OnEvict`.

---

## 🏛️ Arquitetura

Uma consulta passa por estas camadas, da mais externa para a mais interna:

```
F1Client                          Facade: ponto de entrada único
 └─ CachedDriverProvider          Decorator: devolve do cache ou consulta a camada de baixo
     └─ WikipediaDriverEnricher   Template Method: Fetch → Validate → Enrich → Normalize
         ├─ JolpicaDriverProvider     Adapter: JSON da Jolpica → Driver
         └─ WikipediaSummaryProvider  Adapter: JSON da Wikipedia → WikipediaSummary
              └─ HttpClient: Retry → RateLimit → (handlers próprios) → rede    Chain of Responsibility
```

O cache (`CacheManager`) usa uma `IEvictionStrategy` (**Strategy**) para escolher quem remover e avisa os `ICacheObserver` (**Observer**) a cada evento. O `F1ClientBuilder` (**Builder**) monta tudo isso.

| Padrão | Onde | Para quê |
| :--- | :--- | :--- |
| Facade | `F1Client`, `IF1Client` | Esconde providers, cache e HTTP atrás de 4 membros. |
| Builder | `F1ClientBuilder` | Configuração fluente com padrões, em vez de construtores com muitos parâmetros. |
| Strategy | `IEvictionStrategy`: `LruStrategy`, `LfuStrategy`, `FifoStrategy` | Troca o algoritmo de remoção sem mudar o cache. |
| Observer | `ICacheObserver`, `ICacheSubject`, `CacheStatisticsObserver` | Estatísticas e logs sem acoplar ao cache. |
| Decorator | `CachedDriverProvider` | Adiciona cache a qualquer `IDriverProvider`, incluindo *negative caching*. |
| Adapter | `JolpicaDriverProvider`, `WikipediaSummaryProvider` | Converte cada API externa para os modelos da biblioteca. |
| Chain of Responsibility | `RetryHandler`, `RateLimitHandler`, `LoggingHandler` | Cada handler trata a requisição HTTP e/ou repassa ao próximo. |
| Template Method | `DriverDataEnricher`, `WikipediaDriverEnricher` | Ordem fixa das etapas de enriquecimento; subclasses implementam cada etapa. |

### Estrutura

```
src/F1Cache.Core/
  F1Client.cs, IF1Client.cs, F1ClientBuilder.cs
  Caching/            CacheManager, CacheEntry
    Eviction/         LRU, LFU, FIFO
    Observers/        CacheSubject, CacheStatisticsObserver
  Clients/
    Jolpica/          Adapter, DTOs e mapper da Jolpica
    Wikipedia/        Adapter, DTOs e mapper da Wikipedia
  Providers/          CachedDriverProvider
    Enrichment/       DriverDataEnricher, WikipediaDriverEnricher
  Http/               RetryHandler, RateLimitHandler, LoggingHandler
  Interfaces/         Contratos públicos
  Models/             Driver, WikipediaSummary
tests/F1Cache.Tests/  Testes xUnit (sem acesso à rede)
examples/F1Cache.Demo/  Aplicação de console de exemplo
```

As versões dos pacotes NuGet ficam centralizadas em `Directory.Packages.props`.

---

## 🛠️ Como executar

Requisito: **.NET SDK 10.0**.

```bash
dotnet build
dotnet test
```

### Gerar o pacote NuGet

```bash
dotnet pack src/F1Cache.Core -c Release -o ./nupkg
```

Gera `nupkg/F1Cache.1.0.0.nupkg` (DLL, documentação XML e este README). Para usar em outro projeto: `dotnet add package F1Cache --source ./nupkg`, ou referencie `src/F1Cache.Core/F1Cache.Core.csproj` diretamente.

### Demo

Consulta pilotos pelo terminal e mostra os eventos do cache em tempo real.

```bash
# Modo interativo: digite ids de pilotos, "list", "stats", "clear" ou "quit"
dotnet run --project examples/F1Cache.Demo

# Consulta alguns pilotos e sai
dotnet run --project examples/F1Cache.Demo -- alonso hamilton alonso

# Cache de 2 pilotos (para ver remoções) e log de cada requisição HTTP
dotnet run --project examples/F1Cache.Demo -- --capacity 2 --verbose alonso hamilton russell alonso
```

| Opção | Descrição |
| :--- | :--- |
| `--policy <lru\|lfu\|fifo>` | Política de remoção (padrão: `lru`). |
| `--capacity <n>` | Capacidade do cache (padrão: 50). |
| `--verbose`, `-v` | Mostra cada requisição HTTP. |
