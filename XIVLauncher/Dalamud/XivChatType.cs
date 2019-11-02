using System;
using System.Linq;

namespace Dalamud.Game.Chat
{
    public enum XivChatType : ushort
    {
        None = 0,
        Debug = 1,

        [XivChatTypeInfo("Urgent", 0xFF9400D3)]
        Urgent = 2,

        [XivChatTypeInfo("Notice", 0xFF9400D3)]
        Notice = 3,
        [XivChatTypeInfo("Say", 0xFFFFFFFF)] Say = 10,
        [XivChatTypeInfo("Shout", 0xFFFF4500)] Shout = 11,
        TellOutgoing = 12,
        [XivChatTypeInfo("Tell", 0xFFFF69B4)] TellIncoming = 13,
        [XivChatTypeInfo("Party", 0xFF1E90FF)] Party = 14,

        [XivChatTypeInfo("Alliance", 0xFFFF4500)]
        Alliance = 15,

        [XivChatTypeInfo("Linkshell 1", 0xFF228B22)]
        Ls1 = 16,

        [XivChatTypeInfo("Linkshell 2", 0xFF228B22)]
        Ls2 = 17,

        [XivChatTypeInfo("Linkshell 3", 0xFF228B22)]
        Ls3 = 18,

        [XivChatTypeInfo("Linkshell 4", 0xFF228B22)]
        Ls4 = 19,

        [XivChatTypeInfo("Linkshell 5", 0xFF228B22)]
        Ls5 = 20,

        [XivChatTypeInfo("Linkshell 6", 0xFF228B22)]
        Ls6 = 21,

        [XivChatTypeInfo("Linkshell 7", 0xFF228B22)]
        Ls7 = 22,

        [XivChatTypeInfo("Linkshell 8", 0xFF228B22)]
        Ls8 = 23,

        [XivChatTypeInfo("Free Company", 0xFF00BFFF)]
        FreeCompany = 24,

        [XivChatTypeInfo("Novice Network", 0xFF8B4513)]
        NoviceNetwork = 27,

        [XivChatTypeInfo("Custom Emotes", 0xFF4AE4FF)]
        CustomEmote = 28,
        [XivChatTypeInfo("Standard Emotes", 0xFF4AE4FF)]
        StandardEmote = 29,

        [XivChatTypeInfo("Yell", 0xFFFFFF00)] Yell = 30,
        [XivChatTypeInfo("Party", 0xFF1E90FF)] CrossParty = 32,

        [XivChatTypeInfo("PvP Team", 0xFFF4A460)]
        PvPTeam = 36,

        [XivChatTypeInfo("Crossworld Linkshell 1", 0xFF1E90FF)]
        CrossLinkShell1 = 37,
        [XivChatTypeInfo("Echo", 0xFF808080)] Echo = 56,
        SystemError = 58,
        GatheringSystemMessage = 60,

        [XivChatTypeInfo("Crossworld Linkshell 2", 0xFF1E90FF)]
        CrossLinkShell2 = 101,

        [XivChatTypeInfo("Crossworld Linkshell 3", 0xFF1E90FF)]
        CrossLinkShell3 = 102,

        [XivChatTypeInfo("Crossworld Linkshell 4", 0xFF1E90FF)]
        CrossLinkShell4 = 103,

        [XivChatTypeInfo("Crossworld Linkshell 5", 0xFF1E90FF)]
        CrossLinkShell5 = 104,

        [XivChatTypeInfo("Crossworld Linkshell 6", 0xFF1E90FF)]
        CrossLinkShell6 = 105,

        [XivChatTypeInfo("Crossworld Linkshell 7", 0xFF1E90FF)]
        CrossLinkShell7 = 106,

        [XivChatTypeInfo("Crossworld Linkshell 8", 0xFF1E90FF)]
        CrossLinkShell8 = 107
    }

    public static class XivChatTypeExtensions
    {
        public static XivChatTypeInfoAttribute GetDetails(this XivChatType p)
        {
            return p.GetAttribute<XivChatTypeInfoAttribute>();
        }
    }

    public class XivChatTypeInfoAttribute : Attribute
    {
        internal XivChatTypeInfoAttribute(string fancyName, uint defaultColor)
        {
            FancyName = fancyName;
            DefaultColor = defaultColor;
        }

        public string FancyName { get; }
        public uint DefaultColor { get; }
    }

    public static class EnumExtensions
    {
        public static TAttribute GetAttribute<TAttribute>(this Enum value)
            where TAttribute : Attribute
        {
            var type = value.GetType();
            var name = Enum.GetName(type, value);
            return type.GetField(name) // I prefer to get attributes this way
                .GetCustomAttributes(false)
                .OfType<TAttribute>()
                .SingleOrDefault();
        }
    }
}