FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["InkMath.AuthService.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 80
EXPOSE 443
ENTRYPOINT ["dotnet", "InkMath.AuthService.dll"]

