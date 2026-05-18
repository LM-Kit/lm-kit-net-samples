# Encrypted GGUF Model Loading (POC)

Demonstrates loading a GGUF model from an encrypted container, with tensor bytes
decrypted on the fly directly into native memory.

The full plaintext GGUF is **never** held in memory nor written to disk:

1. On encrypt, the plaintext GGUF is streamed (64 KB at a time) through
   AES-256-CTR into an output `.lmke` container.
2. On load, only the GGUF metadata block (typically a few MB) is decrypted
   into a pinned managed buffer. The native runtime then invokes a managed
   read callback once per tensor; each call decrypts just that tensor's bytes
   from disk into the destination buffer and returns.

## Usage

```
encrypted_model_loading encrypt    <plaintext.gguf> <output.lmke> <password>
encrypted_model_loading load       <input.lmke>     <password>    [prompt]
encrypted_model_loading roundtrip  <plaintext.gguf> <password>    [prompt]
```

## How it works

### Container format (AES-256-CTR scheme)

```
[ 0..  4)  Magic "LMKE"
[ 4..  8)  Format version (uint32 LE)
[ 8.. 12)  Encryption scheme (uint32 LE)
[12.. 28)  PBKDF2 salt (16 bytes, random)
[28.. 44)  AES-CTR nonce (16 bytes, random, initial counter block)
[44.. 48)  PBKDF2 iteration count (uint32 LE)
[48.. 56)  Plaintext total size (uint64 LE)
[56.. 64)  Plaintext metadata size (uint64 LE)
[64..  N)  AES-CTR ciphertext of the full plaintext GGUF
```

Because CTR is a stream cipher, container byte offset `P + 64` corresponds to
plaintext byte offset `P`, so the GGUF tensor offsets embedded in the
(decrypted) metadata block remain valid for seeking.

### Load flow

```
EncryptedGguf.Reader.Open(path, password)
     |
     |-- read 64-byte header, derive AES key via PBKDF2-SHA256
     |
     v
reader.GetMetadataBytes()                              <-- decrypts first N bytes
     |                                                     (N = metadata size from header)
     v
pinned byte[] passed to native lmkit_model_load_encrypted
     |
     v
gguf_init_from_memory()    (parses metadata in native)
     |
     v
llama_model_init_from_user(gguf_ctx, set_tensor_data_cb)
     |
     v   <-- native calls back per tensor:
set_tensor_data_cb(tensor):
   name = ggml_get_name(tensor)
   size = ggml_nbytes(tensor)
   offs = gguf_get_data_offset + gguf_get_tensor_offset(name)
   -> managed read callback:
         reader.ReadDecrypted(offs, Span<byte>(tensor->data, size))
              |
              seeks encrypted file to (64 + offs), reads `size` ciphertext bytes,
              generates AES-CTR keystream for that byte range, XORs in place.
```

### Security choices

- **PBKDF2-HMAC-SHA256** with a default of 100k iterations for password-to-key
  derivation. Override by changing the constant in `EncryptedGguf.cs`.
- **AES-256-CTR** because it is seekable. Each tensor can be decrypted
  independently without processing earlier blocks, which is required for the
  per-tensor callback model.
- **No authentication tag** in this POC. For production, wrap the container
  with an HMAC-SHA256 over (header + ciphertext) or use AES-256-GCM-SIV per
  chunk. Skipped here to keep the POC streaming and small.

## Build status

This POC depends on a new native export `lmkit_model_load_encrypted` added to
`LM-Kit.Native/llama.cpp/lmkit/lmkit-encrypted.cpp`. Rebuild the native
library before running the demo.
