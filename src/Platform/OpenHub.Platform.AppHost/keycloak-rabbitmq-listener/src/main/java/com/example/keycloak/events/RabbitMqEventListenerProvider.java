package com.example.keycloak.events;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.rabbitmq.client.Channel;
import org.jboss.logging.Logger;
import org.keycloak.events.Event;
import org.keycloak.events.EventListenerProvider;
import org.keycloak.events.EventType;
import org.keycloak.events.admin.AdminEvent;
import org.keycloak.events.admin.OperationType;
import org.keycloak.events.admin.ResourceType;

import java.io.IOException;
import java.time.Instant;
import java.util.Set;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * Keycloak creates one of these per request that triggers an event.
 * onEvent(Event) fires for user-facing events: LOGIN, LOGOUT, REGISTER,
 *   UPDATE_PASSWORD, LOGIN_ERROR, etc. Unchanged by the tenant/user fan-out work
 *   below — still published as raw Keycloak JSON to the single keycloak-events queue.
 * onEvent(AdminEvent, boolean) fires for admin actions: a user created via the
 *   admin console/API, a client updated, an organization or organization
 *   membership changed, etc. USER/ORGANIZATION/ORGANIZATION_MEMBERSHIP resource
 *   types are now routed to per-type durable fanout exchanges (tenant.changed /
 *   user.changed) instead of the keycloak-events queue, as a slimmed custom JSON
 *   DTO — see TenantChangedEventDto / UserChangedEventDto.
 *
 * allowedEventTypes / allowedResourceTypes come from the factory's SPI
 * config. An empty set means "no filter — publish everything of that kind."
 */
public class RabbitMqEventListenerProvider implements EventListenerProvider {

    private static final Logger log = Logger.getLogger(RabbitMqEventListenerProvider.class);
    private static final ObjectMapper mapper = new ObjectMapper();

    // Matches "organizations/{orgId}" and "organizations/{orgId}/members/{userId}"
    private static final Pattern ORGANIZATION_PATH =
            Pattern.compile("^organizations/([^/]+)(?:/members/([^/]+))?");
    // Matches "users/{userId}"
    private static final Pattern USER_PATH = Pattern.compile("^users/([^/]+)");

    private final Channel channel;
    private final String queueName;
    private final String tenantChangedExchange;
    private final String userChangedExchange;
    private final Set<EventType> allowedEventTypes;
    private final Set<ResourceType> allowedResourceTypes;

    public RabbitMqEventListenerProvider(
            Channel channel,
            String queueName,
            String tenantChangedExchange,
            String userChangedExchange,
            Set<EventType> allowedEventTypes,
            Set<ResourceType> allowedResourceTypes) {
        this.channel = channel;
        this.queueName = queueName;
        this.tenantChangedExchange = tenantChangedExchange;
        this.userChangedExchange = userChangedExchange;
        this.allowedEventTypes = allowedEventTypes;
        this.allowedResourceTypes = allowedResourceTypes;
    }

    @Override
    public void onEvent(Event event) {
        if (!allowedEventTypes.isEmpty() && !allowedEventTypes.contains(event.getType())) {
            return; // filtered out
        }
        publish(event);
    }

    @Override
    public void onEvent(AdminEvent event, boolean includeRepresentation) {
        if (!allowedResourceTypes.isEmpty() && !allowedResourceTypes.contains(event.getResourceType())) {
            return; // filtered out
        }

        String exchange = resolveExchange(event.getResourceType());
        if (exchange == null) {
            log.debugf("No fan-out exchange mapped for resourceType=%s, dropping", event.getResourceType());
            return;
        }

        Object dto = buildDto(event);
        if (dto == null) {
            log.warnf("Could not derive a fan-out DTO for resourceType=%s resourcePath=%s, dropping",
                    event.getResourceType(), event.getResourcePath());
            return;
        }

        publishToExchange(exchange, dto);
    }

    private String resolveExchange(ResourceType resourceType) {
        switch (resourceType) {
            case ORGANIZATION:
                return tenantChangedExchange;
            case USER:
            case ORGANIZATION_MEMBERSHIP:
                return userChangedExchange;
            default:
                return null; // no fan-out mapping for this resource type
        }
    }

