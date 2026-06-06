# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ./Migracion_a_C/WebApplication1 ./Migracion_a_C/WebApplication1

RUN dotnet restore ./Migracion_a_C/WebApplication1/WebApplication1/WebApplication1.csproj

RUN dotnet publish ./Migracion_a_C/WebApplication1/WebApplication1/WebApplication1.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "WebApplication1.dll"]