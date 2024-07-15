FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build

ARG version=0.0.0-undefined

WORKDIR /app

# Copy everything and build
COPY src/ ./
RUN dotnet publish /p:Version=$version Giraffe.Website/Giraffe.Website.fsproj -c Release -o published

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime

# Change the HTTP port that the server process is listening
# https://learn.microsoft.com/en-us/dotnet/core/compatibility/containers/8.0/aspnet-port
ENV ASPNETCORE_HTTP_PORTS=5000

WORKDIR /app
COPY --from=build /app/published .
ENTRYPOINT ["dotnet", "Giraffe.Website.dll"]