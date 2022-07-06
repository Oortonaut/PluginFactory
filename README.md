# PluginFactory

PluginFactory is a simple solution to accessing plugin classes for
your .NET game or other application at runtime. It enumerates all 
loaded assemblies and extracts class constructors that match the
signature you specify. Then you call these constructors by name
to create objects.

# Building

Put it in your project. You can delete the Test class if you'd like
40 lines back.

# Usage

It's very simple and documented inline. But here's an example':

      public class NPC: IGameObject { 
          NPC(int id, float3 pos, string script) {} 
      }
      var factory = new PluginFactory<IGameObject>(typeof(int), typeof(float3), typeof(string));
      var result = factory.Create("NPC", id++, spawnPosition, "vendor-armor.lua");
      foreach(var pluginName in factory.Plugins) {
          EditorMenu(pluginName, () => factory.Create(pluginName, id++, editorPos, defaultScript));
      }