    private Object buildDto(AdminEvent event) {
        String operation = mapOperation(event.getOperationType());
        String occurredAt = Instant.ofEpochMilli(event.getTime()).toString();
        JsonNode representation = parseRepresentation(event);

        switch (event.getResourceType()) {
            case ORGANIZATION: {
                Matcher m = ORGANIZATION_PATH.matcher(nullToEmpty(event.getResourcePath()));
                if (!m.find() || m.group(1) == null) return null;

                TenantChangedEventDto dto = new TenantChangedEventDto();
                dto.TenantId = m.group(1);
                dto.RealmSlug = event.getRealmId();
                dto.Operation = operation;
                dto.OccurredAtUtc = occurredAt;
                dto.Name = textOrNull(representation, "name");
                return dto;
            }
            case USER: {
                Matcher m = USER_PATH.matcher(nullToEmpty(event.getResourcePath()));
                if (!m.find() || m.group(1) == null) return null;

                UserChangedEventDto dto = new UserChangedEventDto();
                dto.UserId = m.group(1);
                dto.TenantId = null; // not resolvable from a plain USER admin event
                dto.RealmSlug = event.getRealmId();
                dto.Operation = operation;
                dto.OccurredAtUtc = occurredAt;
                dto.Email = textOrNull(representation, "email");
                dto.FirstName = textOrNull(representation, "firstName");
                dto.LastName = textOrNull(representation, "lastName");
                return dto;
            }
            case ORGANIZATION_MEMBERSHIP: {
                Matcher m = ORGANIZATION_PATH.matcher(nullToEmpty(event.getResourcePath()));
                if (!m.find() || m.group(1) == null || m.group(2) == null) return null;

                UserChangedEventDto dto = new UserChangedEventDto();
                dto.TenantId = m.group(1);
                dto.UserId = m.group(2);
                dto.RealmSlug = event.getRealmId();
                dto.Operation = operation;
                dto.OccurredAtUtc = occurredAt;
                dto.Email = textOrNull(representation, "email");
                dto.FirstName = textOrNull(representation, "firstName");
                dto.LastName = textOrNull(representation, "lastName");
                return dto;
            }
            default:
                return null;
        }
    }

    private static String mapOperation(OperationType operationType) {
        if (operationType == null) return "Updated";
        switch (operationType) {
            case CREATE: return "Created";
            case DELETE: return "Deleted";
            case UPDATE:
            case ACTION:
            default: return "Updated";
        }
    }

    private JsonNode parseRepresentation(AdminEvent event) {
        String raw = event.getRepresentation();
        if (raw == null || raw.isBlank()) return null;
        try {
            return mapper.readTree(raw);
        } catch (IOException e) {
            log.warnf("Failed to parse admin event representation JSON, continuing without it: %s", e.getMessage());
            return null;
        }
    }

    private static String textOrNull(JsonNode node, String field) {
        if (node == null) return null;
        JsonNode value = node.get(field);
        return (value == null || value.isNull()) ? null : value.asText();
    }

    private static String nullToEmpty(String s) {
        return s == null ? "" : s;
    }

    private void publish(Object event) {
        if (channel == null) {
            log.warn("RabbitMQ channel unavailable, dropping Keycloak event");
            return;
        }
        try {
            byte[] body = mapper.writeValueAsBytes(event);
            channel.basicPublish("", queueName, null, body);
        } catch (IOException e) {
            log.error("Failed to publish Keycloak event to RabbitMQ", e);
        }
    }

    private void publishToExchange(String exchange, Object payload) {
        if (channel == null) {
            log.warn("RabbitMQ channel unavailable, dropping event for exchange " + exchange);
            return;
        }
        try {
            byte[] body = mapper.writeValueAsBytes(payload);
            // Empty routing key — fanout exchanges ignore it, every bound queue gets a copy.
            channel.basicPublish(exchange, "", null, body);
        } catch (IOException e) {
            log.error("Failed to publish to exchange " + exchange, e);
        }
    }

    @Override
    public void close() {
        // Channel/connection are shared and owned by the factory — nothing to close here.
    }
}
