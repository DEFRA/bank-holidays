# syntax=docker/dockerfile:1
# Builds DEFRA.BankHolidays per Defra .NET container standards (github.com/DEFRA/defra-docker-dotnetcore).
ARG PARENT_VERSION=2.1.9-dotnet10.0

# Development stage: SDK-based parent image used to restore, test and publish the app.
FROM defradigital/dotnetcore-development:$PARENT_VERSION AS development
ENV TZ="Europe/London"
ARG PARENT_VERSION

# Label the development image with the parent image it extends.
LABEL uk.gov.defra.parent-image=defradigital/dotnetcore-development:${PARENT_VERSION}

# Create directories for app and test files to be copied into.
RUN mkdir -p ./DEFRA.BankHolidays/DEFRA.BankHolidays/ ./DEFRA.BankHolidays/DEFRA.BankHolidays.Tests/

# Install app and test dependencies. Both project files are copied first because the test
#   project references the app project.
COPY --chown=dotnet:dotnet DEFRA.BankHolidays/DEFRA.BankHolidays/*.csproj ./DEFRA.BankHolidays/DEFRA.BankHolidays/
COPY --chown=dotnet:dotnet DEFRA.BankHolidays/DEFRA.BankHolidays.Tests/*.csproj ./DEFRA.BankHolidays/DEFRA.BankHolidays.Tests/
RUN dotnet restore ./DEFRA.BankHolidays/DEFRA.BankHolidays.Tests/DEFRA.BankHolidays.Tests.csproj

# Copy in test and app code.
COPY --chown=dotnet:dotnet DEFRA.BankHolidays/DEFRA.BankHolidays.Tests/ ./DEFRA.BankHolidays/DEFRA.BankHolidays.Tests/
COPY --chown=dotnet:dotnet DEFRA.BankHolidays/DEFRA.BankHolidays/ ./DEFRA.BankHolidays/DEFRA.BankHolidays/

# Build the app.
RUN dotnet publish ./DEFRA.BankHolidays/DEFRA.BankHolidays/ -c Release -o /home/dotnet/out

# Expose the app on a defined port, configurable via a build argument. 8080 matches the
# /health/live and /health/ready probes (see Program.cs) and the Container App ingress in infra/.
ARG PORT=8080
ENV PORT=${PORT}
EXPOSE ${PORT}

# Exec form needs a script to interpolate ${PORT}; the script itself execs dotnet so it still
#   receives signals directly rather than a shell.
# Root-owned so the dotnet user can't modify its own startup script; switch back afterwards.
USER root
COPY --chown=root:root dev-entrypoint.sh ./
RUN chmod +x ./dev-entrypoint.sh
USER dotnet
CMD ["./dev-entrypoint.sh"]

# Production stage
FROM defradigital/dotnetcore:$PARENT_VERSION AS production
ARG PARENT_VERSION

# Label the production image with the parent image it extends.
LABEL uk.gov.defra.parent-image=defradigital/dotnetcore-development:${PARENT_VERSION} \
      org.opencontainers.image.title="DEFRA.BankHolidays" \
      org.opencontainers.image.description="Defra Bank Holidays service - UK bank holiday lookup API" \
      org.opencontainers.image.vendor="DEFRA"

# Expose the app on a defined port, configurable via a build argument.
ARG PORT=8080
ENV ASPNETCORE_URLS=http://*:${PORT} \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE ${PORT}

# Switch to root temporarily to install ICU and lock down file permissions.
USER root

# The production parent image is globalization-invariant and ships without ICU. The app uses
#   the en-GB culture (request localization), which throws at start-up in invariant mode.
RUN apk add --no-cache icu-libs icu-data-full

# Copy the compiled app from the development build stage, assigning root ownership to
#   prevent modification by other users.
COPY --from=development --chown=root:root /home/dotnet/out/ ./

# Remove write permissions, then drop back to the unprivileged user.
RUN chmod -R a-w /home/dotnet
USER dotnet

# Set the default command for production to run the compiled app.
CMD ["dotnet", "DEFRA.BankHolidays.dll"]
