FROM node:24-bookworm-slim AS node
FROM mcr.microsoft.com/dotnet/sdk:10.0
ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    PYTHONDONTWRITEBYTECODE=1
RUN apt-get update && apt-get install -y --no-install-recommends \
      python3 python3-venv git curl jq unzip util-linux ca-certificates \
    && rm -rf /var/lib/apt/lists/*
COPY --from=node /usr/local/ /usr/local/
COPY scripts/lab-tools-entrypoint /usr/local/bin/lab-tools-entrypoint
RUN chmod 755 /usr/local/bin/lab-tools-entrypoint
ENTRYPOINT ["/usr/local/bin/lab-tools-entrypoint"]
