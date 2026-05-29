using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace OneFileBox.Services;

public static class NewFileService
{
    private static readonly uint[] _crcTable = new uint[256];

    static NewFileService()
    {
        for (int i = 0; i < 256; i++)
        {
            uint c = (uint)i;
            for (int j = 0; j < 8; j++)
                c = (c & 1) != 0 ? (0xEDB88320 ^ (c >> 1)) : (c >> 1);
            _crcTable[i] = c;
        }
    }

    public static bool Create(string fullPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            byte[] bytes = ext switch
            {
                ".txt" or ".rtf" => Array.Empty<byte>(),
                ".docx" => CreateDocx(),
                ".xlsx" => CreateXlsx(),
                ".pptx" => CreatePptx(),
                ".png" => CreatePng(),
                ".bmp" => CreateBmp(),
                _ => Array.Empty<byte>()
            };

            File.WriteAllBytes(fullPath, bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] CreateDocx()
    {
        using var ms = new MemoryStream();
        using var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true);
        WriteEntry(zip, "[Content_Types].xml",
            @"<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">" +
            @"<Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>" +
            @"<Default Extension=""xml"" ContentType=""application/xml""/>" +
            @"<Override PartName=""/word/document.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml""/>" +
            @"</Types>");
        WriteEntry(zip, "_rels/.rels",
            @"<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">" +
            @"<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""word/document.xml""/>" +
            @"</Relationships>");
        WriteEntry(zip, "word/document.xml",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>" +
            @"<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">" +
            @"<w:body><w:p><w:pPr/><w:r><w:t></w:t></w:r></w:p></w:body></w:document>");
        return ms.ToArray();
    }

    private static byte[] CreateXlsx()
    {
        using var ms = new MemoryStream();
        using var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true);
        WriteEntry(zip, "[Content_Types].xml",
            @"<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">" +
            @"<Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>" +
            @"<Default Extension=""xml"" ContentType=""application/xml""/>" +
            @"<Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>" +
            @"<Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>" +
            @"</Types>");
        WriteEntry(zip, "_rels/.rels",
            @"<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">" +
            @"<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>" +
            @"</Relationships>");
        WriteEntry(zip, "xl/workbook.xml",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>" +
            @"<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">" +
            @"<sheets><sheet name=""Sheet1"" sheetId=""1"" r:id=""rId1"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships""/></sheets></workbook>");
        WriteEntry(zip, "xl/worksheets/sheet1.xml",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>" +
            @"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">" +
            @"<sheetData><row r=""1""><c r=""A1"" t=""str""><v></v></c></row></sheetData></worksheet>");
        WriteEntry(zip, "xl/_rels/workbook.xml.rels",
            @"<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">" +
            @"<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>" +
            @"</Relationships>");
        return ms.ToArray();
    }

