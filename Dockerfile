FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as build
LABEL Author = "Tarun Reddy"
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore


COPY . ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1
USER root
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "gsoApi.dll"]


