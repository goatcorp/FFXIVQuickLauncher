using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection.Emit;
using System.Drawing;

namespace Dalamud.Game.Chat {
    public enum XivChatType : ushort {
        None = 0,
        Debug = 1,
        [XivChatTypeInfo("Urgent", 0xFF9400D3)] Urgent = 2,
        [XivChatTypeInfo("Notice", 0xFF9400D3)] Notice = 3,
        [XivChatTypeInfo("Say", 0xFFFFFFFF)] Say = 10,
        [XivChatTypeInfo("Shout", 0xFFFF4500)] Shout = 11,
        TellOutgoing = 12,
        [XivChatTypeInfo("Tell", 0xFFFF69B4)] TellIncoming = 13,
        [XivChatTypeInfo("Party", 0xFF1E90FF)] Party = 14,
        [XivChatTypeInfo("Alliance", 0xFFFF4500)] Alliance = 15,
        [XivChatTypeInfo("Linkshell 1", 0xFF228B22)] Ls1 = 16,
        [XivChatTypeInfo("Linkshell 2", 0xFF228B22)] Ls2 = 17,
        [XivChatTypeInfo("Linkshell 3", 0xFF228B22)] Ls3 = 18,
        [XivChatTypeInfo("Linkshell 4", 0xFF228B22)] Ls4 = 19,
        [XivChatTypeInfo("Linkshell 5", 0xFF228B22)] Ls5 = 20,
        [XivChatTypeInfo("Linkshell 6", 0xFF228B22)] Ls6 = 21,
        [XivChatTypeInfo("Linkshell 7", 0xFF228B22)] Ls7 = 22,
        [XivChatTypeInfo("Linkshell 8", 0xFF228B22)] Ls8 = 23,
        [XivChatTypeInfo("Free Company", 0xFF00BFFF)] FreeCompany = 24,
        [XivChatTypeInfo("Novice Network", 0xFF8B4513)]NoviceNetwork = 27,
        [XivChatTypeInfo("Yell", 0xFFFFFF00)] Yell = 30,
        [XivChatTypeInfo("Party", 0xFF1E90FF)] CrossParty = 32,
        [XivChatTypeInfo("PvP Team", 0xFFF4A460)] PvPTeam = 36,
        [XivChatTypeInfo("Crossworld Linkshell", 0xFF1E90FF)] CrossLinkShell = 37,
        [XivChatTypeInfo("Echo", 0xFF808080)] Echo = 56,
        SystemError = 58,
        GatheringSystemMessage = 60,
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
        internal XivChatTypeInfoAttribute(string fancyName, uint defaultColor) {
            FancyName = fancyName;
            DefaultColor = defaultColor;
        }
        
        public string FancyName { get; private set; }
        public uint DefaultColor { get; private set; }
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
