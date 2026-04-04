using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PhotoshopFile;

namespace SysWeaver.Media.Psd
{

    public static class PsdImage
    {

        public static ImageData32 DecodeLayer(Layer layer, String fromFilename = null)
        {
            layer.CreateMissingChannels();
            var psd = layer.PsdFile;
            String id = fromFilename == null ? String.Join("", '"', layer.Name, '"') : (layer == psd.BaseLayer ? String.Join("", '"', fromFilename, '"') : String.Join("", '"', layer.Name, "\" in \"", fromFilename, '"'));
            var width = layer.Rect.Width;
            var heigth = layer.Rect.Height;
            var mode = psd.ColorMode;
            var bpp = psd.BitDepth;
            switch (mode)
            {
                case PsdColorMode.RGB:
                    {
                        var rl = layer.Channels.First(x => x.ID == 0);
                        var gl = layer.Channels.First(x => x.ID == 1);
                        var bl = layer.Channels.First(x => x.ID == 2);
                        var al = layer.AlphaChannel;
                        rl.DecodeImageData();
                        gl.DecodeImageData();
                        bl.DecodeImageData();
                        if (al != null)
                            al.DecodeImageData();
                        var r = rl.ImageData;
                        var g = gl.ImageData;
                        var b = bl.ImageData;
                        var a = al?.ImageData;
                        switch (bpp)
                        {
                            case 8:
                                {
                                    Byte[] data = new byte[width * heigth * 4];
                                    int so = 0;
                                    int d = 0;
                                    for (int y = 0; y < heigth; ++y)
                                    {
                                        for (int x = 0; x < width; ++x, ++so, d += 4)
                                        {
                                            var rr = r[so];
                                            var gg = g[so];
                                            var bb = b[so];
                                            var aa = a != null ? a[so] : (Byte)0xff;
                                            //FixRgba(ref rr, ref gg, ref bb, aa);
                                            data[d + 0] = rr;
                                            data[d + 1] = gg;
                                            data[d + 2] = bb;
                                            data[d + 3] = aa;
                                        }
                                    }
                                    return new ImageData32(width, heigth, data);
                                }
                            case 16:
                                {
                                    Byte[] data = new Byte[width * heigth * 4];
                                    int so = 0;
                                    int d = 0;
                                    for (int y = 0; y < heigth; ++y)
                                    {
                                        for (int x = 0; x < width; ++x, so += 2, d += 4)
                                        {
                                            var s0 = so + 1;
                                            var s1 = so;
                                            UInt16 rr = r[s0];
                                            //rr <<= 8;
                                            //rr |= r[s1];
                                            UInt16 gg = g[s0];
                                            //gg <<= 8;
                                            //gg |= g[s1];
                                            UInt16 bb = b[s0];
                                            //bb <<= 8;
                                            //bb |= b[s1];
                                            UInt16 aa = a != null ? a[s0] : (Byte)0xff;
                                            //aa <<= 8;
                                            //aa |= a != null ? a[s1] : (Byte)0xff;

                                            data[d + 0] = (Byte)rr;
                                            data[d + 1] = (Byte)gg;
                                            data[d + 2] = (Byte)bb;
                                            data[d + 3] = (Byte)aa;
                                        }
                                    }
                                    return new ImageData32(width, heigth, data);
                                }
                        }
                        break;

                    }
            }
            throw new Exception("Color mode " + mode + " with " + bpp + (bpp == 1 ? " bit per channel is not supported!" : " bits per channel is not supported!"));
        }


        public static ImageData32 Load(String filename)
        {
            var t = new PsdFile(filename, new LoadContext());
            var layer = t.BaseLayer;
            return DecodeLayer(layer, filename);
        }


        static String ReadId(PsdBinaryReader r)
        {
            var cidl = r.ReadInt32();
            if (cidl == 0)
                return r.ReadAsciiChars(4);
            return r.ReadAsciiChars(cidl);
        }

        static Object ReadType(String type, PsdBinaryReader r)
        {
            if (type == "TEXT")
                return r.ReadUnicodeString();
            if (type == "enum")
            {
                var typeId = ReadId(r);
                var value = ReadId(r);
                return new KeyValuePair<String, String>(typeId, value);
            }
            if (type == "UntF")
            {
                var unit = r.ReadAsciiChars(4);
                return new KeyValuePair<String, Double>(unit, BitConverter.Int64BitsToDouble(r.ReadInt64()));
            }
            if (type == "Objc")
                return ReadDescriptor(r).ToList();
            if ((type == "obj ") || (type == "VlLs"))
            {
                var count = r.ReadInt32();
                KeyValuePair<String, Object>[] data = new KeyValuePair<string, object>[count];
                for (int i = 0; i < count; ++ i)
                {
                    var keyType = r.ReadAsciiChars(4);
                    data[i] = new KeyValuePair<string, object>(keyType, ReadType(keyType, r));
                }
                return data;
            }
            if (type == "prop")
            {
                var className = r.ReadUnicodeString();
                String classId = ReadId(r);
                String keyId = ReadId(r);
                return Tuple.Create(className, classId, keyId);
            }
            if (type == "long")
                return r.ReadInt32();
            if (type == "comp")
                return r.ReadInt64();
            if (type == "doub")
                return BitConverter.Int64BitsToDouble(r.ReadInt64());
            if (type == "tdta")
            {
                var len = r.ReadInt32();
                return r.ReadBytes(len);
            }
            if (type == "bool")
            {
                return r.ReadByte() != 0;
            }
            throw new Exception("Type + " + type + " is unsupported!\nSee: https://www.adobe.com/devnet-apps/photoshop/fileformatashtml/#50577411_21585");
        }

