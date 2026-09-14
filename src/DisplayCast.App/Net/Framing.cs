using System.IO;
using System.Text;

namespace DisplayCast.Net;

/// <summary>基于 TCP 的帧协议：[魔数 DC10][int32 长度][负载]。</summary>
public static class Framing
{
    public static readonly byte[] Magic = Encoding.ASCII.GetBytes("DC10");
    private const int MaxFrame = 512 * 1024 * 1024; // 512MB

    public static async Task WriteAsync(Stream s, byte[] payload, CancellationToken ct = default)
    {
        var header = new byte[8];
        Magic.CopyTo(header, 0);
        BitConverter.GetBytes(payload.Length).CopyTo(header, 4);
        await s.WriteAsync(header, ct);
        await s.WriteAsync(payload, ct);
        await s.FlushAsync(ct);
    }

    public static async Task<byte[]> ReadAsync(Stream s, CancellationToken ct = default)
    {
        var header = new byte[8];
        await ReadExactAsync(s, header, ct);
        if (header[0] != Magic[0] || header[1] != Magic[1] || header[2] != Magic[2] || header[3] != Magic[3])
            throw new InvalidDataException("非法帧头");
        int len = BitConverter.ToInt32(header, 4);
        if (len < 0 || len > MaxFrame)
            throw new InvalidDataException("帧长度非法");
        var payload = new byte[len];
        await ReadExactAsync(s, payload, ct);
        return payload;
    }

    private static async Task ReadExactAsync(Stream s, byte[] buffer, CancellationToken ct)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int n = await s.ReadAsync(buffer.AsMemory(offset), ct);
            if (n <= 0) throw new EndOfStreamException();
            offset += n;
        }
    }
}
