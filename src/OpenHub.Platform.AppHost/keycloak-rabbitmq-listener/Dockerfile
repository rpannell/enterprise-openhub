# syntax=docker/dockerfile:1

# --- Stage 1: build the provider jar with Maven ---
FROM maven:3.9-eclipse-temurin-17 AS build
WORKDIR /build

# Resolve dependencies first, in their own layer. This layer is only
# invalidated when pom.xml itself changes — NOT when you edit Java source.
# That means dependency downloads get cached across builds, even while
# you're actively iterating on the listener code.
COPY pom.xml .
RUN --mount=type=cache,target=/root/.m2 mvn -q dependency:go-offline

# Now copy source and build. Only this layer re-runs when .java files
# change, and the dependency cache mount means it doesn't re-download
# anything — just compiles your (small) set of classes.
COPY src ./src
RUN --mount=type=cache,target=/root/.m2 mvn -q clean package -DskipTests

# --- Stage 2: the real Keycloak image, with our jar dropped into providers/ ---
# This tag MUST match the keycloak.version in pom.xml.
FROM quay.io/keycloak/keycloak:26.0.7
COPY --from=build /build/target/rabbitmq-event-listener.jar /opt/keycloak/providers/rabbitmq-event-listener.jar
