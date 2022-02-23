/**
 * This file is part of AriaNet by huming2207, licensed under the CC-BY-NC-SA 3.0 Australian Licence.
 * You can find the original code in this GitHub repository: https://github.com/huming2207/AriaNet
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AriaNet.Attributes;
using XIVLauncher.Common.Game.Patch.Acquisition.Aria.JsonRpc;

namespace AriaNet
{
    public class AriaManager
    {
        private readonly JsonRpcHttpClient rpcClient;
        private readonly string secret;
        
        public AriaManager(string secret, string rpcUrl = "http://localhost:6800/jsonrpc")
        {
            this.secret = secret;
            this.rpcClient = new JsonRpcHttpClient(rpcUrl);
        }

        private async Task<T> Invoke<T>(string method, params object[] arguments)
        {
            var args = new object[arguments.Length + 1];
            args[0] = $"token:{this.secret}";
            Array.Copy(arguments, 0, args, 1, arguments.Length);

            return await this.rpcClient.Invoke<T>(method, args);
        }

        public async Task<string> AddUri(List<string> uriList)
        {
            return await Invoke<string>("aria2.addUri", uriList);
        }

        public async Task<string> AddUri(List<string> uriList, string userAgent, string referrer)
        {
            return await Invoke<string>("aria2.addUri", uriList,
                new Dictionary<string, string>
                {
                    {"user-agent", userAgent},
                    {"referer", referrer}
                });
        }

        public async Task<string> AddUri(List<string> uriList, Dictionary<string, string> options)
        {
            return await Invoke<string>("aria2.addUri", uriList, options);
        }

        public async Task<string> AddMetaLink(string filePath)
        {
            var metaLinkBase64 = Convert.ToBase64String(File.ReadAllBytes(filePath));
            return await Invoke<string>("aria2.addMetalink", metaLinkBase64);
        }
        
        public async Task<string> AddTorrent(string filePath)
        {
            var torrentBase64 = Convert.ToBase64String(File.ReadAllBytes(filePath));
            return await Invoke<string>("aria2.addTorrent", torrentBase64);
        }

        public async Task<string> RemoveTask(string gid, bool forceRemove = false)
        {
            if (!forceRemove)
            {
                return await Invoke<string>("aria2.remove", gid);
            }
            else
            {
                return await Invoke<string>("aria2.forceRemove", gid);
            }
        }

        public async Task<string> PauseTask(string gid, bool forcePause = false)
        {
            if (!forcePause)
            {
                return await Invoke<string>("aria2.pause", gid);
            }
            else
            {
                return await Invoke<string>("aria2.forcePause", gid);
            }
        }

        public async Task<bool> PauseAllTasks()
        {
            return (await Invoke<string>("aria2.pauseAll")).Contains("OK");
        }
        
        public async Task<bool> UnpauseAllTasks()
        {
            return (await Invoke<string>("aria2.unpauseAll")).Contains("OK");
        }

        public async Task<string> UnpauseTask(string gid)
        {
            return await Invoke<string>("aria2.unpause", gid);
        }

        public async Task<AriaStatus> GetStatus(string gid)
        {
            return await Invoke<AriaStatus>("aria2.tellStatus", gid);
        }

        public async Task<AriaUri> GetUris(string gid)
        {
            return await Invoke<AriaUri>("aria2.getUris", gid);
        }

        public async Task<AriaFile> GetFiles(string gid)
        {
            return await Invoke<AriaFile>("aria2.getFiles", gid);
        }

        public async Task<AriaTorrent> GetPeers(string gid)
        {
            return await Invoke<AriaTorrent>("aria2.getPeers", gid);
        }

        public async Task<AriaServer> GetServers(string gid)
        {
            return await Invoke<AriaServer>("aria2.getServers", gid);
        }
        
        public async Task<AriaStatus> GetActiveStatus(string gid)
        {
            return await Invoke<AriaStatus>("aria2.tellActive", gid);
        }
        public async Task<AriaOption> GetOption(string gid)
        {
            return await Invoke<AriaOption>("aria2.getOption", gid);
        }


        public async Task<bool> ChangeOption(string gid, AriaOption option)
        {
            return (await Invoke<string>("aria2.changeOption", gid, option))
                .Contains("OK");
        }
        
        public async Task<AriaOption> GetGlobalOption()
        {
            return await Invoke<AriaOption>("aria2.getGlobalOption");
        }
        
        public async Task<bool> ChangeGlobalOption(AriaOption option)
        {
            return (await Invoke<string>("aria2.changeGlobalOption", option))
                .Contains("OK");
        }

        public async Task<AriaGlobalStatus> GetGlobalStatus()
        {
            return await Invoke<AriaGlobalStatus>("aria2.getGlobalStat");
        }

        public async Task<bool> PurgeDownloadResult()
        {
            return (await Invoke<string>("aria2.purgeDownloadResult")).Contains("OK");
        }
        
        public async Task<bool> RemoveDownloadResult(string gid)
        {
            return (await Invoke<string>("aria2.removeDownloadResult", gid))
                .Contains("OK");
        }

        public async Task<AriaVersionInfo> GetVersion()
        {
            return await Invoke<AriaVersionInfo>("aria2.getVersion");
        }
        
        public async Task<AriaSession> GetSessionInfo()
        {
            return await Invoke<AriaSession>("aria2.getSessionInfo");
        }

        public async Task<bool> Shutdown(bool forceShutdown = false)
        {
            if (!forceShutdown)
            {
                return (await Invoke<string>("aria2.shutdown")).Contains("OK");
            }
            else
            {
                return (await Invoke<string>("aria2.forceShutdown")).Contains("OK");
            }
        }

        public async Task<bool> SaveSession()
        {
            return (await Invoke<string>("aria2.saveSession")).Contains("OK");
        }
    }
}