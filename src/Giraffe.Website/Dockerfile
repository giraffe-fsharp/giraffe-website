FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS build

ARG version=0.0.0-undefined

WORKDIR /app

# Copy everything and build
COPY src/ ./
RUN dotnet publish /p:Version=$version Giraffe.Website/Giraffe.Website.fsproj -c Release -o published

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:5.0-alpine AS runtime

WORKDIR /app
COPY --from=build /app/published .
ENTRYPOINT ["dotnet", "Giraffe.Website.dll"]