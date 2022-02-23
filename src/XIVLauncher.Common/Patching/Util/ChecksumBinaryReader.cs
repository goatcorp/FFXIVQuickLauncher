using System;
using System.IO;

namespace XIVLauncher.Common.Patching.Util
{
    public class ChecksumBinaryReader : BinaryReader
    {
        private readonly Crc32 _crc32 = new Crc32();

        public ChecksumBinaryReader(Stream input) : base(input) {}


        public void InitCrc32()
        {
            _crc32.Init();
        }

        public uint GetCrc32()
        {
            return _crc32.Checksum;
        }

        public override byte[] ReadBytes(int count)
        {
            var result = base.ReadBytes(count);

            _crc32.Update(result);

            return result;
        }

        public override byte ReadByte()
        {
            var result = base.ReadByte();

            _crc32.Update(result);

            return result;
        }

        public override sbyte ReadSByte() => (sbyte)ReadByte();
        public override bool ReadBoolean() => ReadByte() != 0;
        public override char ReadChar() => (char)ReadByte();
        public override short ReadInt16() => BitConverter.ToInt16(ReadBytes(sizeof(short)), 0);
        public override ushort ReadUInt16() => BitConverter.ToUInt16(ReadBytes(sizeof(ushort)), 0);
        public override int ReadInt32() => BitConverter.ToInt32(ReadBytes(sizeof(int)), 0);
        public override uint ReadUInt32() => BitConverter.ToUInt32(ReadBytes(sizeof(uint)), 0);
        public override long ReadInt64() => BitConverter.ToInt64(ReadBytes(sizeof(long)), 0);
        public override ulong ReadUInt64() => BitConverter.ToUInt64(ReadBytes(sizeof(ulong)), 0);
        public override float ReadSingle() => BitConverter.ToSingle(ReadBytes(sizeof(float)), 0);
        public override double ReadDouble() => BitConverter.ToDouble(ReadBytes(sizeof(float)), 0);
    }
}
