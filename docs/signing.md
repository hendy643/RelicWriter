# Code signing

The release workflow signs `RelicWriter.exe` with our own **self-signed** certificate. Read what that does and does not give you:

- **It does:** put an Authenticode signature and a timestamp in the exe, so a changed file is detectable, the same publisher identity (`CN=RelicWriter, O=hendy643`) is on every release, and anyone who chooses to trust the certificate sees a valid signature.
- **It does not:** make Windows trust the exe. The certificate is not from a certificate authority, so SmartScreen still warns ("Windows protected your PC") and the publisher shows as unknown. Only a certificate from a trusted CA (or SignPath's free open-source one, which was the first plan) removes that, and SmartScreen reputation takes downloads to build.

Signing is skipped, without failing the release, while the two secrets below do not exist.

## Sigstore signatures (cosign)

Separately, and for every release file, the publish job makes a keyless [cosign](https://github.com/sigstore/cosign) signature: `RelicWriter-<version>-win-x64.zip.sigstore.json` and `RelicWriter-<version>-linux-x86_64.flatpak.sigstore.json` are attached to the release. There is no key to keep: the job's GitHub identity (`id-token: write`) gets a short-lived certificate from Sigstore, and the signature is recorded in the public Rekor transparency log. The job verifies each bundle right after making it, so a release does not publish with a bad one. This proves a file was built by this repository's Release workflow; it does nothing for Windows or SmartScreen.

To check a download (needs [cosign](https://docs.sigstore.dev/cosign/system_config/installation/)), with the file and its `.sigstore.json` in the same folder:

```
cosign verify-blob --bundle RelicWriter-1.1.0-win-x64.zip.sigstore.json \
  --certificate-identity-regexp '^https://github.com/hendy643/RelicWriter/\.github/workflows/release\.yml@' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com \
  RelicWriter-1.1.0-win-x64.zip
```

It prints `Verified OK`. Not tested yet: the step has not run on GitHub, and `sigstore/cosign-installer@v3` installs whatever cosign version it currently defaults to (the `--bundle` flag works in 2.x and 3.x).

## Setup (once, by the owner)

1. Run `packaging/windows/make-cert.sh` on your own machine. It writes to `~/.relicwriter-signing/` (outside the repository): `RelicWriter-signing.pfx` (the private key, secret), `RelicWriter-signing.password` (secret) and `RelicWriter-signing.cer` (public). It makes a 3072-bit RSA key valid for five years with the code-signing usage, and prints the thumbprint and the two commands for the next step.
2. Put the secrets in GitHub with the `gh secret set` commands it prints (`SIGNING_PFX_BASE64`, `SIGNING_PFX_PASSWORD`), then delete the password file. Keep a backup of the `.pfx`: if it is lost, the next certificate is a new identity.
3. The public certificate is committed as `packaging/windows/RelicWriter-signing.cer`. Its SHA-1 thumbprint is `AD:FB:FF:88:11:80:B5:65:AB:A2:47:BB:DD:85:B2:72:65:E2:37:65` and its SHA-256 fingerprint `76:92:E3:0D:73:00:D6:CC:33:40:FC:E3:0E:33:9C:9C:A2:B8:EA:1E:98:AE:14:D2:7A:A1:06:63:FD:30:E4:22`; compare these with the exe's Digital Signatures tab (Details, then View Certificate) before trusting it.

The private key must never be committed or pasted into an issue or a chat. Anyone holding the `.pfx` can sign files as RelicWriter; if it leaks, delete the secrets, make a new certificate and say so in the release notes.

## What the workflow does

In the `windows` job of `.github/workflows/release.yml`, after `packaging/windows/build.sh`: write the `.pfx` from the secret to the runner's temp folder, run `signtool sign` (SHA-256, timestamped with DigiCert's free timestamp server, so the signature outlives the certificate), delete the `.pfx`, and check the exe has a signer and a timestamp. The publish job then zips the signed exe.

## For users who want the signature to verify

Download `packaging/windows/RelicWriter-signing.cer` from the repository, then (as the user, not administrator) import it into **Current User, Trusted Publishers** and **Trusted Root Certification Authorities** (double-click the file, Install Certificate). The exe's Properties, Digital Signatures tab then shows a valid signature. Only do this if you trust the certificate's source: trusting it as a root means files signed with it are trusted. SmartScreen's download reputation check is separate and may still warn.

## Not tested

- `make-cert.sh` was run on Linux and its output inspected (subject, code-signing usage). Whether `signtool` accepts the `.pfx` on a Windows runner, and whether the workflow step works, has not been tried.
- Signing a single-file .NET exe after publishing is supported, but the signed exe has not been run.
- The timestamp server URL is DigiCert's public one; change it in the workflow if it is retired.
- Only the exe is signed: the zip and the Flatpak are not.
