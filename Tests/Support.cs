global using Hypostasis;
global using Hypostasis.Dalamud;
using System.IO.Compression;
using System.Text;
namespace Dalamud.Configuration { public interface IPluginConfiguration { int Version { get; set; } } }
namespace Hypostasis.Dalamud { public abstract class PluginConfiguration { public virtual void Initialize() {} } }
namespace Hypostasis {
    public static class Util {
        public static string CompressString(string value,string prefix) {
            using var stream=new MemoryStream();
            using(var zip=new GZipStream(stream,CompressionMode.Compress,true))zip.Write(Encoding.UTF8.GetBytes(value));
            return prefix+Convert.ToBase64String(stream.ToArray());
        }
        public static string DecompressString(string value,string prefix) {
            if(!value.StartsWith(prefix))throw new InvalidOperationException();
            using var stream=new MemoryStream(Convert.FromBase64String(value[prefix.Length..]));
            using var zip=new GZipStream(stream,CompressionMode.Decompress);
            using var reader=new StreamReader(zip);return reader.ReadToEnd();
        }
    }
}
