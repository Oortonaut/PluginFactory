using System;
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;

// Released into the public domain July 5, 2022 by Acy 'Ace' Stapp

namespace Oortonaut.PluginFactory {
    public class ResultFactory<IResult> {
        #region API
        // Say our example plugin interface is ```IGameObject(int id, float3 position, string script)```
        // we initialize the plugin factory with the list of those types like so:
        //      var factory = new Factory<IGameObject>(typeof(int), typeof(float3), typeof(string));
        //                       Constructor Argument Types -----^------------^---------------^
        // The factory then spins through all of the loaded classes and remembers which ones can be
        // constructed using your supplied argument types.
        protected ResultFactory(Type[] argTypes_) {
            argTypes = argTypes_;
        }
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
        protected Type[] argTypes;
        List<string> errors = new();
        #endregion

        //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
        #region Workers (this really should go in a utilities class)
        protected IResult? CreateError(string typeName) => Error($"Create: failed to find {typeName}");
        protected IResult? Error(string typeName) {
            errors.Add($"Create: failed to find {typeName}");
            return default;
        }
        protected IEnumerable<(Type type, ConstructorInfo ci)> GetCtors(Assembly a) {
            // For all of the loaded types
            foreach(Type t in a.GetTypes().Where(t => !t.IsInterface && !t.IsAbstract)) {
                // that implement our desired interface
                if(t.GetInterfaces().Contains(typeof(IResult))) {
                    // and a compliant constructor
                    var ci = t.GetConstructor(argTypes);
                    if(ci != null) {
                        // return the type and constructor
                        yield return (t, ci);
                    } else {
                        // otherwise we didn't have a constructor
                        Error($"{t.Name}: {nameof(IResult)}: No constructor: assembly {a.GetName()}");
                    }
                } // else the type doesn't implement our interface
            }
        }
        protected IEnumerable<(Type type, MethodInfo mi)> GetStaticMethods(Assembly a, string? name) {
            foreach(Type t in a.GetTypes().Where(t => !t.IsInterface && !t.IsAbstract)) {
                foreach (var mi in t.GetMethods()) {
                    var methodArgTypes = mi.GetParameters().Select(pi => pi.ParameterType).ToArray();
                    if (argTypes.SequenceEqual(methodArgTypes)) {
                        if(name == null || mi.Name == name) {
                            if (mi.ReturnType.GetInterfaces().Contains(typeof(IResult))) {
                                yield return (t, mi);
                            }
                        }
                    }
                }
            }
        }
        #endregion
    }
    public class ConstructorFactory<FDelegate, IResult>: ResultFactory<IResult> {
        public static string? staticCtorMethod = null;
        // These are all the same for constructors but can vary for static methods
        // 0 = result type name, 1 = class type name, 2 = method name,
        // 3 = plugin name, 4 = plugin version
        public static string classCtorFmt = @"{0}";
        // public static string staticCtorFmt = @"{0} {3}.{2}.{1}";
        public static string staticCtorFmt = @"{3}:{4}";
        // This factory builds classes based on matching parameter types
        public ConstructorFactory(IEnumerable<Assembly> assemblies, Type[] args) : base(args) {
            foreach(Assembly a in assemblies) {
                foreach(var ci in GetCtors(a)) {
                    var ciType = ci.type;
                    var asmName = ciType.Assembly.GetName();
                    var name = string.Format(classCtorFmt, ciType.Name, ciType.Name, ciType.Name, asmName.Name, asmName.Version);
                    try {
                        ctors.Add(name, CompileCtor(ci.ci));
                    } catch(Exception x) {
                        Error(x.Message);
                    }
                }
                foreach(var ctor in GetStaticMethods(a, staticCtorMethod)) {
                    var mi = ctor.mi;
                    var reType = mi.ReturnType;
                    var miType = mi.ReflectedType ?? reType;
                    var asmName = miType.Assembly.GetName();
                    var name = string.Format(staticCtorFmt, reType.Name, mi.Name, miType.Name, asmName.Name, asmName.Version);
                    try {
                        ctors.Add(name, CompileStaticMethod(mi));
                    } catch(Exception x) {
                        Error(x.Message);
                    }
                }
            }
            
            if(!ctors.Any()) Error($"No plugins found.");
        }
        public ConstructorFactory(Type[] args) : this(AppDomain.CurrentDomain.GetAssemblies(), args) { }
        // This is if you want a list of the loaded plugins for an editor or something.
        public IEnumerable<string> Ctors => ctors.Keys;
        protected Entry CompileCtor(ConstructorInfo ci) {
            var paramType = ci.GetParameters();
            var paramExpr = argTypes.Select(t => Expression.Parameter(t)).ToArray();
            var lambda = Expression.Lambda<FDelegate>(Expression.New(ci, paramExpr), paramExpr);
            var del = lambda.Compile();
            return new(del, ci.GetType().Assembly);
        }
        protected Entry CompileStaticMethod(MethodInfo mi) {
            var paramType = mi.GetParameters();
            var paramExpr = argTypes.Select(t => Expression.Parameter(t)).ToArray();
            var lambda = Expression.Lambda<FDelegate>(Expression.Call(mi, paramExpr), paramExpr);
            var del = lambda.Compile();
            return new(del, mi.GetType().Assembly);
        }
        protected FDelegate? GetCtor(string typeName) {
            if(ctors.TryGetValue(typeName, out Entry? ci)) return ci.ctor;
            else { Error($"{typeName}: Requested type unrecognized"); return default; }
        }
        //===========================================================
        protected record Entry(FDelegate? ctor, Assembly assembly);
        protected Dictionary<string, Entry> ctors = new();
    }
    // Create a factory for each combo of result interface and
    // arguments types. Often there will just be one.

