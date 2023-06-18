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

public abstract class Runner
{
    public abstract string RunnerType { get; }

    public string Folder { get; }

    public string DownloadUrl { get; }

    protected DirectoryInfo Prefix;

    protected DirectoryInfo ToolFolder; 

    public Dictionary<string, string> Environment { get; }


    protected Runner(string folder, string url, DirectoryInfo prefix, Dictionary<string, string> env = null)
    {
        Folder = folder;
        DownloadUrl = url;
        Prefix = prefix;
        Environment =  env ?? new Dictionary<string, string>();
    }

    public abstract Task Install();

    protected bool IsDirectoryEmpty(string folder)
    {
        if (!Directory.Exists(folder)) return true;
        return !Directory.EnumerateFileSystemEntries(folder).Any();
    }

    public virtual string GetCommand()
    {
        return "";
    }

    public virtual string GetServer()
    {
        return "";
    }

    public virtual string GetParameters()
    {
        return "";
    }

    public virtual string GetPathCommand()
    {
        return "";
    }

    public virtual string GetPathParameters(string unixPath)
    {
        return unixPath;
    }
}