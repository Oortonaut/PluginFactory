using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace Oortonaut.PluginFactory {
    public class Plugin {
        public static Plugin? Construct(string fileName) {
            var assemblyName = AssemblyName.GetAssemblyName(fileName);
            if(assemblyName.Name == null || assemblyName.Version == null ||
               assemblyName.ProcessorArchitecture == ProcessorArchitecture.None) return default;
            return new(fileName, assemblyName);
        }
        private Plugin(string fileName_, AssemblyName assemblyName_) {
            fileName = fileName_;
            assemblyName = assemblyName_;
            name = assemblyName.Name!;
            version = assemblyName.Version!;
        }
        public bool Load() {
            using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            using var ms = new MemoryStream(( int ) fs.Length);
            fs.CopyTo(ms);
            fs.Dispose();
            assembly = Assembly.Load(ms.ToArray());
            ms.Dispose();

            if(Loaded) {
                LoadResources();
            }
            return Loaded;
        }
        public bool Load(AssemblyLoadContext context) {
            using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            assembly = context.LoadFromStream(fs);
            fs.Dispose();
            if(Loaded) {
                LoadResources();
            }
            return Loaded;
        }
        public bool Loaded => assembly != null;
        public string Text(string key, string dflt = "") => text[key] ?? dflt;
        public byte[]? Bin(string key, byte[]? dflt = null) => bin[key] ?? dflt;
        public object? Other(string key, object? dflt = null) => other[key] ?? dflt;
        public IEnumerable<string> TextKeys => text.Keys;
        public IEnumerable<string> BinKeys => bin.Keys;
        public IEnumerable<string> OtherKeys => other.Keys;
        // Supported types are 
        //  * string for strings and text files
        //  * 
        //  * byte[] for other resources
        void LoadResources() {
            foreach(var rsrcName in assembly!.GetManifestResourceNames()) {
                if(!rsrcName.EndsWith(".resources")) continue;
                var req = rsrcName.Substring(0, rsrcName.Length - 10);
                Console.WriteLine($"Resource: {req}");
                try {
                    ResourceManager resManager = new ResourceManager(req, assembly);
                    // for some reason the ResourceSet doesn't create when I don't call GetObject first
                    var f = resManager.GetObject("");
                    ResourceSet? resSet = resManager.GetResourceSet(
                        System.Globalization.CultureInfo.CurrentUICulture, false, true);
                    if(resSet != null) {
                        foreach(DictionaryEntry res in resSet) {
                            if(res.Value != null) {
                                if(res.Value.GetType() == typeof(string)) {
                                    text.Add(( string ) res.Key, ( string ) res.Value);
                                } else if(res.Value.GetType() == typeof(byte[])) {
                                    bin.Add(( string ) res.Key, ( byte[] ) res.Value);
                                } else {
                                    other.Add(( string ) res.Key, res.Value);
                                }
                            }
                        }
                    }
                    resManager.ReleaseAllResources();
                } catch { }
            }
        }


        public readonly string fileName;
        public readonly AssemblyName assemblyName;
        public readonly string name;
        public readonly Version version;
        public Assembly? assembly; // when loaded
        Dictionary<string, string> text = new();
        Dictionary<string, byte[]> bin = new();
        Dictionary<string, object> other = new();
    }
}
