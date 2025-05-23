﻿using DigitalWorldOnline.Commons.Models.Chat;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Create
{
    public class CreateChatMessageCommand : IRequest<Unit>
    {
        public ChatMessageModel ChatMessage { get; private set; }

        public CreateChatMessageCommand(ChatMessageModel chatMessage)
        {
            ChatMessage = chatMessage;
        }
    }
}