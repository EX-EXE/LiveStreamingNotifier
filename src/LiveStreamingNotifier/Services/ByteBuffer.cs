using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveStreamingNotifier.Services;
internal class ByteBuffer : IDisposable
{
	private readonly byte[] buffer;
	public ByteBuffer(int size)
	{
		buffer = ArrayPool<byte>.Shared.Rent(size);
	}
	public void Dispose()
	{
		ArrayPool<byte>.Shared.Return(buffer);
	}

	public Span<byte> Span() => buffer.AsSpan();
	public Span<byte> Span(int slice) => buffer.AsSpan(slice);
	public Memory<byte> Memory() => buffer.AsMemory();

}
