using OpenHub.RealtimeMessaging.Api.Managers.Models;
using OpenHub.RealtimeMessaging.Api.Models.Requests;
using Riok.Mapperly.Abstractions;

namespace OpenHub.RealtimeMessaging.Api.Mappers;

[Mapper]
public partial class MessageMapper
{
    public partial SendMessageDto ToDto(SendMessageRequest request);
}
