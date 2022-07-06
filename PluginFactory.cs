using System;
using System.Reflection;

// Released into the public domain July 5, 2022 by Acy 'Ace' Stapp

namespace Oortonaut {
    // Create a factory for each combo of result interface and
    // arguments types. Often there will just be one.
    public class PluginFactory<Interface> {
        #region API
        // Say our example plugin interface is ```IGameObject(int id, float3 position, string script)```
        // we initialize the plugin factory with the list of those types like so:
        //      var factory = new PluginFactory<IGameObject>(typeof(int), typeof(float3), typeof(string));
        //                       Constructor Argument Types -----^------------^---------------^
        // The factory then spins through all of the loaded classes and remembers which ones can be
        // constructed using your supplied argument types.
        public PluginFactory(params Type[] argTypes_) {
            argTypes = argTypes_;
            foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                foreach(var ci in GetAssemblyPluginInterfaces(a))
                    constructors.Add(ci.type.Name, ci.ci);
        }
        // Often, you'll have the types saved in a file or just hardcoded. Use the Create method and
        // the argument values to create the object.
        //      var result = factory.Create("NPC", id++, spawnPosition, "vendor-armor.lua");
        //                     Plugin type ---^      ^-----^- Constructor arguments -^
        // Make sure to check if it succeeded.
        //      if (result != null) world.Add(result);
        public Interface? Create(string typeName, params object[] args) {
            constructors.TryGetValue(typeName, out ConstructorInfo? ci);
            if (ci == null) errors.Add($"Create: failed to find {typeName}");
            return ( Interface? ) (ci?.Invoke(null, args) ?? null);
        }
        // This is if you want a list of the loaded plugins for an editor or something.
        public IEnumerable<string> Plugins => constructors.Where(kv => null != kv.Value).Select(kv => kv.Key);
        // If there were any problems loading, they will be in the errors list.
        // Take it here and do with it as you will.
        public List<string> TakeErrors() { 
            var result = errors;
            errors = new();
            return result;
        }
        #endregion

        //===========================================================
        #region Members
        Type[] argTypes;
        Dictionary<string, ConstructorInfo?> constructors = new();
        List<string> errors = new();
        #endregion

        //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
        #region Workers (this really should go in a utilities class)
        IEnumerable<(Type type, ConstructorInfo ci)> GetAssemblyPluginInterfaces(Assembly a) {
            // For all of the loaded types
            foreach(Type t in a.GetTypes().Where(t => !t.IsInterface && !t.IsAbstract)) {
                // that implement our desired interface
                if (t.GetInterfaces().Contains(typeof(Interface))) {
                    // and a compliant constructor
                    var ci = t.GetConstructor(argTypes);
                    if(ci != null) {
                        // return the type and constructor
                        yield return (t, ci);
                    } else {
                        // otherwise we didn't have a constructor
                        errors.Add($"{t.Name}: {nameof(Interface)}: No constructor: assembly {a.GetName()}");
                    }
                } // else the type doesn't implement our interface
            }
        }
        #endregion
    }
    #region Test
    public static class Test {
        public interface IFoo { string name { get; } int number { get; } }
        public record Bar(string name, int number): IFoo;
        public class Baz: IFoo {
            public Baz(string s, int n) {
                name_ = s + "-baz";
                number_ = -n;
            }
            string name_;
            int number_;
            public string name => name_;
            public int number => number_;
        }
        public static IFoo MakeIFoo(string a, int b) {
            return new Bar(a, b);
        }
        public static bool Run() {
            try {
                bool result = true;
                var factory = new PluginFactory<IFoo>(typeof(string), typeof(int));
                var bar = factory.Create("Bar", "bar", 420);
                result &= bar != null && bar.GetType().Equals(typeof(Bar));
                var baz = factory.Create("Baz", "baz", 42);
                result &= baz != null && baz.GetType().Equals(typeof(Baz));
                var bazCached = factory.Create("Baz", "baz2", 2);
                result &= bazCached != null && bazCached.GetType().Equals(typeof(Baz));
                result &= baz != null && bazCached != null && baz.name != bazCached.name && baz.number != bazCached.number;
                var plugh = factory.Create("Plugh", "", 0);
                result &= plugh == null;
                int i = 0;
                foreach(var plugin in factory.Plugins) {
                    var pi = factory.Create(plugin, plugin + " instance", i++);
                    result &= pi != null;
                }
                result &= factory.Plugins.Count() > 0;
                var q = factory.TakeErrors().ToList();
                result &= q.Count == 0;
                return result;
            } catch { return false; }
        }
    }
    #endregion
}
