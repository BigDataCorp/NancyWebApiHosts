using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace NancyASPHost
{
    public class ModuleContainer
    {
        delegate object InstanceFactory (params object[] args);
     
        object padlock = new object ();

        Dictionary<string, List<Type>> exportedTypesByBaseType = new Dictionary<string, List<Type>> (StringComparer.Ordinal);

        Dictionary<string, Type> exportedTypes = new Dictionary<string, Type> (StringComparer.Ordinal);

        Dictionary<string, Assembly> loadedAssemblies = new Dictionary<string, Assembly> (StringComparer.Ordinal);

        Dictionary<string, InstanceFactory> exportedFactories = new Dictionary<string, InstanceFactory> (StringComparer.Ordinal);

        NLog.Logger _logger = NLog.LogManager.GetLogger ("ModuleContainer");

        static ModuleContainer _instance = new ModuleContainer ();

        public static ModuleContainer Instance
        {
            get
            {
                return _instance;
            }
        }

        /// <summary>
        /// Initializes the specified modules folder.<para/>
        /// Will load all assemblies found in the modules folder and subfolders and
        /// scan for the types derived from listOfInterfaces.
        /// </summary>
        /// <param name="modulesFolder">List of folders where plugin/modules are located.</param>
        /// <param name="listOfInterfaces">The list of interfaces or base types.</param>
        public void Initialize (string[] modulesFolder, Type[] listOfInterfaces)
        {
            lock (padlock)
            {
                ContainerInitialization (modulesFolder, listOfInterfaces);
            }
        }

        /// <summary>
        /// Initializes the specified modules folder.<para/>
        /// Will load all assemblies found in the modules folder and subfolders and
        /// scan for the types derived from listOfInterfaces.
        /// </summary>
        /// <param name="modulesFolder">The modules folder.</param>
        /// <param name="listOfInterfaces">The list of interfaces or base types.</param>
        public void Initialize (string modulesFolder, Type[] listOfInterfaces)
        {
            lock (padlock)
            {
                ContainerInitialization (new string[] { modulesFolder }, listOfInterfaces);
            }
        }
        
        private void ContainerInitialization (string[] modulesFolder, Type[] listOfInterfaces)
        {
            // if we have no interface, create an empty list
            if (listOfInterfaces == null)
                listOfInterfaces = new Type[0];

            // get base folder
            var baseAddress = AppDomain.CurrentDomain.BaseDirectory;
            // prepare extension folder
            if (modulesFolder == null || modulesFolder.Length == 0 || (modulesFolder.Length == 1 && String.IsNullOrEmpty (modulesFolder[0])))
                return;
            
            // initialize
            foreach (var t in listOfInterfaces)
                exportedTypesByBaseType[t.Name] = new List<Type> ();

            // get mscorelib assembly
            Assembly mscorelib = 333.GetType ().Assembly;

            // load current assemblies code to register their types and avoid duplicity
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!a.GlobalAssemblyCache && !a.IsDynamic && a != mscorelib)
                {
                    loadedAssemblies[a.FullName] = a;                    
                }
            }

            // register assembly resolution for our loaded modules
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            var ignoredAssemblies = new Dictionary<string,string> (StringComparer.Ordinal);

            // check the directory exists
            foreach (var folder in modulesFolder)
            {
                var directoryInfo = new DirectoryInfo (prepareFilePath(folder));
                if (!directoryInfo.Exists)
                {
                    directoryInfo.Create ();
                    directoryInfo.Refresh ();
                }
            
                // read all files in modules folder, looking for assemblies
                // let's read all files, since linux use case sensitive search wich could lead
                // to case problems like ".dll" and ".Dll"            
                foreach (var f in directoryInfo.EnumerateFiles ("*", SearchOption.AllDirectories))
                {
                    // check if file has a valid assembly extension
                    if (!f.Extension.EndsWith (".dll", StringComparison.OrdinalIgnoreCase) &&
                        !f.Extension.EndsWith (".exe", StringComparison.OrdinalIgnoreCase))
                        continue;
                    // try to avoid loading dlls with native code
                    if (f.FullName.IndexOf ("x86", StringComparison.OrdinalIgnoreCase) > 0 ||
                        f.FullName.IndexOf ("x64", StringComparison.OrdinalIgnoreCase) > 0 ||
                        f.FullName.IndexOf ("interop", StringComparison.OrdinalIgnoreCase) > 0)
                        continue;

                    // try to get assembly full name: "Assembly text name, Version, Culture, PublicKeyToken"
                    // lets ignore it if we are unable to load it (access denied, invalid assembly or native code, etc...)
                    if (ignoredAssemblies.ContainsKey (f.Name))
                        continue;
                    string fullName = null;
                    try
                    { 
                        fullName = AssemblyName.GetAssemblyName (f.FullName).FullName;
                    }
                    catch (Exception ex) 
                    {
                        ignoredAssemblies[f.Name] = ex.GetType ().Name + ", location: " + f.FullName.Replace (baseAddress, "./").Replace ('\\', '/');
                        continue;
                    }

                    // check if assembly was already loaded
                    if (loadedAssemblies.ContainsKey (fullName)) 
                        continue;

                    // try to load assembly
                    try
                    { 
                        // load assembly
                        var a = Assembly.LoadFile (f.FullName);

                        // register in our loaded assemblies lookup map for AppDomain.CurrentDomain.AssemblyResolve resolution
                        loadedAssemblies.Add (a.FullName, a);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn (ex);
                    }
                }
            }

            // try to load derived types
            try
            {
                foreach (var a in loadedAssemblies.Values)
                {
                    // dynamic assemblies don't have GetExportedTypes method
                    if (a.IsDynamic)
                        continue;
                    // search for types derived from desired types list (listOfInterfaces)
                    foreach (var t in a.GetExportedTypes ())
                    {
                        if (t == null || t.IsAbstract || t.IsGenericTypeDefinition || t.IsInterface)
                            continue;
                        for (int j = 0; j < listOfInterfaces.Length; j++)
                        {
                            if (listOfInterfaces[j].IsAssignableFrom (t))
                            {
                                if (exportedTypes.ContainsKey (t.Name))
                                {
                                    // ignore if the full name is the same!!!
                                    if (exportedTypes[t.Name].FullName == t.FullName)
                                        continue;
                                    _logger.Info ("Module with same name detected {0}. {1} was overwritten with {2}.", t.Name, exportedTypes[t.Name].FullName, t.FullName);                                    
                                }

                                // register type by fullName (namespace + name) and name
                                exportedTypes[t.FullName] = t;
                                exportedTypes[t.Name] = t;

                                // add to interface list of types
                                List<Type> list;
                                exportedTypesByBaseType.TryGetValue (listOfInterfaces[j].Name, out list);
                                if (list == null)
                                {
                                    list = new List<Type> ();
                                    exportedTypesByBaseType[listOfInterfaces[j].Name] = list;
                                }
                                list.Add (t);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn (ex);
            }

            // log initialization status
            if (ignoredAssemblies.Count > 0)
            {
                _logger.Info ("Assemblies ignorados: " + Environment.NewLine + 
                    "[" + 
                    Environment.NewLine +
                    String.Join ("," + Environment.NewLine, ignoredAssemblies.Select (i => i.Key + ": " + i.Value)) + Environment.NewLine +
                    "]");
            }
        }        

        private Assembly CurrentDomain_AssemblyResolve (object sender, ResolveEventArgs args)
        {
            Assembly a;
            if (!loadedAssemblies.TryGetValue (args.Name, out a))
                throw new InvalidOperationException (
                      String.Format ("Assembly not available in plugin/modules path; assembly name '{0}'.", args.Name));
            return a;
        }

        /// <summary>
        /// Adjust file path.
        /// </summary>
        private string prepareFilePath (string path)
        {
            if (path == null)
                return path;
            int ix = path.IndexOf ("${basedir}", StringComparison.OrdinalIgnoreCase);
            if (ix >= 0)
            {
                int tagLen = "${basedir}".Length;
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                if (path.Length > ix + tagLen && (path[ix + tagLen] == '\\' || path[ix + tagLen] == '/'))
                    appDir = appDir.EndsWith ("/") || appDir.EndsWith ("\\") ? appDir.Substring (0, appDir.Length - 1) : appDir;
                path = path.Remove (ix, tagLen);
                path = path.Insert (ix, appDir);
            }
            path = path.Replace ("\\", "/");
            return path;
        }
        /// <summary>
        /// Creates the type constructor delegate for a type.
        /// </summary>
        /// <typeparam name="T">The type to create a constructor.</typeparam>
        /// <returns></returns>
        private InstanceFactory CreateFactory<T> ()
        {
            return CreateFactory (typeof (T));
        }

        /// <summary>
        /// Creates the type constructor delegate for a type.
        /// </summary>
        /// <param name="type">The type to create a constructor.</param>
        /// <returns></returns>
        private InstanceFactory CreateFactory (Type type)
        {
            return CreateFactory (type.GetConstructors ().OrderByDescending (i => i.GetParameters ().Length).First ());
        }

        /// <summary>
        /// Creates the type constructor delegate for a type..
        /// </summary>
        /// <param name="ctor">The constructor information.</param>
        /// <returns></returns>
        private InstanceFactory CreateFactory (ConstructorInfo ctor)
        {
            // code http://rogeralsing.com/2008/02/28/linq-expressions-creating-objects/
            Type type = ctor.DeclaringType;
            ParameterInfo[] paramsInfo = ctor.GetParameters ();
            // prepare parameters            
            //create a single param of type object[]
            var param = Expression.Parameter (typeof (object[]), "args");

            var argsExp = new Expression[paramsInfo.Length];

            //pick each arg from the params array 
            //and create a typed expression of them
            for (int i = 0; i < paramsInfo.Length; i++)
            {
                var index = Expression.Constant (i);
                Type paramType = paramsInfo[i].ParameterType;

                var paramAccessorExp = Expression.ArrayIndex (param, index);

                var paramCastExp = Expression.Convert (paramAccessorExp, paramType);

                argsExp[i] = paramCastExp;
            }

            //make a NewExpression that calls the
            //ctor with the args we just created
            var newExp = Expression.New (ctor, argsExp);
           
            //create a lambda with the New
            //Expression as body and our param object[] as arg
            var lambda = Expression.Lambda (typeof (InstanceFactory), newExp, param);

            //compile it
            var compiled = (InstanceFactory)lambda.Compile ();
            return compiled;
        }

        /// <summary>
        /// Gets an instance for a registered type.
        /// </summary>
        /// <typeparam name="T">The type of the T.</typeparam>
        /// <param name="fullTypeName">Full name of the type.</param>
        /// <returns></returns>
        public T GetInstance<T> (string fullTypeName) where T : class
        {
            return GetInstance (fullTypeName) as T;
        }

        /// <summary>
        /// Gets an instance for a registered type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        public object GetInstance (Type type)
        {
            return GetInstance (type.FullName);
        }

        /// <summary>
        /// Gets an instance for a registered type by its full type name.
        /// </summary>
        /// <param name="fullTypeName">Full name of the type, namespace and class name.</param>
        /// <returns></returns>
        public object GetInstance (string fullTypeName)
        {
            Type t;
            InstanceFactory ctor;
            // if we have the type, lets get it
            if (exportedTypes.TryGetValue (fullTypeName, out t))
            {
                if (!exportedFactories.TryGetValue (t.FullName, out ctor))
                {
                    ctor = CreateFactory (t);
                    lock (padlock)
                    {
                        exportedFactories[t.FullName] = ctor;
                    }
                }
                return ctor ();
            }
            return null;
        }

        /// <summary>
        /// Gets an instance that implements the desired type T.
        /// </summary>
        /// <param name="type">The base type.</param>
        /// <returns></returns>
        public T GetInstance<T> () where T : class
        {
            return GetInstances<T> ().FirstOrDefault ();
        }

        /// <summary>
        /// Gets instances for all registered types for a given interface or base type.
        /// </summary>
        /// <typeparam name="T">The interface or base type.</typeparam>
        /// <returns>List of intances of registered types</returns>
        public IEnumerable<T> GetInstances<T> () where T : class
        {
            foreach (var t in GetTypes<T> ())
                yield return GetInstance<T> (t.FullName);
        }

        /// <summary>
        /// Gets all registered types for a given interface or base type.
        /// </summary>
        /// <typeparam name="T">The interface or base type.</typeparam>
        /// <returns>List of registered types</returns>
        public IEnumerable<Type> GetTypes<T> () where T : class
        {
            List<Type> list;
            if (!exportedTypesByBaseType.TryGetValue (typeof (T).Name, out list))
                throw new Exception ("Service Type unknow. Type not registered on ModuleContainer.Initialize call.");
            for (int i = 0; i < list.Count; i++)
                yield return list[i];
        }
    }
}
