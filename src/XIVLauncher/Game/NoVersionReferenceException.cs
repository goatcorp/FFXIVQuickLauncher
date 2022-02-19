using System;
using XIVLauncher.Common;

namespace XIVLauncher.Game
{
    public class NoVersionReferenceException : Exception
    {
        public NoVersionReferenceException(Repository repo, string version) : base($"No version reference found for {repo}({version})")
        {
        }
    }
}