# PandaHunt container image (used by Fly.io: `fly deploy` from the repo root).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Restore together with the sources: restoring from the .csproj alone skips the package that carries
# _framework/blazor.web.js, and then no page is interactive.
COPY PandaHunt/ PandaHunt/
RUN dotnet publish PandaHunt/PandaHunt.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# The app listens on 8080 (the image default). The SQLite file and the login keys live on the volume at /data
# (paths are set in fly.toml). Fly mounts the volume owned by root, so the container starts as root, hands /data
# to the unprivileged "app" user and then runs the app as that user.
USER root
ENTRYPOINT ["/bin/sh", "-c", "mkdir -p /data && chown -R app:app /data && export HOME=/home/app && exec setpriv --reuid=app --regid=app --init-groups dotnet PandaHunt.dll"]