    private static byte[] CreatePptx()
    {
        using var ms = new MemoryStream();
        using var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true);
        WriteEntry(zip, "[Content_Types].xml",
            @"<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">" +
            @"<Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>" +
            @"<Default Extension=""xml"" ContentType=""application/xml""/>" +
            @"<Override PartName=""/ppt/presentation.xml"" ContentType=""application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml""/>" +
            @"<Override PartName=""/ppt/slides/slide1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.presentationml.slide+xml""/>" +
            @"</Types>");
        WriteEntry(zip, "_rels/.rels",
            @"<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">" +
            @"<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""ppt/presentation.xml""/>" +
            @"</Relationships>");
        WriteEntry(zip, "ppt/presentation.xml",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>" +
            @"<p:presentation xmlns:p=""http://schemas.openxmlformats.org/presentationml/2006/main"">" +
            @"<p:sldIdLst><p:sldId id=""256"" r:id=""rId1"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships""/></p:sldIdLst>" +
            @"<p:sldSz cx=""9144000"" cy=""6858000""/></p:presentation>");
        WriteEntry(zip, "ppt/slides/slide1.xml",
            @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>" +
            @"<p:sld xmlns:p=""http://schemas.openxmlformats.org/presentationml/2006/main"">" +
            @"<p:cSld><p:spTree><p:nvGrpSpPr><p:cNvPr id=""1"" name=""""/>" +
            @"<p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>" +
            @"<p:grpSpPr><a:xfrm xmlns:a=""http://schemas.openxmlformats.org/drawingml/2006/main"">" +
            @"<a:off x=""0"" y=""0""/><a:ext cx=""0"" cy=""0""/><a:chOff x=""0"" y=""0""/><a:chExt cx=""0"" cy=""0""/>" +
            @"</a:xfrm></p:grpSpPr></p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sld>");
        WriteEntry(zip, "ppt/_rels/presentation.xml.rels",
            @"<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">" +
            @"<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide"" Target=""slides/slide1.xml""/>" +
            @"</Relationships>");
        return ms.ToArray();
    }

    private static byte[] CreatePng()
    {
        var signature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var ihdr = MakeChunk("IHDR", new byte[] { 0, 0, 0, 1, 0, 0, 0, 1, 8, 2, 0, 0, 0 });
        var raw = new byte[] { 0, 0, 0, 0 };
        var idat = MakeChunk("IDAT", Deflate(raw));
        var iend = MakeChunk("IEND", Array.Empty<byte>());

        var result = new byte[signature.Length + ihdr.Length + idat.Length + iend.Length];
        int o = 0;
        Buffer.BlockCopy(signature, 0, result, o, signature.Length); o += signature.Length;
        Buffer.BlockCopy(ihdr, 0, result, o, ihdr.Length); o += ihdr.Length;
        Buffer.BlockCopy(idat, 0, result, o, idat.Length); o += idat.Length;
        Buffer.BlockCopy(iend, 0, result, o, iend.Length);
        return result;
    }

    private static byte[] CreateBmp()
    {
        var bmp = new byte[66];
        bmp[0] = 0x42; bmp[1] = 0x4D;
        bmp[2] = 0x42; bmp[3] = 0x00;
        bmp[10] = 0x36; bmp[11] = 0x00; bmp[12] = 0x00; bmp[13] = 0x00;
        bmp[14] = 40; bmp[18] = 1; bmp[19] = 0;
        bmp[22] = 1; bmp[23] = 0;
        bmp[24] = 1; bmp[25] = 0;
        bmp[26] = 24;
        int px = 14 + 40;
        bmp[px] = 0xFF; bmp[px + 1] = 0xFF; bmp[px + 2] = 0xFF;
        return bmp;
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
        using var w = new StreamWriter(entry.Open(), Encoding.UTF8);
        w.Write(content);
    }

    private static byte[] MakeChunk(string type, byte[] data)
    {
        var chunk = new byte[data.Length + 12];
        chunk[0] = (byte)(data.Length >> 24);
        chunk[1] = (byte)(data.Length >> 16);
        chunk[2] = (byte)(data.Length >> 8);
        chunk[3] = (byte)data.Length;
        for (int i = 0; i < 4; i++) chunk[4 + i] = (byte)type[i];
        Buffer.BlockCopy(data, 0, chunk, 8, data.Length);
        uint crc = Crc32(type + Encoding.ASCII.GetString(data));
        chunk[^4] = (byte)(crc >> 24);
        chunk[^3] = (byte)(crc >> 16);
        chunk[^2] = (byte)(crc >> 8);
        chunk[^1] = (byte)crc;
        return chunk;
    }

    private static byte[] Deflate(byte[] raw)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x78); ms.WriteByte(0x01);
        int len = raw.Length;
        ms.WriteByte(0x01);
        ms.WriteByte((byte)(len & 0xFF));
        ms.WriteByte((byte)((len >> 8) & 0xFF));
        ms.WriteByte((byte)((~len) & 0xFF));
        ms.WriteByte((byte)(((~len) >> 8) & 0xFF));
        ms.Write(raw, 0, raw.Length);
        uint a = 1, b = 0;
        foreach (byte x in raw) { a = (a + x) % 65521; b = (b + a) % 65521; }
        uint adler = (b << 16) | a;
        ms.WriteByte((byte)(adler >> 24));
        ms.WriteByte((byte)(adler >> 16));
        ms.WriteByte((byte)(adler >> 8));
        ms.WriteByte((byte)adler);
        return ms.ToArray();
    }

    private static uint Crc32(string data)
    {
        var bytes = Encoding.ASCII.GetBytes(data);
        uint crc = 0xFFFFFFFF;
        foreach (byte x in bytes)
        {
            int idx = (int)((crc ^ x) & 0xFF);
            crc = (crc >> 8) ^ _crcTable[idx];
        }
        return crc ^ 0xFFFFFFFF;
    }
}