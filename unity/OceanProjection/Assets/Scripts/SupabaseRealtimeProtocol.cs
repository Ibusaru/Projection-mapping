using System;
using UnityEngine;

internal static class SupabaseRealtimeProtocol
{
    public static string BuildJoinMessage(string topic, string reference)
    {
        JoinMessage message = new JoinMessage
        {
            topic = topic,
            @event = "phx_join",
            payload = new JoinPayload
            {
                config = new JoinConfig
                {
                    broadcast = new BroadcastConfig
                    {
                        ack = false,
                        self = false
                    },
                    presence = new PresenceConfig
                    {
                        enabled = false
                    },
                    postgres_changes = new[]
                    {
                        new PostgresChangeConfig
                        {
                            @event = "*",
                            schema = "public",
                            table = "fishes"
                        }
                    },
                    @private = false
                }
            },
            @ref = reference,
            join_ref = reference
        };

        return JsonUtility.ToJson(message);
    }

    public static Envelope ParseEnvelope(string messageJson)
    {
        return JsonUtility.FromJson<Envelope>(messageJson);
    }

    [Serializable]
    private sealed class JoinMessage
    {
        public string topic;
        public string @event;
        public JoinPayload payload;
        public string @ref;
        public string join_ref;
    }

    [Serializable]
    private sealed class JoinPayload
    {
        public JoinConfig config;
    }

    [Serializable]
    private sealed class JoinConfig
    {
        public BroadcastConfig broadcast;
        public PresenceConfig presence;
        public PostgresChangeConfig[] postgres_changes;
        public bool @private;
    }

    [Serializable]
    private sealed class BroadcastConfig
    {
        public bool ack;
        public bool self;
    }

    [Serializable]
    private sealed class PresenceConfig
    {
        public bool enabled;
    }

    [Serializable]
    private sealed class PostgresChangeConfig
    {
        public string @event;
        public string schema;
        public string table;
    }

    [Serializable]
    internal sealed class Envelope
    {
        public string @event;
        public string @ref;
        public Payload payload;
    }

    [Serializable]
    internal sealed class Payload
    {
        public string status;
        public string extension;
        public string message;
        public Response response;
    }

    [Serializable]
    internal sealed class Response
    {
        public string reason;
        public PostgresSubscription[] postgres_changes;
    }

    [Serializable]
    internal sealed class PostgresSubscription
    {
        public long id;
        public string @event;
        public string schema;
        public string table;
    }
}
