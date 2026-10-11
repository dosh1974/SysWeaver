using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PhotoshopFile;

namespace SysWeaver.Media.Psd
{

    /// <summary>
    /// Helpers to load PSD files as RGBA images, to enumerate layers and to extract text layers.
    /// Only RGB documents with 8 or 16 bits per channel can be decoded to an image.
    /// </summary>
    public static class PsdImage
    {
        /// <summary>
        /// Decode a channel, unless it has no compressed data (a channel added by <see cref="Layer.CreateMissingChannels"/> already has its ImageData).
        /// </summary>
        static void Decode(Channel c)
        {
            if (c.ImageDataRaw != null)
                c.DecodeImageData();
        }

        /// <summary>
        /// Decode a layer (or the composite image, <see cref="PsdFile.BaseLayer"/>) to an RGBA image of the layer's rect size.
        /// Missing color channels are added as fully saturated (255) by <see cref="Layer.CreateMissingChannels"/>, the channels are decoded (the layer is modified).
        /// Without an alpha channel (ID -1) the alpha is 255.
        /// 16 bit channels are reduced to 8 bits by taking the high byte.
        /// </summary>
        /// <param name="layer">The layer to decode.</param>
        /// <param name="fromFilename">Optional file name, only used in the exception message.</param>
        /// <returns>The RGBA image (R, G, B, A byte order, stride = width * 4).</returns>
        /// <exception cref="NotSupportedException">The document is not RGB, or the bit depth is not 8 or 16.</exception>
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
                        Decode(rl);
                        Decode(gl);
                        Decode(bl);
                        if (al != null)
                            Decode(al);
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
            throw new NotSupportedException("Color mode " + mode + " with " + bpp + (bpp == 1 ? " bit per channel is not supported!" : " bits per channel is not supported!") + " Layer: " + id);
        }


        /// <summary>
        /// Load a PSD file and decode its composite image (<see cref="PsdFile.BaseLayer"/>), see <see cref="DecodeLayer"/>.
        /// </summary>
        /// <param name="filename">The PSD file.</param>
        /// <returns>The RGBA composite image.</returns>
        /// <exception cref="NotSupportedException">The document is not RGB, or the bit depth is not 8 or 16.</exception>
        /// <exception cref="PsdInvalidException">The file is not a valid PSD file.</exception>
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

        /// <summary>
        /// The maximum nesting depth of descriptors / lists (prevents a stack overflow on malicious data)
        /// </summary>
        const int MaxDescriptorDepth = 64;

        static void CheckDescriptorDepth(int depth)
        {
            if (depth > MaxDescriptorDepth)
                throw new PsdInvalidException("Descriptors are nested deeper than " + MaxDescriptorDepth + " levels.");
        }

