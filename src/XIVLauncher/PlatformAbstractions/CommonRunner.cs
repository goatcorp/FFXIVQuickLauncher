using System.Collections.Generic;
using System.Diagnostics;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.PlatformAbstractions
{
    public class CommonRunner : IRunner
    {
        private static CommonRunner instance;

        public static CommonRunner Instance
        {
            get
            {
                instance ??= new CommonRunner();
                return instance;
            }
        }

        public Process Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, bool runas)
        {
            throw new System.NotImplementedException();
        }
    }
}