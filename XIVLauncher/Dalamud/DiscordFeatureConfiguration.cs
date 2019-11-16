using System;
using System.Collections.Generic;
using Dalamud.Game.Chat;

namespace Dalamud.Discord
{
    public enum ChannelType
    {
        Guild,
        User
    }

    [Serializable]
    public class ChannelConfiguration
    {
        public ChannelType Type { get; set; }

        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
    }

    [Serializable]
    public class ChatTypeConfiguration
    {
        public XivChatType ChatType { get; set; }

        public ChannelConfiguration Channel { get; set; }
        public int Color { get; set; }
    }

    [Serializable]
    public class DiscordFeatureConfiguration
    {
        public string Token { get; set; }

        public bool CheckForDuplicateMessages { get; set; }
        public int ChatDelayMs { get; set; }

        public bool DisableEmbeds { get; set; }

        public List<ChatTypeConfiguration> ChatTypeConfigurations { get; set; }

        public ChannelConfiguration CfNotificationChannel { get; set; }
        public ChannelConfiguration FateNotificationChannel { get; set; }
        public ChannelConfiguration RetainerNotificationChannel { get; set; }
    }
}