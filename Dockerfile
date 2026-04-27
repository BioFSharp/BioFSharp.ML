FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy-amd64 AS build

WORKDIR /src
COPY . .

RUN dotnet publish src/DPPOP.CLI/DPPOP.CLI.fsproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0-jammy-amd64

RUN apt-get update -y \
    && apt-get install -y --no-install-recommends libnuma1 \
    && rm -rf /var/lib/apt/lists/*

COPY artifacts/legacy-runtime/imlp-1.0.0/legacy-runtime.tar.gz /tmp/legacy-runtime.tar.gz
RUN tar -xzf /tmp/legacy-runtime.tar.gz -C / \
    && rm -f /tmp/legacy-runtime.tar.gz

ENV PATH="/usr/local/cntk/cntk/lib:/usr/local/mpi/bin:${PATH}"
ENV LD_LIBRARY_PATH="/usr/local/cntk/cntk/dependencies/lib:/usr/local/cntk/cntk/lib:/usr/local/mpi/lib:${LD_LIBRARY_PATH}"

WORKDIR /data
COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "DPPOP.CLI.dll"]
