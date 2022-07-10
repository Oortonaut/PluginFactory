using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Resources;
using System.Collections;
using System.Runtime.Loader;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Oortonaut.PluginFactory {
    public class PluginLoadContext: AssemblyLoadContext {
        public PluginLoadContext() : base(isCollectible: true) { }
        protected override Assembly? Load(AssemblyName assemblyName) {
            return Assembly.Load(assemblyName);
        }
    }
    public class Loader: IDisposable {
        public delegate bool Validator(AssemblyName asmName);
        public Loader(string path_ = "plugins/", string pluginExt_ = ".plugin"): 
            this(path_, pluginExt_, x => true) {
        }
        public Loader(string path_, string pluginExt_, Validator validator) {
            path = path_;
            pluginExt = pluginExt_;
            context = new();
            BuildPluginList(validator);
        }
        public void Load() {
            foreach (var pi in plugins) {
                if(!pi.Value.Load(this)) continue;
            }
        }
        public IEnumerable<Plugin> Plugins => plugins.Values;
        public IEnumerable<Assembly> GetPluginAssemblies() {
            var caller = Assembly.GetCallingAssembly();
            yield return caller;
            var entry = Assembly.GetEntryAssembly();
            if (entry != null && !caller.Equals(entry)) {
                yield return entry;
            }
            foreach(var pi in Plugins) {
                if(pi.Loaded) yield return pi.assembly!;
            }
        }
        void BuildPluginList(Validator validator) {
            DirectoryInfo di = new DirectoryInfo(path);
            var options = new EnumerationOptions();
            options.RecurseSubdirectories = true;
            HashSet<Plugin> loaded = new(), unload = new();
            foreach(var file in di.EnumerateFiles($"*{pluginExt}.dll", options)) {
                var pi = Plugin.Construct(file.FullName);
                if(pi != null && validator(pi.assemblyName)) {
                    plugins[pi.name] = pi;
                }
            }
        }
        public static implicit operator PluginLoadContext(Loader l) => l.context;
        PluginLoadContext context;
        Dictionary<string, Plugin> plugins = new();
        string path, pluginExt;

        bool disposed = false;
        ~Loader() {
            Dispose(false);
        }
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool manual) {
            if(disposed) return;
            if(manual) { }
            context.Unload();
            disposed = true;
        }
    }
}
