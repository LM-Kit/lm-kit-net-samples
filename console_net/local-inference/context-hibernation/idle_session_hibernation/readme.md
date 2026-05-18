# Idle Session Hibernation

Interactive console app that demonstrates `IKVCache.HibernateAsync()`: serialize a populated KV-cache to disk, free RAM/VRAM, then transparently rehydrate on the next turn with zero state loss.

## What it shows

- `MultiTurnConversation` implements `LMKit.Inference.IKVCache`. Cast to that interface to access hibernation.
- `IKVCache.Residency` reports `NotCreated`, `InMemory`, or `Hibernated`.
- `IKVCache.HibernateAsync(filePath = null)` serializes the context. With no path, a file is created in `Configuration.ContextHibernationDirectory`.
- The next `Submit()` after hibernation auto-rehydrates the cache from disk. No code change required.
- `IKVCache.KVCacheContent` exposes a textual projection of the cache (for diagnostics / retrieval).
- Five interactive modes from a menu:
  - **Start**: create a fresh `MultiTurnConversation`.
  - **Ask**: REPL of free-form turns.
  - **Hibernate**: hibernate the current context.
  - **Scripted**: run a 3-turn + hibernate + rehydrate demo.
  - **State**: print residency / working set / KV preview.

## Run

```bash
cd console_net/local-inference/context-hibernation/idle_session_hibernation
dotnet run
```

No command-line arguments. The model (`qwen3.5:0.8b`) loads once at startup. Hibernation files go to `%TEMP%/lmkit-hibernation-demo/`.

## Where this fits

A multi-tenant server with 200 live chat sessions cannot hold 200 KV-caches in memory at once. Hibernation lets you keep an unbounded number of conversations warm on disk and pay the rehydration cost only on rare access. The same pattern lets a phone app survive long backgrounding without rebuilding context.
