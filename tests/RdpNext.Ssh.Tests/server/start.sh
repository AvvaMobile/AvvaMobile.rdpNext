#!/bin/bash
# SSH test sunucusunu açar: ./start.sh [port]. Anahtarlar yoksa keys/ altında üretilir.
set -euo pipefail
cd "$(dirname "$0")"
port="${1:-2222}"
name=rdpnext-ssh-test
mkdir -p keys
[ -f keys/id_ed25519 ] || ssh-keygen -q -t ed25519 -N '' -f keys/id_ed25519
[ -f keys/id_rsa ] || ssh-keygen -q -t rsa -b 3072 -N '' -f keys/id_rsa
[ -f keys/id_ecdsa ] || ssh-keygen -q -t ecdsa -b 256 -N '' -f keys/id_ecdsa
[ -f keys/id_ed25519_pw ] || ssh-keygen -q -t ed25519 -N 'anahtar123' -f keys/id_ed25519_pw
docker image inspect $name >/dev/null 2>&1 || docker build -q -t $name . >/dev/null
docker rm -f $name >/dev/null 2>&1 || true
docker run -d --name $name -p 127.0.0.1:$port:22 $name >/dev/null
cat keys/*.pub | docker exec -i $name sh -c 'cat > /home/deneme/.ssh/authorized_keys && chown deneme:deneme /home/deneme/.ssh/authorized_keys && chmod 600 /home/deneme/.ssh/authorized_keys'
for i in $(seq 1 50); do nc -z 127.0.0.1 $port 2>/dev/null && exit 0; sleep 0.2; done
echo "SSH sunucusu açılmadı" >&2; exit 1
