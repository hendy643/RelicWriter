#!/usr/bin/env bash
# Makes the self-signed code-signing certificate the release workflow signs RelicWriter.exe with (docs/signing.md).
#   RelicWriter-signing.pfx   the private key and certificate. SECRET: never commit it. It goes into the GitHub secrets below.
#   RelicWriter-signing.cer   the public certificate. Safe to publish; users can trust it to make the signature verify.
# Run it yourself, on your own machine, so the private key never leaves it except into GitHub's secret store.
# Usage: packaging/windows/make-cert.sh [output dir]    (default ~/.relicwriter-signing; refuses to overwrite)
set -euo pipefail
out="${1:-$HOME/.relicwriter-signing}"
[ ! -e "$out/RelicWriter-signing.pfx" ] || { echo "$out/RelicWriter-signing.pfx exists; move it away first (a new certificate means a new identity)." >&2; exit 1; }
mkdir -p "$out"; chmod 700 "$out"
cd "$out"

password="$(openssl rand -base64 24)"
cat > openssl.cnf <<CNF
[req]
distinguished_name = dn
x509_extensions = ext
prompt = no
[dn]
CN = RelicWriter
O = hendy643
[ext]
basicConstraints = critical, CA:false
keyUsage = critical, digitalSignature
extendedKeyUsage = critical, codeSigning
subjectKeyIdentifier = hash
CNF
openssl req -x509 -newkey rsa:3072 -sha256 -days 1825 -nodes -config openssl.cnf -keyout key.pem -out cert.pem
# 3DES/SHA1 for the container only, because Windows' older importers cannot read OpenSSL 3's default; the key itself is RSA 3072.
openssl pkcs12 -export -inkey key.pem -in cert.pem -name RelicWriter -out RelicWriter-signing.pfx -passout "pass:$password" \
    -keypbe PBE-SHA1-3DES -certpbe PBE-SHA1-3DES -macalg sha1
openssl x509 -in cert.pem -outform der -out RelicWriter-signing.cer
rm key.pem cert.pem openssl.cnf
chmod 600 RelicWriter-signing.pfx

printf '%s' "$password" > RelicWriter-signing.password; chmod 600 RelicWriter-signing.password
echo "Written to $out: RelicWriter-signing.pfx (secret), RelicWriter-signing.password (secret), RelicWriter-signing.cer (public)."
echo "Thumbprint: $(openssl x509 -inform der -in RelicWriter-signing.cer -noout -fingerprint -sha1 | cut -d= -f2)"
echo
echo "Now put the two secrets in GitHub (from the repository folder), then delete the password file:"
echo "  base64 -w0 \"$out/RelicWriter-signing.pfx\" | gh secret set SIGNING_PFX_BASE64"
echo "  gh secret set SIGNING_PFX_PASSWORD < \"$out/RelicWriter-signing.password\""
echo "Keep a backup of the .pfx somewhere safe: losing it means a new certificate."
