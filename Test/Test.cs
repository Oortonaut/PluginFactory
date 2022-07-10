using Oortonaut;
using System;
using System.IO;
using System.Collections;
using System.Reflection;
using System.Resources;
using System.Text;
using FooFactory = Oortonaut.PluginFactory.Factory<string, int, IFoo>;
using FooSingletonFactory = Oortonaut.PluginFactory.Factory<Oortonaut.PluginFactory.Plugin, IFooPlugin>;
using Oortonaut.PluginFactory;

Test.TestLoader();

if (Test.Run()) {
    Console.WriteLine("YAY :)");
    Test.Benchmark();
    return 0;
} else {
    Console.WriteLine("OMG NO :(");
    return 1;
}

// foo = Create(string, int);
public interface IFoo { 
    string name { get; } 
    int number { get; }
}
// fooPlugin = Create(Plugin pi, string player)
public interface IFooPlugin {
    string motd { get; }
}
static class Test {
    public record Bar(string name, int number): IFoo;
    public class Baz: IFoo {
        public Baz(string s, int n) {
            name_ = s;
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
    public delegate IFoo TestFunc(string a, int b);
    public static bool Run() {
        try {
            bool result = true;
            var factory = new FooFactory();
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
            foreach(var plugin in factory.Ctors) {
                Console.WriteLine($"Plugin '{plugin}'");
                string instanceName = plugin + " instance";
                var pi = factory.Create(plugin, instanceName, i++);
                result &= pi != null;
            }
            result &= factory.Ctors.Count() > 0;
            var q = factory.TakeErrors().ToList();
            result &= q.Count == 7;
            if(!result) {
                Console.WriteLine($"Reported Errors:");
                foreach(var err in q) {
                    Console.WriteLine(err);
                }
            }
            return result;
        } catch {
            Console.WriteLine("Exception");
            return false;
        }
    }
    static int benchmarkSum = 1;
    public static double Benchmark() {
        // prime it
        int batchCount = 1000;
        int batchItems = 1000;
        double scale = 1e9 / (batchCount * batchItems);
        var factory = new FooFactory();
        IFoo? bar;
        for(int i = 0; i < batchCount; ++i) {
            bar = new Baz("bar", i);
            benchmarkSum = benchmarkSum * 10501 + 374333473;
        }
        double factoryAllocTime = 0;
        double allocTime = 0;
        var s = new System.Diagnostics.Stopwatch();

        for(int j = 0; j < batchCount; ++j) {
            s.Restart();
            for(int i = 0; i < batchItems; ++i) {
                bar = factory.Create("Bar", "baz", i);
                benchmarkSum = benchmarkSum * 10501 + 374333473;
            }
            factoryAllocTime += s.Elapsed.TotalSeconds * scale;
            s.Restart();
            for(int i = 0; i < batchItems; ++i) {
                bar = new Bar("baz", i);
                benchmarkSum = benchmarkSum * 10501 + 374333473;
            }
            allocTime += s.Elapsed.TotalSeconds * scale;
        }
        double factoryTime = factoryAllocTime - allocTime;
        Console.WriteLine($"Factory = {factoryTime:0.#}ns, Alloc = {allocTime:0.#}ns, Total = {factoryAllocTime:0.#}ns");
        return factoryTime;
    }
    static string HexBytes(byte[] data, int wordSize, int[] colScales, int rows) {
        string result = "";
        int ofs = 0;
        int cols = 1;
        foreach(var c in colScales) cols *= c;
        for (int row = 0; row < rows; ++row) {
            string hexLine = "";
            string charLine = "";
            for(int word = 0; word < cols; ++word) {
                // little endian
                for(int wordByte = wordSize; wordByte-- > 0;) {
                    if(ofs + wordByte < data.Length) {
                        byte datum = ofs + wordByte < data.Length ? data[ofs + wordByte] : default;
                        hexLine += string.Format("{0:X2}", datum);
                        charLine += (datum >= 32 && datum <= 255) ? ( char ) datum : '.';
                    } else {
                        hexLine += "  ";
                        charLine += ' ';
                    }
                }
                hexLine += ' ';
                if(wordSize > 1) charLine += ' ';
                ofs += wordSize;
                int space = 1;
                foreach (var c in colScales) {
                    space *= c;
                    if (word % space == (space-1)) {
                        hexLine += " ";
                        charLine += " ";
                    }
                }
            }
            result += hexLine.Trim() + " : " + charLine.Trim() + '\n';
        }
        return result;
    }
    public static void TestLoader() {
        {
            var factory = new FooFactory();
            int count = factory.Ctors.Count();
            Console.WriteLine($"Before loader {count}");
        }
        string pluginPath = "../../../plugins/";
        using(var loader = new Loader(pluginPath, ".plugin")){
            foreach(var pi in loader.Plugins) {
                Console.WriteLine($"Loading plugin {pi.name}:{pi.version} ({pi.fileName})");
                if(pi.Load(loader)) {
                    foreach(var s in pi.TextKeys) {
                        Console.WriteLine($"Text: {s}={pi.Text(s)}");
                    }
                    foreach(var s in pi.BinKeys) {
                        Console.WriteLine($"Bin:  {s} ({pi.Bin(s)!.Length})\n{HexBytes(pi.Bin(s)!, 4, new[] { 2, 2 }, 4)}");
                    }
                    foreach(var s in pi.OtherKeys) {
                        Console.WriteLine($"Othr: {s} Type {s.GetType()} String {s.ToString().Take(60)}");
                    }
                }
            }
        }
        using(var loader = new Loader(pluginPath, ".plugin")) {
            loader.Load();
            var factory = new FooFactory(loader.GetPluginAssemblies());
            foreach (var ctorName in factory.Ctors) {
                Console.WriteLine($"{ctorName}");
            }
            int count = factory.Ctors.Count();
            Console.WriteLine($"With plugins loaded {count}ctors");
        }
        using(var loader = new Loader(pluginPath, ".plugin", asmName => asmName.Name == "Test")) {
            loader.Load();
            var factory = new FooFactory(loader.GetPluginAssemblies());
            foreach(var ctorName in factory.Ctors) {
                Console.WriteLine($"{ctorName}");
            }
            int count = factory.Ctors.Count();
            Console.WriteLine($"Test only {count}ctors");
        }
        using(var loader = new Loader(pluginPath, ".not-plugin")) {
            loader.Load();
            var factory = new FooFactory(loader.GetPluginAssemblies());
            foreach(var ctorName in factory.Ctors) {
                Console.WriteLine($"{ctorName}");
            }
            int count = factory.Ctors.Count();
            Console.WriteLine($"No plugins found {count}ctors");
        }
    }
}

