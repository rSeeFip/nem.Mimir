# Mimir Sandbox Container — Isolated code execution environment
# Minimal Alpine image with Python 3.12 and Node.js 22 for secure sandboxed execution.
# No CMD/ENTRYPOINT — containers are one-shot, created per execution request.

FROM alpine:3.19

# Install Python 3 and Node.js from Alpine repos
RUN apk add --no-cache \
        python3~=3.11 \
        nodejs~=20 \
    && rm -rf /var/cache/apk/*

# Create non-root sandbox user with no shell and no home directory
RUN addgroup -S sandbox && \
    adduser -S -G sandbox -s /sbin/nologin -H sandbox

# Working directory for code execution
WORKDIR /sandbox

# Switch to non-root user
USER sandbox
