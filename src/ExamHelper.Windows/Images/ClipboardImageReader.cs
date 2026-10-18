using System.IO;
using System.Buffers.Binary;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media.Imaging;
namespace ExamHelper.Windows.Images;
public sealed record ClipboardImagePayload(BitmapSource? Bitmap,IReadOnlyList<string> Files);
public sealed class ClipboardImageReader
{
    public ClipboardImagePayload? Read(IDataObject data)
    {
        Exception? failure=null;var advertised=false;
        var formats=new[] { "PNG","image/png",DataFormats.Bitmap,DataFormats.GetDataFormat(17).Name,DataFormats.Dib,DataFormats.Tiff };
        foreach(var format in formats.Distinct())
        {
            try
            {
                if(!data.GetDataPresent(format,format==DataFormats.Bitmap)) continue;
                var value=data.GetData(format,format==DataFormats.Bitmap);if(value is null)continue;advertised=true;
                if(value is BitmapSource bitmap)return new(Snapshot(bitmap),[]);
                var bytes=ReadBytes(value);if(bytes is null)continue;
                if(format==DataFormats.Dib || format==DataFormats.GetDataFormat(17).Name)bytes=WithBitmapFileHeader(bytes);
                using var stream=new MemoryStream(bytes);var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
                if(decoder.Frames.Count==0)throw new ArgumentException("剪贴板图片没有图像帧。");
                return new(Snapshot(decoder.Frames[0]),[]);
            }
            catch(Exception error) when(error is IOException or FormatException or ArgumentException or NotSupportedException or InvalidOperationException or OverflowException or System.Runtime.InteropServices.COMException)
            { failure=error; }
        }
        if(data.GetDataPresent(DataFormats.FileDrop))
        {
            var value=data.GetData(DataFormats.FileDrop);
            var paths=value is string[] array ? array.ToArray() : value is StringCollection strings ? strings.Cast<string>().ToArray() : [];
            if(paths.Length>0)return new(null,paths);
        }
        if(advertised || failure is not null)throw new ArgumentException("无法读取剪贴板图片，请重新复制后重试。",failure);
        return null;
    }
    private static byte[]? ReadBytes(object value)
    {
        if(value is byte[] bytes)return bytes.ToArray();
        if(value is not Stream stream)return null;
        if(stream is MemoryStream memory)return memory.ToArray();
        var position=stream.CanSeek ? stream.Position : (long?)null;
        try { if(position is not null)stream.Position=0;using var copy=new MemoryStream();stream.CopyTo(copy);return copy.ToArray(); }
        finally { if(position is { } previous)stream.Position=previous; }
    }
    private static BitmapSource Snapshot(BitmapSource source)
    {
        var stride=checked((source.PixelWidth*source.Format.BitsPerPixel+7)/8);var pixels=new byte[checked(stride*source.PixelHeight)];source.CopyPixels(pixels,stride,0);
        var bitmap=BitmapSource.Create(source.PixelWidth,source.PixelHeight,source.DpiX,source.DpiY,source.Format,source.Palette,pixels,stride);bitmap.Freeze();return bitmap;
    }
    // Clipboard DIB starts at its information header; WIC's BMP decoder needs a file header.
    private static byte[] WithBitmapFileHeader(byte[] dib)
    {
        if(dib.Length<12)throw new ArgumentException("剪贴板位图头不完整。");
        var header=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(dib));int palette,masks=0;
        if(header==12)
        { var bits=BinaryPrimitives.ReadUInt16LittleEndian(dib.AsSpan(10));palette=bits<=8 ? checked((1<<bits)*3) : 0; }
        else
        {
            if(header<40 || header>dib.Length)throw new ArgumentException("剪贴板位图头无效。");
            var bits=BinaryPrimitives.ReadUInt16LittleEndian(dib.AsSpan(14));var colors=BinaryPrimitives.ReadUInt32LittleEndian(dib.AsSpan(32));
            palette=checked((int)(colors>0 ? colors : bits<=8 ? 1u<<bits : 0u)*4);
            var compression=BinaryPrimitives.ReadUInt32LittleEndian(dib.AsSpan(16));if(header==40)masks=compression==3 ? 12 : compression==6 ? 16 : 0;
        }
        var offset=checked(14+header+palette+masks);if(offset>=dib.Length+14)throw new ArgumentException("剪贴板位图缺少像素。");
        var file=new byte[checked(dib.Length+14)];file[0]=0x42;file[1]=0x4D;
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(2),file.Length);BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(10),offset);dib.CopyTo(file,14);return file;
    }
}
