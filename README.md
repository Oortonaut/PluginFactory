# PluginFactory

PluginFactory is a simple solution to accessing plugin classes for
your .NET game or other application at runtime. It enumerates all 
loaded assemblies and extracts class constructors that match the
signature you specify. Then you call these constructors by name
to create objects.

# Building

Option 1: Just include PluginFactory.cs in your project. Change the namespace!
Make it yours and enjoy!

Option 2: Link in the PluginFactory library by adding a dependency to your project
or the compiled DLL (I think?).

# Usage

It's very simple and documented inline. But here's an example':

    public interface IGameObject {} // Plugins will implement this
    public class NPC: IGameObject { // A sample class for testing.
        public NPC(int id, float3 pos, string script) {} 
    }

    // Create a factory when your app starts up
    var factory = new PluginFactory<IGameObject>(typeof(int), typeof(float3), typeof(string));

    // Example of creating an NPC using the class name.
    var result = factory.Create("NPC", id++, spawnPosition, "vendor-armor.lua");

    // Example of adding menu items for each class exposed by the plugin
    foreach(var pluginName in factory.Plugins) {
        EditorMenu(pluginName, () => factory.Create(pluginName, id++, editorPos, defaultScript));
    }

    // Example of loading a saved game
    foreach (var saved in saveGame.actors) {
        Add(factory.Create(saved.module, saved.id, saved.pos, saved.script);
    }

To be clear, you only create the factory once, at the start of the game or app.
This was unclear to a reddit commentator, and to be fair, it is
extremely slow if you create a new factory for every new object.

Use Assembly.Load() to activate plugins before creating the factory.

# Performance

On an i5-5670K, underclocked, the benchmark shows an overhead of ~26ns. This was a
best case; usually it runs 30-31ns.

Factory = 25.8ns, Alloc = 8.5ns, Total = 34.3ns
YAY :)

We can of course naively reciprocate this to get a real big number - about 
38 million in this case. If you are creating that many objects, hell, good for
you, but I never will, at least from a plugin. So there's that.

