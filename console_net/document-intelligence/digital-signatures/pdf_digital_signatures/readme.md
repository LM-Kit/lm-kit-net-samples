# PDF Digital Signatures

Sign, verify, countersign, and tamper-check PDF documents with LM-Kit.NET's digital-signature engine. Everything runs locally and offline: signing appends an incremental revision (PAdES detached CAdES), and validation reports independent verdict axes instead of a single boolean.

## Features

- One-call signing with any `X509Certificate2` whose private key the BCL reaches: PFX files, the OS certificate store, hardware-backed keys.
- Validation on four independent axes: document integrity, signer identity against explicit trust anchors, revocation from embedded material, and RFC 3161 timestamps.
- Countersigning that preserves the earlier signature byte for byte (incremental by construction).
- Tamper demonstration: one flipped byte turns the verdict `INVALID` with `DocumentModified`.
- Optional PAdES B-T: pass `--tsa <url>` to prove the signing time through an RFC 3161 authority.

## Prerequisites

- .NET 8.0 SDK or newer.
- No model download and no GPU: this demo exercises the document engine only.

## How It Works

1. A sample contract PDF is generated from Markdown (or pass your own PDF path as the first argument).
2. A self-signed demo certificate is created with `CertificateRequest`. In production you load a PFX or pick a store certificate instead.
3. `PdfSigner.Sign` appends the signature as an incremental revision: the input bytes survive verbatim inside the output.
4. `PdfSignatureValidator.Validate` reports each signature's axes. Trust is explicit: only the anchors you add to `TrustedRoots` count.
5. A second `Sign` call countersigns; the report then shows signature 1 covering its own revision and signature 2 covering the whole file, both `VALID`.
6. One byte of signed content is flipped and validation flips to `INVALID` with `DocumentModified`.

## Usage

```
dotnet run
dotnet run -- path/to/your.pdf
dotnet run -- --tsa http://timestamp.digicert.com
```

Outputs land in `./output`: `contract.pdf`, `contract-signed.pdf`, `contract-countersigned.pdf`, `contract-tampered.pdf`.

## Example Output

```
Sample contract created: .\output\contract.pdf
Demo signer created: CN=LM-Kit Demo Signer (self-signed)
Signed: .\output\contract-signed.pdf

== After signing ==
Overall: VALID

Signature #1 (ETSI.CAdES.detached)
  Status     : VALID
  Integrity  : Valid
  Identity   : Trusted (CN=LM-Kit Demo Signer)
  Revocation : NotChecked
  Timestamp  : None
  Covers all : True
  Detail     : The document is intact and the signer chains to a trust anchor. ...

== After tampering with one byte ==
Overall: INVALID

Signature #1 (ETSI.CAdES.detached)
  Status     : INVALID
  Integrity  : DocumentModified
  ...
```

## Configuration

- `PdfSigningOptions.Certification` turns the signature into the document's certification signature (DocMDP 1..3).
- `PdfSigningOptions.Bounds` places a visible field on the page. It renders who signed, when, and why, localized through `AppearanceCulture` (the current UI culture by default; fourteen languages built in). `Appearance` overrides the text, size, and color; `Appearance.FontFile` embeds a TrueType/OpenType face so scripts beyond WinAnsi (Cyrillic, CJK, Arabic) render; `FieldAppearance.None` keeps the field empty.
- `PdfSigner.BeginSign` returns a session (byte ranges, digest, `Complete`) for external signing through an HSM or a service; the private key never has to enter the process.
- `PdfSigner.AddDocumentTimestamp` and `PdfSigner.ExtendLtv` append document timestamps and long-term validation material; both keep existing signatures intact.
