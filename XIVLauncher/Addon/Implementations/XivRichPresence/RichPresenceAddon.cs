using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DiscordRPC;
using Serilog;
using XIVLauncher.Addon.Implementations.XivRichPresence;

namespace XIVLauncher.Addon
{
    class RichPresenceAddon : IPersistentAddon
    {
        private const string ClientID = "478143453536976896";

        private Process _gameProcess;
        private string _lastFc = string.Empty;

        private static readonly RichPresence DefaultPresence = new RichPresence
        {
            Details = "In menus",
            State = "",
            Assets = new Assets
            {
                LargeImageKey = "li_1",
                LargeImageText = "",
                SmallImageKey = "class_0",
                SmallImageText = ""
            }
        };

        public void Setup(Process gameProcess)
        {
            _gameProcess = gameProcess;
        }

        public async void DoWork(object state)
        {
            var cancellationToken = (CancellationToken) state;

            if (!Settings.IsDX11())
                return;

            CheckManualInstall();
            try
            {
                var game = new Nhaama.FFXIV.Game(_gameProcess);

                var discordManager = new DiscordPresenceManager(DefaultPresence, ClientID);
                discordManager.SetPresence(DefaultPresence);

                Log.Information("RichPresence DoWork started.");

                while (!cancellationToken.IsCancellationRequested)
                {
                    game.Update();

                    if (game.ActorTable == null)
                    {
                        discordManager.SetPresence(DefaultPresence);
                        continue;
                    }

                    if (game.ActorTable.Length > 0)
                    {
                        var player = game.ActorTable[0];

                        if (player.ActorID == 0)
                        {
                            discordManager.SetPresence(DefaultPresence);
                            continue;
                        }

                        var territoryType = game.TerritoryType;

                        var loadingImageKey = await XivApi.GetLoadingImageKeyForTerritoryType(territoryType);

                        var largeImageKey = $"li_{loadingImageKey}";

                        var fcName = player.CompanyTag;

                        if (fcName != string.Empty)
                        {
                            _lastFc = fcName;
                            fcName = $" <{fcName}>";
                        }
                        else if (_lastFc != string.Empty)
                        {
                            fcName = $" <{_lastFc}>";
                        }

                        var worldName = await XivApi.GetNameForWorld(player.World);

                        if (player.World != player.HomeWorld)
                            worldName = $"{worldName} (🏠{await XivApi.GetNameForWorld(player.HomeWorld)})";

                        discordManager.SetPresence(new RichPresence
                        {
                            Details = $"{player.Name}{fcName}",
                            State = worldName,
                            Assets = new Assets
                            {
                                LargeImageKey = largeImageKey,
                                LargeImageText = await XivApi.GetPlaceNameForTerritoryType(territoryType),
                                SmallImageKey = $"class_{player.Job}",
                                SmallImageText = await XivApi.GetJobName(player.Job) + " Lv." + player.Level
                            }
                        });
                    }

                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Critical error in RichPresence.");
            }

            Log.Information("RichPresence exited!");
        }

        private bool CheckManualInstall()
        {
            try
            {
                // Delete a manually installed version of RichPresence, don't need to launch it twice
                var dump64Path = Path.Combine(Settings.GamePath.FullName, "game", "dump64.dll");
                if (File.Exists(dump64Path))
                    File.Delete(dump64Path);

                return true;
            }
            catch (Exception)
            {
                MessageBox.Show("XIVLauncher found a manual installation of FFXIV Rich Presence, but could not remove it.\nTo fix this, please close any instances of FINAL FANTASY XIV, start XIVLauncher as administrator and log in.");
                return false;
            }
        }

        public string Name => "FFXIV Discord Rich Presence";
    }
}
