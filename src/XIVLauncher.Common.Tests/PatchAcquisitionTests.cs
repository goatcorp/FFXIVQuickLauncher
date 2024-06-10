using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.Game.Patch.Acquisition.Aria;
using XIVLauncher.Common.Game.Patch.PatchList;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Tests
{
    [TestClass]
    public class PatchAcquisitionTests
    {
        private static readonly PatchListEntry testPatch = new PatchListEntry
        {
            HashBlockSize = 0x0000000002faf080,
            HashType = "sha1",
            Length = 0x00000000596279e7,
            Url = "http://patch-dl.ffxiv.com/game/4e9a232b/H2017.06.06.0000.0001a.patch",
            VersionId = "H2017.06.06.0000.0001a",
            Hashes = new []
            {
                "5b4e55eb5036e3230d436012a0a98bc9da855d11",
                "61f38e1688359b75fb2de29c2560b0f2ab84a10c",
                "8af712c40e38abbc73150ce8761ef91c1906037b",
                "1db91d0f4189f8aaa4f62ac092ed3bf69daf76cd",
                "ddf9370a1133af1952adc57e36a0530a7a57c2c8",
                "1debbefa37fc23f7b7fbc78e397732f49a1a48f1",
                "d18f9093c5e6e5b8a971ab662c82968b730cf9ac",
                "5e09826e70d6f160faccb358e57ec1dfb75aa3c4",
                "93271b5593772b68527c54265480a39249161652",
                "2456723cba5e13f565c0fe27f64870105df3024d",
                "5a514fd5e8b0a83b23811298c8d0021bb6f06f02",
                "d0214cd84983be73c06b76e3cfb7be70fc937e1b",
                "a226ab18300b7844cf5d6ed3731b151661923d1b",
                "6c4dffa62b9a1317488a40796e4d8ff2224dcd37",
                "c85f2d0e6190f13b512e772b340677c080f127e7",
                "7621efe84fb0981c54021c2fb48770c9d7f74e5f",
                "f921b93ab6877e63d8964785725117ad12e21d2e",
                "dba220e52405effd485250c7b1d30aa2027f7e42",
                "3c5e8d833521ca06501e5d2572c3dea317c280df",
                "ff4da7cc12b1a72e802bad8e3d8b24c212689ef5",
                "6160bac6703e2a399e40c70cb63893c530ccad44",
                "9a67a544d82c8a7571f3028741b394fdd96f4000",
                "ef0a807957eca4b087fe9f378e37c1b70ffd2226",
                "851718934ca2c9e70ef832e571694665dfe52f5d",
                "d78e823da698807069323f641fb90297c20a240c",
                "90ca2a62d38feeaa2a6c75cb445e096c7c9ecdfe",
                "2c5bce2c05284218897482768bac432eb805a06a",
                "46f3051257a03caf33e66022718ae80121e3bb0d",
                "251df9f816f06ed64820a4f94b3018afe125cfd8",
                "06562a29de9f52d7e3b2aaddcfc82d69de160af7"
            },
        };

        [TestMethod]
        public void TestVersionDecode()
        {
            Assert.AreEqual("game/4e9a232b/H2017.06.06.0000.0001a.patch", testPatch.GetUrlPath());
            Assert.AreEqual("game\\4e9a232b\\H2017.06.06.0000.0001a.patch", testPatch.GetFilePath());
            Assert.AreEqual("ffxiv", testPatch.GetRepoName());
        }

        private async Task TestPatchDownload(PatchAcquisition acquisition)
        {
            var completeSignal = new ManualResetEvent(false);

            acquisition.Complete += (sender, args) =>
            {
                Debug.WriteLine($"[{acquisition.GetType().FullName}] Download completed!");
                completeSignal.Set();
            };

            acquisition.ProgressChanged += (sender, progress) =>
            {
                Debug.WriteLine($"[{acquisition.GetType().FullName}] recv: {progress.Progress} - speed: {ApiHelpers.BytesToString(progress.BytesPerSecondSpeed)}");
            };

            await acquisition.StartDownloadAsync(testPatch.Url, new FileInfo(Path.Combine(Environment.CurrentDirectory, "a.patch")));

            completeSignal.WaitOne();
        }

        [TestMethod]
        public async Task TestAriaDownload()
        {
            await AriaHttpPatchAcquisition.InitializeAsync(0, new FileInfo("aria2.log"));
            await TestPatchDownload(new AriaHttpPatchAcquisition());
        }
    }
}
