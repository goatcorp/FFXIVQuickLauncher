using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Discord;
using XIVLauncher.Dalamud;

namespace Dalamud
{
    [Serializable]
    class DalamudConfiguration
    {
        public DiscordFeatureConfiguration DiscordFeatureConfig { get; set; }

        public bool OptOutMbCollection { get; set; } = false;

        public List<string> BadWords { get; set; }

        public enum PreferredRole
        {
            None,
            All,
            Tank,
            Dps,
            Healer
        }

        public Dictionary<int, PreferredRole> PreferredRoleReminders { get; set; }

        public string LastVersion { get; set; }

        public Dictionary<string, object> PluginConfigurations { get; set; }

        public bool WelcomeGuideDismissed;
    }
}
