# PluginFactory
PluginFactory is a simple backend that lets game makers support end user-created mods
and plugins provided as .NET assemblies. It's super fast, if you consider 26ns super
fast.

Do you need plugins? You do if your game has these:
  * Logging
  * Tracing
  * Units
  * Behaviors
  * Recipes
  * Scripting
  * Lots of drops
  * Procedural Levels
  * Procedural Characters
  * Procedural Anything

## I am a...
### ... Game Developer
Easily add the ability to mod your app or game by making any `interface` pluggable.
Plugins expose constructors for pluggable classes one or more interfaces.
You ship a reference assembly with your public for modders to use. 
Plugins can be loaded and unloaded to support runtime configuration changes.

### ... Plugin or Mod Developer
You link the reference assembly into your project and write classes to implement 
the plugin interfaces. You package your plugin as a single .NET assembly with
embedded or associated text and binary resources.

### ... End User
You'll get a plugin from the game or mod developer. It might be a .dll or a
zip file. Just unzip that baby anywhere in your plugins/ folder and you're good
to go.

The developer can easily enumerate, load and 
unload plugins using the Loader class. With the Plugin class, they can plugin resources
and query its supported interfaces and types. Finally, they use a Factory to quickly
and efficiently create new instances of those types.

# ENVIRONMENT NOTE: GIT SYMLINKS / CS6.0 / NULLABLE
This repository uses softlinks at the moment. Please turn them on. I'm in the process
of getting rid of them. If softlinks are a problem you'll have to copy the reference
assembly to the correct location manually for the Test.plugin project.

This project targets .NET 6.0 and C# 10 and explicit nullable.

## Documentation

Todo, probably a separate doc. See Usage

## In the Works
  * .zip file support
  * A Plugin that represents the game assembly (for resources mostly)
  * Patch object for script patching
  * Dependency system
  * Plugin info/singleton types
  * Multiple plugin paths for loader
  * Better duplicate handling for loader
  * Demo projects 
     1. Basic logger - using just the factory class, creates loggers from
        the default execution context assemblies. Several builtin loggers
        are provided.
     2. Script player and plugin - an extremely simple scripting system
        dispatches script commands based on the first word in a line.
        A plugin is used to load some new scripting commands, which have
        access to the game infrastructure, including logging.
     3. Resources - the main app accesses plugin resources and plugin
        types have access to their own plugin resources as well as game
        resources (?).

## Resources
Resources are associated with loaded plugins and equivalently loaded 
assemblies. They're not available until an assembly is loaded. However,
you can store them off, unload the loader and its assemblies, and the 
stored resources will survive.

The resource compiler strips some extensions. Be wary of this if you are 
using extensions to differentiate between resources types, bitmap vs png, etc.
Use a magic number based technique instead or enforce binary format standards. 
As far as I can tell, the compiled resource types, and whether they appear in
TextKeys or BinKeys depends only on text (string) vs binary (byte[]).

I removed the .cs plumbing file used for the Resources. If your app is using
a windows UI you should probably keep that.

## Usage

### High Level / Simplest
    string pluginPath = "plugins/";
    using(Loader loader = new Loader(pluginPath, ".plugin")){
        loader.Load();
        foreach(Plugin pi in loader.Plugins) {
            Console.WriteLine($"Loading plugin {pi.name}:{pi.version} ({pi.fileName})");
            if(pi.Loaded) { // plugins can also be loaded on demand here
                foreach(var s in pi.TextKeys) // strings and text files
                    Console.WriteLine($"Text: {s}={pi.Text(s)}");
                foreach(var s in pi.BinKeys) // everything else
                    Console.WriteLine($"Bin:  {s} ({pi.Bin(s)!.Length})\n{HexBytes(pi.Bin(s)!, 4, new[] { 2, 2 }, 4)}");
                foreach(var s in pi.OtherKeys) // everything everything else
                    Console.WriteLine($"Othr: {s} Type {s.GetType()} String {s.ToString().Take(60)}");
            }
        }

        var factory = new FooFactory(loader.GetPluginAssemblies());
        foreach (string ctorName in factory.Ctors) {
            ILogger newItem = factory.Create(ctorName, GetVerbosity(ctorName));
            loggers.Add(newItem); // needs null check
        }
    }

### Low Level / Advanced?
    using LoggerFactory = Factory<Verbosity, ILogger>;
    public enum Verbosity { Debug, Info, Warning, Error };
    public interface ILogger { void Log(string x); }
    public class ConsoleLogger: ILogger { 
        public ConsoleLogger(Verbosity level) {} 
        public void Log(string x) {}
    }

    // Create a factory when your app starts up
    var factory = new LoggerFactory();

    // Create a specific logger
    var result = factory.Create("ConsoleLogger", Verbosity.Info);

    // Create all loggers
    // Example of adding menu items for each class exposed by the plugin
    foreach(var ctorName in factory.Ctors) {
        loggers.Add(factory.Create(ctorName, GetVerbosity(ctorName)));
    }
    // Loading from serialization
    foreach (var logger in config.loggers) ...

To be clear, you only create the factory once, at the start of the game or app.
This was unclear to a reddit commentator, and to be fair, it is
extremely slow if you create a new factory for every new object.

Use Assembly.Load() to activate plugins before creating the factory.

### Performance

On an i5-5670K, underclocked, the benchmark shows an overhead of ~26ns. This was a
best case; usually it runs 30-31ns. There's a couple of ways to bring that down by
a few more ns but they add a little hassle.

Factory = 25.8ns, Alloc = 8.5ns, Total = 34.3ns
YAY :)

We can of course naively reciprocate this to get a real big number - about 
38 million in this case. If you are creating that many objects, hell, good for
you, but I never will, at least from a plugin. So there's that.

