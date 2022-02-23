using System.Collections.Generic;
using System.Diagnostics;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.PlatformAbstractions
{
    public class CommonRunner : IRunner
    {
        private static CommonRunner _instance;
        
        public static CommonRunner Instance
        {
            get
            {
                _instance ??= new CommonRunner();
                return _instance;
            }
        }
        
        public Process Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, bool runas)
        {
            throw new System.NotImplementedException();
        }
    }
}