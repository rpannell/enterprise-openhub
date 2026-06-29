package com.example.keycloak.events;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.rabbitmq.client.Channel;
import org.jboss.logging.Logger;
import org.keycloak.events.Event;
import org.keycloak.events.EventListenerProvider;
import org.keycloak.events.EventType;
import org.keycloak.events.admin.AdminEvent;
import org.keycloak.events.admin.ResourceType;

import java.io.IOException;
import java.util.Set;

/**
 * Keycloak creates one of these per request that triggers an event.
 * onEvent(Event) fires for user-facing events: LOGIN, LOGOUT, REGISTER,
 *   UPDATE_PASSWORD, LOGIN_ERROR, etc.
 * onEvent(AdminEvent, boolean) fires for admin actions: a user created
 *   via the admin console/API, a client updated, an organization or
 *   organization membership changed, etc.
 *
 * allowedEventTypes / allowedResourceTypes come from the factory's SPI
 * config. An empty set means "no filter — publish everything of that kind."
 */
public class RabbitMqEventListenerProvider implements EventListenerProvider {

    private static final Logger log = Logger.getLogger(RabbitMqEventListenerProvider.class);
    private static final ObjectMapper mapper = new ObjectMapper();

    private final Channel channel;
    private final String queueName;
    private final Set<EventType> allowedEventTypes;
    private final Set<ResourceType> allowedResourceTypes;

    public RabbitMqEventListenerProvider(
            Channel channel,
            String queueName,
            Set<EventType> allowedEventTypes,
            Set<ResourceType> allowedResourceTypes) {
        this.channel = channel;
        this.queueName = queueName;
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
        publish(event);
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

    @Override
    public void close() {
        // Channel/connection are shared and owned by the factory — nothing to close here.
    }
}
