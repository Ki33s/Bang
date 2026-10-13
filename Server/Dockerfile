FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY NuGet.Config ./
COPY Assets/Scripts/Core/ ./Assets/Scripts/Core/
COPY Server/Bang.Server.csproj Server/Program.cs ./Server/
RUN dotnet publish Server/Bang.Server.csproj -c Release -o /out
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out ./
ENV BANG_URL=http://0.0.0.0:8081
EXPOSE 8081
ENTRYPOINT ["dotnet", "Bang.Server.dll"]