        static IEnumerable<KeyValuePair<String, Object>> ReadDescriptor(PsdBinaryReader r)
        {
            var name = r.ReadUnicodeString();
            var cid = ReadId(r);
            var itemCount = r.ReadInt32();
            for (int i = 0; i < itemCount; ++i)
            {
                var key = ReadId(r);
                var osType = r.ReadAsciiChars(4);
                var value = ReadType(osType, r);
                yield return new KeyValuePair<string, object>(key, value);
            }
        }

   

        public static ImageData32 LoadAndExtractTextLayers(out List<PsdTextLayer> layers, String filename) => LoadAndExtractTextLayers(out layers, out var _, filename);

        public static bool ExtractText(out String text, out double ty, Layer l)
        {
            text = null;
            ty = 0;
            var tysh = l.AdditionalInfo.FirstOrDefault(x => x.Key == "TySh");
            if (tysh == null) //  TODO: Support < 6.0 using tySh ?
                return false;
            var fxrp = l.AdditionalInfo.FirstOrDefault(x => x.Key == "fxrp");
            if (fxrp == null)
                return false;
            double xx, xy, yx, yy, tx;
            using (var ms = new MemoryStream((tysh as RawLayerInfo).Data))
            using (var r = new PsdBinaryReader(ms, Encoding.Default))
            {
                var version = r.ReadInt16();
                xx = BitConverter.Int64BitsToDouble(r.ReadInt64());
                xy = BitConverter.Int64BitsToDouble(r.ReadInt64());
                yx = BitConverter.Int64BitsToDouble(r.ReadInt64());
                yy = BitConverter.Int64BitsToDouble(r.ReadInt64());
                tx = BitConverter.Int64BitsToDouble(r.ReadInt64());
                ty = BitConverter.Int64BitsToDouble(r.ReadInt64());
            }
            using (var ms = new MemoryStream((tysh as RawLayerInfo).Data))
            using (var r = new PsdBinaryReader(ms, Encoding.Default))
            {
                var version = r.ReadInt16();
                var transforms = r.ReadBytes(6 * 8);
                var textVersion = r.ReadInt16();
                var descVersion = r.ReadInt32();
                foreach (var c in ReadDescriptor(r))
                {
                    if (c.Key == "Txt ")
                        text = ((String)c.Value).TrimEnd((Char)0);
/*                    if (c.Key == "EngineData")
                    {
                        String ed = Encoding.UTF8.GetString((Byte[])c.Value);
                    }
*/
                }
            }
            return true;
        }

        public static ImageData32 LoadAndExtractTextLayers(out List<PsdTextLayer> layers, out IReadOnlyDictionary<String, String> props, String filename)
        {
            Dictionary<String, String> properties = new(StringComparer.Ordinal);
            HashSet<String> seenLayers = new HashSet<string>();
            PsdFile t = new PsdFile(filename, new LoadContext());
            var layer = t.BaseLayer;
            layers = new List<PsdTextLayer>();
            foreach (var l in t.Layers)
            {
                if (!l.Visible)
                    continue;
                if (!ExtractText(out var glyphs, out var ty, l))
                {
                    var key = l.Name.Trim().SplitFirst('=', out var val);
                    if (!String.IsNullOrEmpty(val))
                        properties[key.TrimEnd()] = val.TrimStart();
                    continue;
                }
                layers.Add(new PsdTextLayer(l.Name, glyphs, l.Rect.X, l.Rect.Y, l.Rect.Width, l.Rect.Height, (float)ty));
            }
            props = properties.Freeze();
            return DecodeLayer(layer, filename);
        }


        public static bool OnPsdLayers(String filename, Func<PsdFile, Layer, bool> onLayer)
        {
            PsdFile t = new PsdFile(filename, new LoadContext());
            foreach (var l in t.Layers)
                if (!onLayer(t, l))
                    return false;
            return true;
        }

        public static ImageData32 GetImageAndProcessPsdLayers(String filename, Func<PsdFile, Layer, bool> onLayer)
        {
            PsdFile t = new PsdFile(filename, new LoadContext());
            foreach (var l in t.Layers)
                if (!onLayer(t, l))
                    return null;
            return DecodeLayer(t.BaseLayer, filename);
        }

        public static List<NamedLayer> GetNamedLayers(String filename)
        {
            List<NamedLayer> layers = new List<NamedLayer>();
            OnPsdLayers(filename, (psd, layer) =>
            {
                if (!layer.Visible)
                    return true;
                var r = layer.Rect;
                if (r.IsEmpty)
                    return true;
                layers.Add(new NamedLayer
                {
                    Name = layer.Name,
                    X = r.X,
                    Y = r.Y,
                    W = r.Width,
                    H = r.Height,
                });
                return true;
            });
            return layers;
        }
    }

    public sealed class NamedLayer
    {
#if DEBUG
        public override string ToString() => String.Concat(W, 'x', H, ' ', Name, " @ ", X, ',', Y);
#endif//DEBUG
        public String Name;
        public int X;
        public int Y;
        public int W;
        public int H;
    }
}
