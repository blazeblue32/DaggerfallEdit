using System.Buffers.Binary;
using System.Text;

namespace DaggerfallEdit.Vanilla;

public sealed record BsaNameRecord(
    string Name,
    int Offset,
    int Length
);

public sealed class BsaNameRecordReader
{
    private const ushort NameRecordBsaType = 0x0100;
    private const int HeaderLength = 4;
    private const int NameRecordDescriptorLength = 18;
    private const int NameLength = 12;

    private readonly byte[] data;
    private readonly Dictionary<string, BsaNameRecord> recordsByName;

    private BsaNameRecordReader(byte[] data, List<BsaNameRecord> records)
    {
        this.data = data;
        Records = records;
        recordsByName = records.ToDictionary(
            record => record.Name,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<BsaNameRecord> Records { get; }

    public static BsaNameRecordReader Load(string path)
    {
        byte[] data = File.ReadAllBytes(path);

        if (data.Length < HeaderLength)
            throw new InvalidDataException($"BSA file is too small: {path}");

        ushort recordCount = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2));
        ushort bsaType = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2, 2));

        if (bsaType != NameRecordBsaType)
            throw new InvalidDataException($"Expected NameRecord BSA type 0x0100, got 0x{bsaType:x4}: {path}");

        int footerLength = checked(recordCount * NameRecordDescriptorLength);
        int footerOffset = data.Length - footerLength;

        if (footerOffset < HeaderLength)
            throw new InvalidDataException($"Invalid BSA footer offset: {path}");

        var records = new List<BsaNameRecord>(recordCount);
        int recordOffset = HeaderLength;

        for (int i = 0; i < recordCount; i++)
        {
            int descriptorOffset = footerOffset + i * NameRecordDescriptorLength;

            string name = ReadFixedAscii(data.AsSpan(descriptorOffset, NameLength));
            short compressed = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(descriptorOffset + 12, 2));
            int recordLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(descriptorOffset + 14, 4));

            if (compressed != 0)
                throw new NotSupportedException($"Compressed BSA NameRecord entries are not supported: {name}");

            if (recordLength < 0 || recordOffset + recordLength > footerOffset)
                throw new InvalidDataException($"Invalid BSA NameRecord length for {name}: {recordLength}");

            records.Add(new BsaNameRecord(name, recordOffset, recordLength));
            recordOffset += recordLength;
        }

        return new BsaNameRecordReader(data, records);
    }

    public bool TryGetRecord(string name, out ReadOnlyMemory<byte> bytes)
    {
        if (!recordsByName.TryGetValue(name, out BsaNameRecord? record))
        {
            bytes = ReadOnlyMemory<byte>.Empty;
            return false;
        }

        bytes = new ReadOnlyMemory<byte>(data, record.Offset, record.Length);
        return true;
    }

    public ReadOnlyMemory<byte> GetRecord(string name)
    {
        if (!TryGetRecord(name, out ReadOnlyMemory<byte> bytes))
            throw new KeyNotFoundException($"BSA NameRecord not found: {name}");

        return bytes;
    }

    private static string ReadFixedAscii(ReadOnlySpan<byte> bytes)
    {
        int length = bytes.IndexOf((byte)0);

        if (length < 0)
            length = bytes.Length;

        return Encoding.ASCII.GetString(bytes[..length]).TrimEnd();
    }
}
