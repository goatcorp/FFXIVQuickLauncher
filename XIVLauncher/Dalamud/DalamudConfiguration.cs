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

        public CustomComboPreset ComboPresets { get; set; }
    }
}