        static Object ReadType(String type, PsdBinaryReader r, int depth)
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
            {
                CheckDescriptorDepth(depth + 1);
                return ReadDescriptor(r, depth + 1).ToList();
            }
            if ((type == "obj ") || (type == "VlLs"))
            {
                CheckDescriptorDepth(depth + 1);
                var count = r.ReadInt32();
                if (count < 0)
                    throw new PsdInvalidException("Negative list item count (" + count + ").");
                // Each item has at least a 4 byte type, prevents huge allocations from corrupt counts
                r.CheckAvailable(4L * count);
                KeyValuePair<String, Object>[] data = new KeyValuePair<string, object>[count];
                for (int i = 0; i < count; ++ i)
                {
                    var keyType = r.ReadAsciiChars(4);
                    data[i] = new KeyValuePair<string, object>(keyType, ReadType(keyType, r, depth + 1));
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
            throw new NotSupportedException("Type " + type + " is unsupported!\nSee: https://www.adobe.com/devnet-apps/photoshop/fileformatashtml/#50577411_21585");
        }

        static IEnumerable<KeyValuePair<String, Object>> ReadDescriptor(PsdBinaryReader r, int depth)
        {
            var name = r.ReadUnicodeString();
            var cid = ReadId(r);
            var itemCount = r.ReadInt32();
            for (int i = 0; i < itemCount; ++i)
            {
                var key = ReadId(r);
                var osType = r.ReadAsciiChars(4);
                var value = ReadType(osType, r, depth);
                yield return new KeyValuePair<string, object>(key, value);
            }
        }

   

        /// <summary>
        /// Load a PSD file, extract the visible text layers and decode the composite image, see <see cref="LoadAndExtractTextLayers(out List{PsdTextLayer}, out IReadOnlyDictionary{string, string}, string)"/>.
        /// </summary>
        public static ImageData32 LoadAndExtractTextLayers(out List<PsdTextLayer> layers, String filename) => LoadAndExtractTextLayers(out layers, out var _, filename);

        /// <summary>
        /// Extract the text of a text layer (Photoshop 6+ "TySh" layer info, the layer must also have a "fxrp" layer info).
        /// </summary>
        /// <param name="text">The text ("Txt " descriptor item, trailing NUL characters removed), null if the descriptor has no text item.</param>
        /// <param name="ty">The vertical translation of the text transform (used as the baseline).</param>
        /// <param name="l">The layer.</param>
        /// <returns>True if the layer is a text layer, false if it has no "TySh" or no "fxrp" layer info.</returns>
        /// <exception cref="NotSupportedException">The text descriptor contains an unsupported item type.</exception>
        /// <exception cref="System.IO.EndOfStreamException">The text data is truncated.</exception>
        /// <exception cref="PsdInvalidException">The text descriptor is invalid (a negative list item count, or descriptors / lists nested deeper than 64 levels).</exception>
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
                foreach (var c in ReadDescriptor(r, 0))
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

        /// <summary>
        /// Load a PSD file, extract the visible text layers (see <see cref="ExtractText"/>) and decode the composite image (see <see cref="DecodeLayer"/>).
        /// Visible layers that are not text layers and are named "key=value" are returned as properties
        /// (key and value trimmed, split at the first '=', layers with an empty value are ignored, a later layer overwrites an earlier one with the same key).
        /// Hidden layers are ignored.
        /// </summary>
        /// <param name="layers">The visible text layers, in file order (bottom to top).</param>
        /// <param name="props">The properties (ordinal, case sensitive keys).</param>
        /// <param name="filename">The PSD file.</param>
        /// <returns>The RGBA composite image.</returns>
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


        /// <summary>
        /// Load a PSD file and call a function for each layer (in file order, bottom to top, hidden layers included).
        /// </summary>
        /// <param name="filename">The PSD file.</param>
        /// <param name="onLayer">Called for each layer, return false to stop.</param>
        /// <returns>False if <paramref name="onLayer"/> returned false, else true.</returns>
        public static bool OnPsdLayers(String filename, Func<PsdFile, Layer, bool> onLayer)
        {
            PsdFile t = new PsdFile(filename, new LoadContext());
            foreach (var l in t.Layers)
                if (!onLayer(t, l))
                    return false;
            return true;
        }

        /// <summary>
        /// Load a PSD file, call a function for each layer (see <see cref="OnPsdLayers"/>) and decode the composite image (see <see cref="DecodeLayer"/>).
        /// </summary>
        /// <param name="filename">The PSD file.</param>
        /// <param name="onLayer">Called for each layer, return false to stop.</param>
        /// <returns>The RGBA composite image, or null if <paramref name="onLayer"/> returned false.</returns>
        public static ImageData32 GetImageAndProcessPsdLayers(String filename, Func<PsdFile, Layer, bool> onLayer)
        {
            PsdFile t = new PsdFile(filename, new LoadContext());
            foreach (var l in t.Layers)
                if (!onLayer(t, l))
                    return null;
            return DecodeLayer(t.BaseLayer, filename);
        }

        /// <summary>
        /// Get the name and rect of the visible layers of a PSD file (in file order, bottom to top).
        /// Layers whose rect is <see cref="System.Drawing.Rectangle.IsEmpty"/> (all values 0) are skipped.
        /// </summary>
        /// <param name="filename">The PSD file.</param>
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

    /// <summary>
    /// The name and rect (in document pixels) of a layer, see <see cref="PsdImage.GetNamedLayers"/>.
    /// </summary>
    public sealed class NamedLayer
    {
#if DEBUG
        public override string ToString() => String.Concat(W, 'x', H, ' ', Name, " @ ", X, ',', Y);
#endif//DEBUG
        /// <summary>
        /// The layer name (the Unicode name if present).
        /// </summary>
        public String Name;
        /// <summary>
        /// Left edge.
        /// </summary>
        public int X;
        /// <summary>
        /// Top edge.
        /// </summary>
        public int Y;
        /// <summary>
        /// Width.
        /// </summary>
        public int W;
        /// <summary>
        /// Height.
        /// </summary>
        public int H;
    }
}
