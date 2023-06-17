using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility;

public class Runner
{
    public string Folder { get; private set; }

    public string DownloadUrl { get; private set; }

    public Dictionary<string, string> Environment { get; private set; }

    public Runner(string folder, string url, Dictionary<string, string> env = null)
    {
        Folder = folder;
        DownloadUrl = url;
        Environment =  env ?? new Dictionary<string, string>();
    }
}