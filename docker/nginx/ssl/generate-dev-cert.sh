#!/usr/bin/env sh
# generate-dev-cert.sh
# Generates a self-signed RSA-2048 TLS certificate for local UPACIP development.
#
# Usage:
#   sh generate-dev-cert.sh [output_dir]
#
# Default output_dir: /etc/nginx/ssl
#
# NOT for production — generated certs are self-signed and must be trusted
# manually in each browser / curl invocation (-k / --insecure).
# In production, supply a CA-signed certificate via a volume mount or
# a secrets manager.

set -e

OUTPUT_DIR="${1:-/etc/nginx/ssl}"

mkdir -p "$OUTPUT_DIR"

openssl req -x509 \
    -nodes \
    -days 365 \
    -newkey rsa:2048 \
    -keyout "$OUTPUT_DIR/server.key" \
    -out    "$OUTPUT_DIR/server.crt" \
    -subj   "/C=US/ST=Dev/L=Dev/O=UPACIP-Dev/CN=localhost"

echo ""
echo "Dev TLS certificate written to: $OUTPUT_DIR"
echo "  Certificate: $OUTPUT_DIR/server.crt"
echo "  Private key: $OUTPUT_DIR/server.key"
echo ""
echo "To trust this certificate in curl, use: curl -k https://localhost/"
