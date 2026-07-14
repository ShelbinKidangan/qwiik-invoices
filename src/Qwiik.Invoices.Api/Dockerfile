FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore before copying source so the layer caches across code-only changes.
COPY Qwiik.Invoices.Api.csproj ./
RUN dotnet restore Qwiik.Invoices.Api.csproj

COPY . .
RUN dotnet publish Qwiik.Invoices.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "Qwiik.Invoices.Api.dll"]
