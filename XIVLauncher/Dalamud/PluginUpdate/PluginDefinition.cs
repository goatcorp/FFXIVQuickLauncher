using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.Dalamud.PluginUpdate
{
    public class PluginDefinition
    {
        public string Author { get; set; }
        public string Name { get; set; }
        public string InternalName { get; set; }
        public string AssemblyVersion { get; set; }
        public string Description { get; set; }
    }
}
