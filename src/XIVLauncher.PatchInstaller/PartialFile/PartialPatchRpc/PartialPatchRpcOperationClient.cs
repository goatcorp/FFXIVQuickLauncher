using Serilog;
using SharedMemory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace XIVLauncher.PatchInstaller.PartialFile.PartialPatchRpc
{
    public abstract class PartialPatchRpcOperationClient : IDisposable
    {
        public class RequestedPartInfo
        {
            public int targetFileIndex;
            public int partIndex;
            public long sourceOffset;
            public int sourceSize;

            public RequestedPartInfo(BinaryReader reader)
            {
                targetFileIndex = reader.ReadInt32();
                partIndex = reader.ReadInt32();
                sourceOffset = reader.ReadInt64();
                sourceSize = reader.ReadInt32();
            }
        }

        protected readonly string RpcName;
        private readonly RpcBuffer Rpc;

        private readonly Queue<BinaryReader> QueuedMessages = new();

        public PartialPatchRpcOperationClient(string rpcName)
        {
            RpcName = rpcName;
            Rpc = new(rpcName, OnMessage);
        }

        private void OnMessage(ulong msgId, byte[] payload)
        {
            lock (QueuedMessages)
            {
                while (QueuedMessages.Count >= 512)
                    Monitor.Wait(QueuedMessages);

                QueuedMessages.Enqueue(new BinaryReader(new MemoryStream(payload)));
                Monitor.Pulse(QueuedMessages);
            }
        }

        public virtual void Run()
        {
            while (true)
            {
                BinaryReader reader;
                lock (QueuedMessages)
                {
                    while (QueuedMessages.Count == 0)
                        Monitor.Wait(QueuedMessages);
                    reader = QueuedMessages.Dequeue();
                }
                if (reader == null)
                    break;

                switch ((PartialPatchRpcOpcode)reader.ReadInt32())
                {
                    case PartialPatchRpcOpcode.StatusUpdate:
                        {
                            var progress = reader.ReadSingle();
                            var applyProgress = reader.ReadInt64();
                            var applyProgressMax = reader.ReadInt64();
                            OnStatusUpdate(progress, applyProgress, applyProgressMax);
                            break;
                        }

                    case PartialPatchRpcOpcode.Finished:
                        return;

                    case PartialPatchRpcOpcode.RequestPartialFile:
                        {
                            var patchSetIndex = reader.ReadInt32();
                            var patchFileIndex = reader.ReadInt32();
                            var patchFileName = reader.ReadString();
                            var partCount = reader.ReadInt32();
                            var parts = new List<RequestedPartInfo>();
                            for (int i = 0; i < partCount; i++)
                                parts.Add(new RequestedPartInfo(reader));
                            OnRequestPartialFile(patchSetIndex, patchFileIndex, patchFileName, parts);
                            break;
                        }

                    case PartialPatchRpcOpcode.FinishPartialFile:
                        {
                            var patchSetIndex = reader.ReadInt32();
                            var patchFileIndex = reader.ReadInt32();
                            var patchFileName = reader.ReadString();
                            OnFinishPartialFile(patchSetIndex, patchFileIndex, patchFileName);
                            break;
                        }
                }
            }
        }

        protected void ProvideIndexFile(string rootPath, string versionFileName, Stream indexFileStream)
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            writer.Write((int)PartialPatchRpcOpcode.ProvideIndexFile);
            writer.Write(rootPath);
            writer.Write(versionFileName);
            indexFileStream.CopyTo(stream);
            Rpc.RemoteRequest(stream.ToArray());
        }

        protected void ProvideIndexFileFinish()
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            writer.Write((int)PartialPatchRpcOpcode.ProvideIndexFileFinish);
            Rpc.RemoteRequest(stream.ToArray());
        }

        protected void ProvidePartialFile(int patchSetIndex, int patchFileIndex, string patchFilePath)
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            writer.Write((int)PartialPatchRpcOpcode.ProvidePartialFile);
            writer.Write(patchSetIndex);
            writer.Write(patchFileIndex);
            writer.Write(patchFilePath);
            Rpc.RemoteRequest(stream.ToArray());
        }

        protected virtual void OnStatusUpdate(float progress, long applyProgress, long applyProgressMax)
        {
            Log.Information("Progress report: {0:00.00}% ({1}MB/{2}MB)", 100 * progress, applyProgress / 1048576, applyProgressMax / 1048576);
        }

        protected abstract void OnRequestPartialFile(int patchSetIndex, int patchFileIndex, string patchFileName, List<RequestedPartInfo> parts);

        protected virtual void OnFinishPartialFile(int patchSetIndex, int patchFileIndex, string patchFileName)
        {
            Log.Information("Finish response: {0}/{1}/{2}", patchSetIndex, patchFileIndex, patchFileName);
        }

        public virtual void Dispose()
        {
            Rpc.Dispose();
        }
    }
}

