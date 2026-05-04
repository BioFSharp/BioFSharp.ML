ARG BASE_IMAGE=csbdocker/cntk-dotnet:1.0.1-cntk2.7-dotnet10

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src
COPY . .

RUN dotnet publish src/DPPOP.CLI/DPPOP.CLI.fsproj -c Release -o /app/publish

FROM ${BASE_IMAGE}

WORKDIR /app
COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "DPPOP.CLI.dll"]
