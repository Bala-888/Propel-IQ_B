#!/bin/bash
# Generate self-signed TLS certificate for local development.
# For production, replace with certs from Let's Encrypt or your CA.
set -e

CERT_DIR="$(dirname "$0")"

openssl req -x509 \
  -newkey rsa:4096 \
  -keyout "${CERT_DIR}/server.key" \
  -out "${CERT_DIR}/server.crt" \
  -sha256 \
  -days 365 \
  -nodes \
  -subj "/C=US/ST=Local/L=Local/O=UPACIP/CN=localhost" \
  -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"

echo "Self-signed certificate generated at ${CERT_DIR}/"
echo "For production: replace server.key and server.crt with your CA-issued certificate."
