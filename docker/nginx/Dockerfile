FROM nginx:1.25-alpine

# Install openssl for self-signed dev certificate generation
RUN apk add --no-cache openssl

# Create the SSL directory
RUN mkdir -p /etc/nginx/ssl

# Generate a self-signed RSA-2048 dev certificate at image build time.
# NOT for production — replace with a CA-signed cert via volume mount or
# a secrets manager in any non-development environment (OWASP A02).
RUN openssl req -x509 \
        -nodes \
        -days 365 \
        -newkey rsa:2048 \
        -keyout /etc/nginx/ssl/server.key \
        -out    /etc/nginx/ssl/server.crt \
        -subj   "/C=US/ST=Dev/L=Dev/O=UPACIP-Dev/CN=localhost"

# Copy virtual host config (replaces default nginx welcome page config)
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80 443
