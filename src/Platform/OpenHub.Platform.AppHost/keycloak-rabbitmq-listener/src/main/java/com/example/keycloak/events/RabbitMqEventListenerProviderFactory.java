package com.example.keycloak.events;

import com.rabbitmq.client.Channel;
import com.rabbitmq.client.Connection;
import com.rabbitmq.client.ConnectionFactory;
import org.jboss.logging.Logger;
import org.keycloak.Config;
import org.keycloak.events.EventListenerProvider;
import org.keycloak.events.EventListenerProviderFactory;
import org.keycloak.events.EventType;
import org.keycloak.events.admin.ResourceType;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.KeycloakSessionFactory;

import java.io.IOException;
import java.util.Arrays;
import java.util.Collections;
import java.util.EnumSet;
import java.util.Set;
import java.util.concurrent.TimeoutException;

/**
 * Factory that Keycloak instantiates once at server startup.
 * - init()      runs once, reads config, opens the RabbitMQ connection.
 * - create()    is called per-request to hand back a provider instance.
 * - close()     runs on shutdown, cleans up the connection.
 *
 * Connection config comes from environment variables following this pattern:
 *   KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_HOST
 *   KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_PORT
 *   KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_USERNAME
 *   KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_PASSWORD
 *   KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_QUEUE
 *
 * Tenant/user change fan-out (durable fanout exchanges, one per type — this
 * factory only ever declares these exchanges, it never owns a queue on them;
 * each consuming domain declares and binds its own):
 *   KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_TENANT_CHANGED_EXCHANGE
 *     default "tenant.changed" — ResourceType.ORGANIZATION admin events
 *   KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_USER_CHANGED_EXCHANGE
 *     default "user.changed" — ResourceType.USER / ORGANIZATION_MEMBERSHIP admin events
 *
 * Filtering config (optional, comma-separated, case-insensitive). Leave unset
 * or empty to publish everything of that kind:
 *   KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_EVENT_TYPES
 *     e.g. "LOGIN,LOGOUT,REGISTER,LOGIN_ERROR"
 *     (names must match org.keycloak.events.EventType constants)
 *   KC_SPI_EVENTS_LISTENER_RABBITMQ_EVENT_LISTENER_RESOURCE_TYPES
 *     e.g. "USER,ORGANIZATION,ORGANIZATION_MEMBERSHIP"
 *     (names must match org.keycloak.events.admin.ResourceType constants)
 *     Note: this filter is applied before exchange routing — if RESOURCE_TYPES
 *     excludes USER, no user.changed publish happens for USER events either.
 */
public class RabbitMqEventListenerProviderFactory implements EventListenerProviderFactory {

    private static final Logger log = Logger.getLogger(RabbitMqEventListenerProviderFactory.class);

    // This ID is how the realm config (eventsListeners list) refers to this provider.
    public static final String PROVIDER_ID = "rabbitmq-event-listener";

    private Connection connection;
    private Channel channel;
    private String queueName;
    private String tenantChangedExchange;
    private String userChangedExchange;

    // null/empty set = no filter applied = publish everything of that kind
    private Set<EventType> allowedEventTypes = Collections.emptySet();
    private Set<ResourceType> allowedResourceTypes = Collections.emptySet();

    @Override
    public EventListenerProvider create(KeycloakSession session) {
        return new RabbitMqEventListenerProvider(
                channel, queueName, tenantChangedExchange, userChangedExchange,
                allowedEventTypes, allowedResourceTypes);
    }

    @Override
    public void init(Config.Scope config) {
        String host = config.get("host", "localhost");
        int port = config.getInt("port", 5672);
        String username = config.get("username", "guest");
        String password = config.get("password", "guest");
        queueName = config.get("queue", "keycloak-events");
        tenantChangedExchange = config.get("tenant-changed-exchange", "tenant.changed");
        userChangedExchange = config.get("user-changed-exchange", "user.changed");

        allowedEventTypes = parseEventTypes(config.get("event-types", ""));
        allowedResourceTypes = parseResourceTypes(config.get("resource-types", ""));

        if (!allowedEventTypes.isEmpty()) {
            log.infof("RabbitMQ event listener: filtering user events to %s", allowedEventTypes);
        }
        if (!allowedResourceTypes.isEmpty()) {
            log.infof("RabbitMQ event listener: filtering admin events to resource types %s", allowedResourceTypes);
        }

        try {
            ConnectionFactory factory = new ConnectionFactory();
            factory.setHost(host);
            factory.setPort(port);
            factory.setUsername(username);
            factory.setPassword(password);

            connection = factory.newConnection();
            channel = connection.createChannel();
            // durable=true so the queue survives a RabbitMQ restart
            channel.queueDeclare(queueName, true, false, false, null);
            // Fanout exchanges for tenant/user changes — declared here only; queues and
            // bindings on them are each consuming domain's own responsibility.
            channel.exchangeDeclare(tenantChangedExchange, "fanout", true);
            channel.exchangeDeclare(userChangedExchange, "fanout", true);

            log.infof("RabbitMQ event listener connected to %s:%d, queue '%s', exchanges '%s'/'%s'",
                    host, port, queueName, tenantChangedExchange, userChangedExchange);
        } catch (IOException | TimeoutException e) {
            log.error("Failed to connect to RabbitMQ for event listener", e);
        }
    }

    @Override
    public void postInit(KeycloakSessionFactory factory) {
        // no-op for this provider
    }

    @Override
    public void close() {
        try {
            if (channel != null) channel.close();
            if (connection != null) connection.close();
        } catch (IOException | TimeoutException e) {
            log.error("Error closing RabbitMQ connection", e);
        }
    }

    @Override
    public String getId() {
        return PROVIDER_ID;
    }

    private Set<EventType> parseEventTypes(String csv) {
        if (csv == null || csv.isBlank()) {
            return Collections.emptySet();
        }
        EnumSet<EventType> result = EnumSet.noneOf(EventType.class);
        for (String raw : csv.split(",")) {
            String name = raw.trim().toUpperCase();
            if (name.isEmpty()) continue;
            try {
                result.add(EventType.valueOf(name));
            } catch (IllegalArgumentException e) {
                log.warnf("Unknown EventType '%s' in event-types config, ignoring. Valid values: %s",
                        name, Arrays.toString(EventType.values()));
            }
        }
        return result;
    }

    private Set<ResourceType> parseResourceTypes(String csv) {
        if (csv == null || csv.isBlank()) {
            return Collections.emptySet();
        }
        EnumSet<ResourceType> result = EnumSet.noneOf(ResourceType.class);
        for (String raw : csv.split(",")) {
            String name = raw.trim().toUpperCase();
            if (name.isEmpty()) continue;
            try {
                result.add(ResourceType.valueOf(name));
            } catch (IllegalArgumentException e) {
                log.warnf("Unknown ResourceType '%s' in resource-types config, ignoring. Valid values: %s",
                        name, Arrays.toString(ResourceType.values()));
            }
        }
        return result;
    }
}
