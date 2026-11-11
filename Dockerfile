# Imagen de SaveStock: la web (API + páginas) y el enviador de correos en la misma imagen.
# Se construye en dos etapas: la primera compila con el SDK y la segunda solo lleva lo publicado.

# --- Etapa 1: compilar y publicar ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS compilacion
WORKDIR /src

# Primero solo los proyectos: si no cambian, Docker reutiliza la capa del restore.
COPY Directory.Build.props ./
COPY src/SaveStock.Core/SaveStock.Core.csproj src/SaveStock.Core/
COPY src/SaveStock.Inventario/SaveStock.Inventario.csproj src/SaveStock.Inventario/
COPY src/SaveStock.Web/SaveStock.Web.csproj src/SaveStock.Web/
COPY src/SaveStock.Enviador/SaveStock.Enviador.csproj src/SaveStock.Enviador/
RUN dotnet restore src/SaveStock.Web/SaveStock.Web.csproj \
    && dotnet restore src/SaveStock.Enviador/SaveStock.Enviador.csproj

COPY src/ src/
RUN dotnet publish src/SaveStock.Web/SaveStock.Web.csproj -c Release --no-restore -o /publicado/web \
    && dotnet publish src/SaveStock.Enviador/SaveStock.Enviador.csproj -c Release --no-restore -o /publicado/enviador

# --- Etapa 2: imagen final, sin SDK ni código fuente ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# sqlite3 sirve para leer la base desde el contenedor, como indica el README.
RUN apt-get update \
    && apt-get install -y --no-install-recommends sqlite3 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=compilacion /publicado/web ./
COPY --from=compilacion /publicado/enviador ./enviador/

# La web escucha en el puerto 5080 dentro del contenedor.
ENV ASPNETCORE_HTTP_PORTS=5080
EXPOSE 5080

# Ninguna clave ni variable de configuración vive aquí: llegan desde el .env (RD-10).
ENTRYPOINT ["dotnet", "SaveStock.Web.dll"]
