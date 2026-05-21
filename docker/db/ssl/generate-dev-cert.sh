#!/usr/bin/env sh
# generate-dev-cert.sh
# Generates a self-signed RSA-2048 TLS certificate for the UPACIP PostgreSQL service.
#
# Usage:
#   sh generate-dev-cert.sh [output_dir]
#
# Default output_dir: directory containing this script (docker/db/ssl/)
#
# Must be run once before `docker compose up` so that docker-compose.yml can bind-mount
# the generated cert and key into the db container at /ssl/server.crt and /ssl/server.key.
#
# PostgreSQL requires the key file to be readable only by the process owner (0600).
# This script sets those permissions automatically on Linux/macOS.
# On Windows with Docker Desktop (WSL2), run from a WSL2 terminal to preserve permissions.
#
# NOT for production — generated certs are self-signed and must be replaced with
# CA-signed certificates in any environment that handles real PHI.

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUTPUT_DIR="${1:-$SCRIPT_DIR}"

mkdir -p "$OUTPUT_DIR"

openssl req -x509 \
    -nodes \
    -days 365 \
    -newkey rsa:2048 \
    -keyout "$OUTPUT_DIR/server.key" \
    -out    "$OUTPUT_DIR/server.crt" \
    -subj   "/C=US/ST=Dev/L=Dev/O=UPACIP-Dev/CN=db"

# PostgreSQL rejects ssl_key_file if it is readable by group or others (AC-003).
chmod 600 "$OUTPUT_DIR/server.key"
chmod 644 "$OUTPUT_DIR/server.crt"

echo ""
echo "PostgreSQL dev TLS certificate written to: $OUTPUT_DIR"
echo "  Certificate : $OUTPUT_DIR/server.crt  (0644)"
echo "  Private key : $OUTPUT_DIR/server.key  (0600)"
echo ""
echo "Start the stack with: docker compose up --wait"