    // Often, you'll have the types saved in a file or just hardcoded. Use the Create method and
    // the argument values to create the object.
    //      var result = factory.Create("NPC", id++, spawnPosition, "vendor-armor.lua");
    //                     Plugin type ---^      ^-----^- Constructor arguments -^
    // Make sure to check if it succeeded.
    //      if (result != null) world.Add(result);
    public class Factory<IResult>: ConstructorFactory<Func<IResult>, IResult> {
        public Factory() : base(new Type[] {}) { }
        public Factory(IEnumerable<Assembly> assemblies) : base(assemblies, new Type[] {}) { }
        public IResult? Create(string typeName) =>
            GetCtor(typeName) switch { null => default, var ctor => ctor() };
    }
    public class Factory<A, IResult>: ConstructorFactory<Func<A, IResult>, IResult> {
        public Factory() : base(new[] { typeof(A) }) { }
        public Factory(IEnumerable<Assembly> assemblies) : base(assemblies, new[] { typeof(A) }) { }
        public IResult? Create(string typeName, A a) =>
            GetCtor(typeName) switch { null => default, var ctor => ctor(a) };
    }
    public class Factory<A, B, IResult>: ConstructorFactory<Func<A, B, IResult>, IResult> {
        public Factory() : base(new[] { typeof(A), typeof(B) }) { }
        public Factory(IEnumerable<Assembly> assemblies) : base(assemblies, new[] { typeof(A), typeof(B) }) { }
        public IResult? Create(string typeName, A a, B b) =>
            GetCtor(typeName) switch { null => default, var ctor => ctor(a, b) };
    }
    public class Factory<A, B, C, IResult>: ConstructorFactory<Func<A, B, C, IResult>, IResult> {
        public Factory() : base(new[] { typeof(A), typeof(B), typeof(C) }) { }
        public Factory(IEnumerable<Assembly> assemblies) : base(assemblies, new[] { typeof(A), typeof(B), typeof(C) }) { }
        public IResult? Create(string typeName, A a, B b, C c) =>
            GetCtor(typeName) switch { null => default, var ctor => ctor(a, b, c) };
    }
    public class Factory<A, B, C, D, IResult>: ConstructorFactory<Func<A, B, C, D, IResult>, IResult> {
        public Factory() : base(new[] { typeof(A), typeof(B), typeof(C), typeof(D) }) { }
        public Factory(IEnumerable<Assembly> assemblies) : base(assemblies, new[] { typeof(A), typeof(B), typeof(C), typeof(D) }) { }
        public IResult? Create(string typeName, A a, B b, C c, D d) =>
            GetCtor(typeName) switch { null => default, var ctor => ctor(a, b, c, d) };
    }
    public class Factory<A, B, C, D, E, IResult>: ConstructorFactory<Func<A, B, C, D, E, IResult>, IResult> {
        public Factory() : base(new[] { typeof(A), typeof(B), typeof(C), typeof(D), typeof(E) }) { }
        public Factory(IEnumerable<Assembly> assemblies) : base(assemblies, new[] { typeof(A), typeof(B), typeof(C), typeof(D), typeof(E) }) { }
        public IResult? Create(string typeName, A a, B b, C c, D d, E e) =>
            GetCtor(typeName) switch { null => default, var ctor => ctor(a, b, c, d, e) };
    }
    public class Factory<A, B, C, D, E, F, IResult>: ConstructorFactory<Func<A, B, C, D, E, F, IResult>, IResult> {
        public Factory() : base(new[] { typeof(A), typeof(B), typeof(C), typeof(D), typeof(E), typeof(F) }) { }
        public Factory(IEnumerable<Assembly> assemblies) : base(assemblies, new[] { typeof(A), typeof(B), typeof(C), typeof(D), typeof(E), typeof(F) }) { }
        public IResult? Create(string typeName, A a, B b, C c, D d, E e, F f) =>
            GetCtor(typeName) switch { null => default, var ctor => ctor(a, b, c, d, e, f) };
    }                                                                              
}
